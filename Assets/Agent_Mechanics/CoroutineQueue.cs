using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Agent_Mechanics
{
    /// <summary>
    /// Enables a sequential triggering of the defined coroutines to handle the third person character movements
    /// </summary>
    public class CoroutineQueue : MonoBehaviour
    {
        private readonly Queue<IEnumerator> queue = new();
        private Coroutine runner;
        private bool isRunning;

        public void Enqueue(IEnumerator routine)
        {
            queue.Enqueue(routine);

            if (!isRunning)
            {
                runner = StartCoroutine(RunQueue());
            }
        }

        private IEnumerator RunQueue()
        {
            isRunning = true;

            while (queue.Count > 0)
            {
                yield return StartCoroutine(queue.Dequeue());
            }

            isRunning = false;
            runner = null;
        }

        public void ClearQueue()
        {
            queue.Clear();
        }

        public void StopCurrentAndClear()
        {
            queue.Clear();

            if (runner != null)
            {
                StopCoroutine(runner);
                runner = null;
            }

            isRunning = false;
        }
    }
}