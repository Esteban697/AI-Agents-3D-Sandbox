using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Agent_Mechanics
{
    [Serializable]
    public class ActionSequence
    {
        public List<string> actions;
    }
    
    public class SequentialMoveTest : MonoBehaviour
    {
        [SerializeField] private StarterAssetsCodeMover starterAssetsCodeMover;
        [SerializeField] private CoroutineQueue coroutineQueue;
        
        private Dictionary<string, Func<IEnumerator>> actionMap;
        
        private string jsonExample = 
            @"{""actions"": 
                [""moveForward"", 
                ""rotateRight"", 
                ""moveForward"", 
                ""rotateLeft"", 
                ""jump""]}";

        private void Awake()
        {
            actionMap = new Dictionary<string, Func<IEnumerator>>(StringComparer.OrdinalIgnoreCase)
            {
                ["moveForward"] = OneStepForward,
                ["moveBackward"] = OneStepBackward,
                ["rotateRight"] = OneRotateRight,
                ["rotateLeft"] = OnRotateLeft,
                ["jump"] = OneJump,
            };
        }
        
        // Take the action sequence form the JSON Example and running the sequentially
        void Start()
        {
            // TO TEST: This runs the example sequence but the whole logic is meant to listen to model responses
            // RunJson(jsonExample);
        }

        public void RunStringArray(string[] array, Action callWhenFinished)
        {
            foreach (var actionName in array)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                if (actionMap.TryGetValue(actionName, out var coroutineFactory))
                {
                    coroutineQueue.Enqueue(coroutineFactory());
                }
                else
                {
                    Debug.LogWarning($"Unknown action: {actionName}");
                }
            }

            coroutineQueue.DefineActionWhenQueueIsEmpty(callWhenFinished);
        }
        
        public void RunJson(string json)
        {
            var sequence = JsonUtility.FromJson<ActionSequence>(json);

            if (sequence?.actions == null || sequence.actions.Count == 0)
            {
                Debug.LogWarning("No actions found in JSON.");
                return;
            }

            foreach (var actionName in sequence.actions)
            {
                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                if (actionMap.TryGetValue(actionName, out var coroutineFactory))
                {
                    coroutineQueue.Enqueue(coroutineFactory());
                }
                else
                {
                    Debug.LogWarning($"Unknown action: {actionName}");
                }
            }
        }

        private IEnumerator OneJump()
        {
            starterAssetsCodeMover.Jump();
            yield break;
        }

        private IEnumerator OneStepForward()
        {
            starterAssetsCodeMover.MoveForward();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopForwardBack();
        }

        private IEnumerator OneStepBackward()
        {
            starterAssetsCodeMover.MoveBackward();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopForwardBack();
        }

        private IEnumerator OneStepLeft()
        {
            starterAssetsCodeMover.MoveLeft();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopLeftRight();
        }

        private IEnumerator OneStepRight()
        {
            starterAssetsCodeMover.MoveRight();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopLeftRight();
        }

        private IEnumerator OneRotateRight()
        {
            starterAssetsCodeMover.MoveForwardRight();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopAll();
        }

        private IEnumerator OnRotateLeft()
        {
            starterAssetsCodeMover.MoveForwardLeft();
            yield return new WaitForSeconds(1.0f);
            starterAssetsCodeMover.StopAll();
        }
    }
}
