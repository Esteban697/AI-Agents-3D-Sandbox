using ollama;
using System.Collections.Generic;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DemoChat : MonoBehaviour
{
    [SerializeField] private Texture2D capturedTexture;
    [SerializeField] private Image rotatingSquare;
    
    [Header("Models")]
    [SerializeField]
    private string demoModel = "gemma3:4b";

    [Header("UI")]
    [SerializeField]
    private TMP_Text llmOutput;
    [SerializeField]
    private TMP_InputField userInput;

    private Queue<string> buffer;
    private bool isStreaming;

    private void StreamFinished() { isStreaming = false; }
    void OnEnable() { Ollama.OnStreamFinished += StreamFinished; }
    void OnDisable() { Ollama.OnStreamFinished -= StreamFinished; }

    private bool italic;
    private bool bold;
    
    // Camera setup
    public Camera sourceCamera;
    public int targetWidth = 480;
    public int targetHeight = 480;

    void LateUpdate()
    {
        if (!isStreaming)
            return;

        // The following formatting is based on gemma3:4b
        while (buffer.TryDequeue(out string text))
        {
            text = text.Replace("\n\n", "\n");

            if (text.Contains("**"))
            {
                bold = !bold;
                if (bold)
                    text = text.Replace("**", "<b>");
                else
                    text = text.Replace("**", "</b>");
            }

            if (text.Contains('*'))
            {
                italic = !italic;
                if (italic)
                    text = text.Replace("*", "<i>");
                else
                    text = text.Replace("*", "</i>");
            }

            llmOutput.text += text;
        }
    }

    void Awake() {  } // no need to launch at Awake because the local LLM will be launched already in another PC

    void Start()
    {
        buffer = new Queue<string>();
        Ollama.InitChat();
    }

    /// <summary>Called by <b>TMP_InputField</b></summary>
    public void OnSubmit(string input)
    {
        if (isStreaming)
            return;

        llmOutput.text += $"<align=right>\"{input}\"</align>\n<align=left><line-height=60%>";
        isStreaming = true;
        userInput.interactable = false;

        bold = false;
        italic = false;
        
        // Capture frame
        StartCoroutine(GetTextureFromCamera(sourceCamera, (txt2D) =>
        {
            // Copy this texture into memory before using it
            CopyCapturedTexture(txt2D, out capturedTexture, true);
            SendTextAndImage(capturedTexture, input);
        }));
    }

    private async void SendTextAndImage(Texture2D texture, string input)
    {
        // Show image for ease of use
        rotatingSquare.sprite = Sprite.Create(texture, new Rect(0, 0, targetWidth, targetHeight), Vector2.zero);
        Debug.Log($"Handing over a texture of size w:{texture.width} h: {texture.height}. With format: {texture.format}");
        // The call ChatStream
        await Ollama.ChatStream((string text) => 
            buffer.Enqueue(text), demoModel, input, 300, texture);

        llmOutput.text += "</line-height></align>\n";
        isStreaming = false;
        userInput.interactable = true;
    }
    
    
    private IEnumerator GetTextureFromCamera(Camera mCamera, Action<Texture2D> callback)
    {
        RenderTexture renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
        renderTexture.antiAliasing = 1;
        Texture2D screenShot = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);

        mCamera.targetTexture = renderTexture;
        mCamera.Render();
        
        yield return new WaitForEndOfFrame();
        
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = renderTexture;
        
        screenShot.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        screenShot.Apply(false, false);
        
        RenderTexture.active = prev;
        mCamera.targetTexture = null;
        renderTexture.Release();
        callback(screenShot);
    }

    private void CopyCapturedTexture(Texture2D inTex, out Texture2D outTex, bool considerMipmapLimits)
    {
        int width = inTex.width;
        int height = inTex.width;

        Texture2DArray outArray = new Texture2DArray(width, height, 24, inTex.format, false);
        outTex = new Texture2D(width, height, inTex.format, false);

        if (!considerMipmapLimits)
        {
            // Texture2D -> Texture2DArray
            // Global Mipmap Limit: "1: Half Resolution" => copies into mips that are now too large; will copy each mip of inTex into a quarter of outArray
            for (int mip = 0; mip < inTex.mipmapCount; ++mip)
            {
                int copyWidth = width >> mip;
                int copyHeight = height >> mip;
                Graphics.CopyTexture(inTex, 0, mip, 0, 0, copyWidth, copyHeight, outArray, 0, mip, 0, 0);
            }

            // Texture2DArray -> Texture2D
            // Global Mipmap Limit: "1: Half Resolution" => errors, since we try to copy into mips that are now too small
            for (int mip = 0; mip < outArray.mipmapCount; ++mip)
            {
                int copyWidth = width >> mip;
                int copyHeight = height >> mip;
                Graphics.CopyTexture(outArray, 0, mip, 0, 0, copyWidth, copyHeight, outTex, 0, mip, 0, 0);
            }
        }
        else // considering mipmap limits
        {
            int globalMipmapLimit = QualitySettings.globalTextureMipmapLimit;

            // Texture2D -> Texture2DArray
            // Global Mipmap Limit: "1: Half Resolution" => mip0 of outArray is not written to, other mips copy as expected
            //  (ALTERNATIVE: if outArray creation already considered globalMipmapLimit for its dimensions,
            //   the CopyTexture call can ignore globalMipmapLimit since the mips will line up again)
            for (int mip = 0; mip < inTex.mipmapCount - globalMipmapLimit; ++mip)
            {
                int copyWidth = width >> mip;
                int copyHeight = height >> mip;
                int srcMip = mip;
                int dstMip = mip + globalMipmapLimit;
                Graphics.CopyTexture(inTex, 0, srcMip, 0, 0, copyWidth, copyHeight, outArray, 0, dstMip, 0, 0);
            }

            // Texture2DArray -> Texture2D
            // Global Mipmap Limit: "1: Half Resolution" => mip0 of outArray is not copied (but outTex does not upload it to GPU anyway)
            for (int mip = globalMipmapLimit; mip < outArray.mipmapCount; ++mip)
            {
                int copyWidth = width >> mip;
                int copyHeight = height >> mip;
                int srcMip = mip;
                int dstMip = mip - globalMipmapLimit;
                Graphics.CopyTexture(outArray, 0, srcMip, 0, 0, copyWidth, copyHeight, outTex, 0, dstMip, 0, 0);
            }
        }
    }
    
    private Texture2D GetTestTexture()
    {
        Texture2D tex = new Texture2D(480, 480, TextureFormat.RGB24, false);
        for (int y = 0; y < 480; y++)
        for (int x = 0; x < 480; x++)
        {
            bool inSquare = x > 25 && x < 75 && y > 25 && y < 75;
            tex.SetPixel(x, y, inSquare ? Color.red : Color.white);
        }
        tex.Apply(false, false);
        return tex;
    }
}
