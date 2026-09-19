using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BundleMenu

  
{
    /// <summary>
    /// Minimal async glue for Unity 2021/2022, which has no built-in Awaitable.
    /// Everything completes on the main thread from a hidden runner's Update.
    /// </summary>
    public static class UnityAsync
    {
        private static AsyncRunner runner;

        private static AsyncRunner Runner
        {
            get
            {
                if (runner != null) return runner;
                var go = new GameObject("[BundleMenu Async]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<AsyncRunner>();
                return runner;
            }
        }

        /// <summary>Completes on the next frame's Update.</summary>
        public static Task NextFrame()
        {
            var tcs = new TaskCompletionSource<bool>();
            Runner.Frame.Add(tcs);
            return tcs.Task;
        }

        /// <summary>Unscaled-time delay (keeps working while the game is paused).</summary>
        public static Task Delay(float seconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            Runner.Timers.Add((Time.unscaledTime + Mathf.Max(0f, seconds), tcs));
            return tcs.Task;
        }

        /// <summary>Await any AsyncOperation, optionally reporting its progress every frame.</summary>
        public static async Task<T> Await<T>(this T op, IProgress<float> progress = null) where T : AsyncOperation
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            while (!op.isDone)
            {
                progress?.Report(op.progress);
                await NextFrame();
            }
            progress?.Report(1f);
            return op;
        }

        /// <summary>Fire-and-forget that still logs exceptions instead of swallowing them.</summary>
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        private sealed class AsyncRunner : MonoBehaviour
        {
            public readonly List<TaskCompletionSource<bool>> Frame = new List<TaskCompletionSource<bool>>();
            public readonly List<(float due, TaskCompletionSource<bool> tcs)> Timers =
                new List<(float, TaskCompletionSource<bool>)>();

            private readonly List<TaskCompletionSource<bool>> scratch = new List<TaskCompletionSource<bool>>();

            private void Update()
            {
                // Copy first: completing a task runs its continuation inline, which may queue more work.
                scratch.Clear();
                scratch.AddRange(Frame);
                Frame.Clear();

                float now = Time.unscaledTime;
                for (int i = Timers.Count - 1; i >= 0; i--)
                {
                    if (Timers[i].due > now) continue;
                    scratch.Add(Timers[i].tcs);
                    Timers.RemoveAt(i);
                }

                foreach (var tcs in scratch) tcs.TrySetResult(true);
            }

            private void OnDestroy()
            {
                foreach (var tcs in Frame) tcs.TrySetCanceled();
                foreach (var t in Timers) t.tcs.TrySetCanceled();
                Frame.Clear();
                Timers.Clear();
                if (runner == this) runner = null;
            }
        }
    }
}
