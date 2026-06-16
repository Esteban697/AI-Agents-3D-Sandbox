using UnityEngine;
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
        [Header("Camera Frame Getter")]
        [SerializeField] CameraFrameGetter cameraFrameGetter;
        
        [Header("Move Sequencer")]
        [SerializeField] SequentialMoveTest sequentialMoveTest;
        
        [Header("Image to show what image is uploaded with prompt")]
        [SerializeField] private Image imageToShow;
        
        [Header("Models")]
        [SerializeField]
        private string demoModel = "llava";
        
        public int targetWidth = 480;
        public int targetHeight = 480;
        
        public bool isStreaming;
        
        private Queue<string> _buffer;
        
        private Texture2D _capturedTexture;
        
        private Action<string> _callback;
        private Event _eventToReadKeyboard;

        private string _responseString = "";

        private bool _loopingEnabled = false;
        private bool _isLooping = false;
        private bool _started = false;
        
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
            _callback?.Invoke(_responseString);
        }
        
        void OnEnable() { Ollama.OnStreamFinished += StreamFinished; }
        void OnDisable() { Ollama.OnStreamFinished -= StreamFinished; }

        void Start()
        {
            _buffer = new Queue<string>();
            Ollama.InitChat();
        }
        
        void OnGUI() 
        {
            _eventToReadKeyboard = Event.current;
        }

        private void Update()
        {
            if (_loopingEnabled)
            {
                if (!_isLooping)
                {
                    _isLooping = true;
                    _responseString = "";
                    OnSubmit(ActionsPrompt, CallWhenActionsReceived);
                }
            }
            
            if (_eventToReadKeyboard == null) return;
            // Capture key press "M" to start moving
            if (_eventToReadKeyboard.keyCode == KeyCode.M)
            {
                if (!_started)
                {
                    _loopingEnabled = true;
                    _started = true;
                }
            }
            // Capture key press "S" to stop
            if (_eventToReadKeyboard.keyCode == KeyCode.S)
            {
                if (_started)
                    _loopingEnabled = false;
            }
        }

        void LateUpdate()
        {
            if (!isStreaming)
                return;

            // The following formatting is based on gemma3:4b
            while (_buffer.TryDequeue(out string text))
            {
                text = text.Replace("\n\n", "\n");
                _responseString += text;
            }
        }
        
        public void OnSubmit(string input, Action<string> responseCallback)
        {
            _callback = responseCallback;
            if (isStreaming)
                return;
            // Start stream to send message to model
            isStreaming = true;
            
            // Capture frame
            StartCoroutine(cameraFrameGetter.GetTextureFromCamera(targetWidth, targetHeight, (txt2D) =>
            {
                // Copy this texture into memory before using it
                cameraFrameGetter.CopyCapturedTexture(txt2D, out _capturedTexture, true);
                SendTextAndImage(_capturedTexture, input, ActionsSchema);
            }));
        }
        
        private async void SendTextAndImage(Texture2D texture, string input, string format)
        {
            // Show image for ease of use
            imageToShow.sprite = Sprite.Create(texture, new Rect(0, 0, targetWidth, targetHeight), Vector2.zero);
            Debug.Log($"Handing over a texture of size w:{texture.width} h: {texture.height}. With format: {texture.format}");

            // The new call ChatStream that includes the format
            await Ollama.ChatStreamNew((string text) =>
                _buffer.Enqueue(text), demoModel, input, format, 300, texture);

            isStreaming = false;
        }

        private void CallWhenActionsReceived(string actionsJson)
        {
            Debug.Log(actionsJson);
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

            sequentialMoveTest.RunStringArray(actions, CallWhenMovingFinished);
        }

        private void CallWhenMovingFinished()
        {
            _isLooping = false;
        }
    }
}
