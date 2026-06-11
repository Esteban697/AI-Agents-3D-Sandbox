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
    public async void OnSubmit(string input)
    {
        if (isStreaming)
            return;

        llmOutput.text += $"<align=right>\"{input}\"</align>\n<align=left><line-height=60%>";
        isStreaming = true;
        userInput.interactable = false;

        bold = false;
        italic = false;
        
        // Capture frame
        StartCoroutine(GetTextureFromCamera(sourceCamera, (txt2D) => SendTextAndImage(txt2D, input)));
    }

    private async void SendTextAndImage(Texture2D texture, string input)
    {
        // Show image
        rotatingSquare.sprite = Sprite.Create(texture, new Rect(0, 0, targetWidth, targetHeight), Vector2.zero);
        Debug.Log($"Handing over a texture of size w:{texture.width} h: {texture.height}. With format: {texture.format}");
        // The call ChatStream
        await Ollama.ChatStream((string text) => 
            buffer.Enqueue(text), demoModel, input, 300, capturedTexture);

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
        screenShot.Apply();
        
        yield return new WaitForEndOfFrame();
        // Copy this texture into memory before using it
        capturedTexture = new Texture2D(screenShot.width, screenShot.height, screenShot.format, false);
    
        // Copy all pixels (main mip)
        capturedTexture.SetPixels(screenShot.GetPixels());
        capturedTexture.Apply();
        
        RenderTexture.active = prev;
        mCamera.targetTexture = null;
        renderTexture.Release();
        
        callback(screenShot);
    }
}
