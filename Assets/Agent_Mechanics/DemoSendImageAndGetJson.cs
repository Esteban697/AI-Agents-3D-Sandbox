using UnityEngine;
using System.Collections.Generic;
using ollama;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;


namespace Agent_Mechanics
{
    /// <summary>
    /// Orchestrator that handles Chat with Ollama to send an image captured from Camera and get a JSON with the
    /// resulting movements to take to the character via SequentialMoveTest to handle the actions queue
    /// </summary>
    public class DemoSendImageAndGetJson : MonoBehaviour
    {
        [Header("Camera Frame Getter")] [SerializeField]
        CameraFrameGetter cameraFrameGetter;

        [Header("Move Sequencer")] [SerializeField]
        SequentialMover sequentialMover;

        [Header("Image to show what image is uploaded with prompt")] [SerializeField]
        private Image imageToShow;

        [Header("Models")] [SerializeField] private string demoModel = "llava";

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
                      ""rotateLeft"",
                      ""rotateRight"",
                      ""jump"",
                    ]
                  }
                },
                ""explanation"": {
                    ""type"": ""string"" }
              },
              ""required"": [""actions""],
              ""additionalProperties"": false
            }";

        private string SystemPrompt = string.Format(
            "You are the expert player that controls the character navigating a 3D space.\n" +
            "Your goal is to find the red sphere in the maze by moving the character forward, rotating and jumping.\n" +
            "Use only the allowed actions from the schema.\n" +
            "Include a single sentence explanation inside the schema.\n" +
            "Do not include markdown, comments, or explanations outside JSON.\n\n" +
            "Schema:\n{0}",
            ActionsSchema);

        private static readonly string UserPrompt =
            "Analyze the image and based on the 3D space in front of the character decide the best sequence of actions.\n" +
            "Rotating will be 45 degrees on the selected side: left or right.\n" +
            "If the character is one step away from an obstacle you should rotate to continue forward in another direction.\n" +
            "Return only valid JSON.\n";


        private void StreamFinished()
        {
            isStreaming = false;
            _callback?.Invoke(_responseString);
        }

        void OnEnable()
        {
            Ollama.OnStreamFinished += StreamFinished;
        }

        void OnDisable()
        {
            Ollama.OnStreamFinished -= StreamFinished;
        }

        void Start()
        {
            _buffer = new Queue<string>();
            Ollama.InitChat();
            Ollama.SetSystemPrompt(SystemPrompt);
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
                    OnSubmit(UserPrompt, CallWhenActionsReceived);
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
            Debug.Log(
                $"Handing over a texture of size w:{texture.width} h: {texture.height}. With format: {texture.format}");

            // The new call ChatStream that includes the format
            await Ollama.ChatStreamNew((string text) =>
                _buffer.Enqueue(text), demoModel, input, format, 300, texture);

            isStreaming = false;
        }

        private void CallWhenActionsReceived(string actionsJson)
        {
            Debug.Log($"Received:{actionsJson}");
            actionsJson = Regex.Replace(actionsJson, @"```(?:\w+)?\s*", "", RegexOptions.Multiline);
            actionsJson = Regex.Replace(actionsJson, @"```\s*", "", RegexOptions.Multiline);
            // Find where the JSON object starts and ends
            int startIndex = actionsJson.IndexOf('{');
            int endIndex = actionsJson.LastIndexOf('}');

            if (startIndex == -1 || endIndex == -1 || endIndex < startIndex)
            {
                Debug.Log("ERROR: No valid JSON object found!");
                Debug.Log($"Full input: {actionsJson}");
                // Retry
                CallWhenMovingFinished();
            }

            // Extract ONLY the JSON portion, stripping everything before/after
            actionsJson = actionsJson.Substring(startIndex, endIndex - startIndex + 1);
            actionsJson = actionsJson.Trim();

            Debug.Log($"Cleaned JSON: {actionsJson}");
            JObject obj = JObject.Parse(actionsJson);
            JArray actionsArray = (JArray)obj["actions"];

            // Convert to string[] - use Formatting.None to avoid extra whitespace
            string[] actions = actionsArray
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            
            // Depending on if actions where received we retry or process them
            if (actions.Length == 0)
            {
                Debug.LogWarning("No actions in found in the last response. Retrying...");
                CallWhenMovingFinished();
            }
            else
            {
                // Look for explanation
                JArray explainJArray = (JArray)obj["explanation"];
                if (explainJArray != null)
                {
                    string explanationInResponse = explainJArray.ToString();
                    Debug.Log($"Experience for memory:{explanationInResponse}");
                    Ollama.AddAssistantMessage(explanationInResponse);
                }
                sequentialMover.RunStringArray(actions, CallWhenMovingFinished);  
            }
        }

        private void CallWhenMovingFinished()
        {
            _isLooping = false;
        }
    }
}