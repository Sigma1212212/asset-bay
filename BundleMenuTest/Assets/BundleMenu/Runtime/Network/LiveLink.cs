using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BundleMenu
{
    /// <summary>
    /// One WebSocket to the Asset Bay room service. Connects, receives and pings on background threads;
    /// incoming messages wait in <see cref="Inbox"/> for the main thread, so nothing here touches Unity.
    /// If it can't connect or the connection drops, <see cref="Dead"/> becomes true and the owner falls
    /// back to plain polling (and tries live again later).
    /// </summary>
    public sealed class LiveLink : IDisposable
    {
        public readonly ConcurrentQueue<string> Inbox = new ConcurrentQueue<string>();
        /// <summary>Messages already taken off the queue (the self-test looks back through these).</summary>
        public readonly System.Collections.Generic.List<string> Seen = new System.Collections.Generic.List<string>();
        public bool Connected => connected && !disposed;
        public bool Dead { get; private set; }
        public string Error { get; private set; }

        private readonly Uri uri;
        private readonly ClientWebSocket ws = new ClientWebSocket();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private volatile bool connected, disposed;

        public LiveLink(Uri uri)
        {
            this.uri = uri;
            ws.Options.KeepAliveInterval = TimeSpan.Zero; // we send our own "ping" text, answered without waking the room
            Task.Run(RunAsync);
        }

        private async Task RunAsync()
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    await ws.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
                }
                connected = true;
                _ = PingLoopAsync();

                var buffer = new byte[8192];
                using (var message = new MemoryStream())
                {
                    while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        message.Write(buffer, 0, result.Count);
                        if (message.Length > 64 * 1024) throw new InvalidDataException("message too large");
                        if (!result.EndOfMessage) continue;
                        string text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                        message.SetLength(0);
                        if (text != "pong") Inbox.Enqueue(text);
                    }
                }
            }
            catch (Exception e)
            {
                if (!disposed) Error = e.Message;
            }
            finally
            {
                connected = false;
                Dead = true;
            }
        }

        private async Task PingLoopAsync()
        {
            try
            {
                while (!cts.IsCancellationRequested && connected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cts.Token).ConfigureAwait(false);
                    await SendAsync("ping").ConfigureAwait(false);
                }
            }
            catch { /* cancelled */ }
        }

        /// <summary>Queue a text message (fire and forget; dropped if the link is down).</summary>
        public void Send(string text)
        {
            if (Connected) _ = SendAsync(text);
        }

        private async Task SendAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await sendLock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e) { Error = e.Message; }
            finally { sendLock.Release(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            connected = false;
            try
            {
                if (ws.State == WebSocketState.Open)
                    _ = ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { /* already gone */ }
            // Give the close a moment to go out, then tear everything down.
            Task.Delay(500).ContinueWith(_ => { try { cts.Cancel(); ws.Abort(); ws.Dispose(); } catch { } });
        }
    }
}
