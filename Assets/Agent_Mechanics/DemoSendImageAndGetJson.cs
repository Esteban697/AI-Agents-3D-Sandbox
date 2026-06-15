using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ollama;
using System;
using System.Linq;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

namespace Agent_Mechanics
{
    /// <summary>
    /// Orchestrator that handles Chat with Ollama to send an image captured from Camera and get a JSON with the
    /// resulting movements to take to the character via SequentialMoveTest to handle the actions queue
    /// </summary>
    public class DemoSendImageAndGetJson : MonoBehaviour
    {
        [Header("Move Sequencer")]
        [SerializeField] SequentialMoveTest sequentialMoveTest;
        
        [Header("Image to show what image is uploaded with prompt")]
        [SerializeField] private Image imageToShow;
        
        [Header("Models")]
        [SerializeField]
        private string demoModel = "llava";
        
        [Header("Camera to capture image")]
        [SerializeField]
        private Camera sourceCamera;
        
        private Queue<string> buffer;
        public bool isStreaming;
        
        private Texture2D capturedTexture;
        private int targetWidth = 480;
        private int targetHeight = 480;
        
        private Action<string> callback;
        private bool captureTriggered = false;
        private Event eventToReadKeyboard;

        private string responseString = "";

        private const string PromptContent =
            "Analyze the image and choose a sequence of actions.\n\nReturn only valid JSON matching this schema.\nRules:\n" +
            "- Use only the allowed action types.\n- Output 1 to 4 actions.\n- Order actions from first to last.\n" +
            "- Do not include markdown or explanations outside JSON.\n" +
            "- If the image is unclear, return {\"actions\":[{\"type\":\"jump\"}]}.";
        
        
        
        private const string ActionsSchema = @"{
              ""type"": ""object"",
              ""properties"": {
                ""actions"": {
                  ""type"": ""array"",
                  ""minItems"": 1,
                  ""maxItems"": 4,
                  ""items"": {
                    ""type"": ""string"",
                    ""enum"": [
                      ""moveForward"",
                      ""moveBackward"",
                      ""rotateLeft"",
                      ""rotateRight"",
                      ""jump"",
                    ]
                  }
                }
              },
              ""required"": [""actions""],
              ""additionalProperties"": false
            }";
        
        private static readonly string ActionsPrompt = string.Format(
            "Analyze the image and decide the best sequence of actions.\n\n" +
            "Return only valid JSON.\n" +
            "Use only the allowed actions from the schema.\n" +
            "Do not include markdown, comments, or explanations outside JSON.\n\n" +
            "Schema:\n{0}",
            ActionsSchema
        );

        private void StreamFinished()
        {
            isStreaming = false;
            callback?.Invoke(responseString);
        }
        
        void OnEnable() { Ollama.OnStreamFinished += StreamFinished; }
        void OnDisable() { Ollama.OnStreamFinished -= StreamFinished; }

        void Start()
        {
            buffer = new Queue<string>();
            Ollama.InitChat();
        }
        
        void OnGUI() 
        {
            eventToReadKeyboard = Event.current;
        }

        private void Update()
        {
            if (eventToReadKeyboard == null) return;
            // Capture key press "M" and trigger the camera capture 
            if (eventToReadKeyboard.keyCode== KeyCode.M)
            {
                if (!captureTriggered)
                {
                    captureTriggered = true;
                    responseString = "";
                    OnSubmit(ActionsPrompt, CallWhenActionsReceived);
                }
            }
        }

        void LateUpdate()
        {
            if (!isStreaming)
                return;

            // The following formatting is based on gemma3:4b
            while (buffer.TryDequeue(out string text))
            {
                text = text.Replace("\n\n", "\n");
                responseString += text;
            }
        }
        
        public void OnSubmit(string input, Action<string> responseCallback)
        {
            callback = responseCallback;
            if (isStreaming)
                return;
            // Start stream to send message to model
            isStreaming = true;
            
            // Capture frame
            StartCoroutine(GetTextureFromCamera(sourceCamera, (txt2D) =>
            {
                // Copy this texture into memory before using it
                CopyCapturedTexture(txt2D, out capturedTexture, true);
                SendTextAndImage(capturedTexture, input, ActionsSchema);
            }));
        }
        
        private async void SendTextAndImage(Texture2D texture, string input, string format)
        {
            // Show image for ease of use
            imageToShow.sprite = Sprite.Create(texture, new Rect(0, 0, targetWidth, targetHeight), Vector2.zero);
            Debug.Log($"Handing over a texture of size w:{texture.width} h: {texture.height}. With format: {texture.format}");
            
            // The new call ChatStream that includes the format
            await Ollama.ChatStreamNew((string text) => 
                buffer.Enqueue(text), demoModel, input, format, 300, texture);
            
            isStreaming = false;
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

        private void CallWhenActionsReceived(string actionsJson)
        {
            // Parse and get the "actions" array
            actionsJson = actionsJson.Replace("```json", "");
            actionsJson = actionsJson.Replace("```", "");
            Debug.Log($"Actions received: {actionsJson}");
            JObject obj = JObject.Parse(actionsJson);
            JArray actionsArray = (JArray)obj["actions"];

            // Convert to string[]
            string[] actions = actionsArray
                .Select(x => x.ToString())
                .ToArray();

            sequentialMoveTest.RunStringArray(actions);
            captureTriggered = false; // For TESTING: Better to wait for the end of movements
        }
    }
}
