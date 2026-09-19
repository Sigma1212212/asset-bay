using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    /// <summary>Every menu setting that follows you between PCs. Plain fields so JsonUtility can do it.</summary>
    [Serializable]
    public sealed class SyncedSettings
    {
        public long savedAt;          // unix ms, newest copy wins
        public int theme, style, entrance, stagger, placement, guiview, source;
        public float speed, guiscale, guix, guiy, failrate;
        public bool screens, sharing, unloadall;
    }

    /// <summary>
    /// Keeps settings on the Asset Bay server under a random sync code. On start it downloads (newer copy
    /// wins); after every change it uploads a few seconds later. On another PC, enter the same code in
    /// the safe-mode window (H) to pull everything over. Only a hash of the code is ever sent.
    /// </summary>
    public sealed class SettingsSync : MonoBehaviour
    {
        private const string CodeKey = "BundleMenu.synccode";
        private const float UploadDelay = 4f;

        public string Endpoint;                      // .../asset-bay/settings
        public Func<SyncedSettings> Export;
        public Action<SyncedSettings> Import;

        public string Code { get; private set; }
        public string Status { get; private set; } = "not synced yet";
        public bool Busy { get; private set; }

        private float uploadAt = -1f;

        private void Start()
        {
            Code = PlayerPrefs.GetString(CodeKey, "");
            if (string.IsNullOrEmpty(Code)) { Code = NewCode(); PlayerPrefs.SetString(CodeKey, Code); PlayerPrefs.Save(); }
            Download(applyOnlyIfNewer: true).Forget();
        }

        /// <summary>Call after settings change; uploads once things settle.</summary>
        public void MarkDirty() => uploadAt = Time.unscaledTime + UploadDelay;

        private void Update()
        {
            if (uploadAt < 0f || Busy || Time.unscaledTime < uploadAt) return;
            uploadAt = -1f;
            Upload().Forget();
        }

        /// <summary>Switch to a code from another PC and take its settings.</summary>
        public async System.Threading.Tasks.Task<bool> Link(string code)
        {
            code = Normalize(code);
            if (code.Length != 12) { Status = "codes are 12 letters/numbers"; return false; }
            string old = Code;
            Code = code;
            bool ok = await Download(applyOnlyIfNewer: false);
            if (!ok) { Code = old; return false; }
            PlayerPrefs.SetString(CodeKey, Code);
            PlayerPrefs.Save();
            return true;
        }

        public async System.Threading.Tasks.Task Upload()
        {
            if (Export == null || string.IsNullOrEmpty(Endpoint)) return;
            Busy = true;
            try
            {
                string json = $"{{\"id\":\"{Id}\",\"settings\":{JsonUtility.ToJson(Export())}}}";
                var (status, _) = await Send("PUT", Endpoint, json);
                Status = status == 200 ? $"saved to server {DateTime.Now:HH:mm}"
                       : status == 429 ? "saving too fast, will retry" : $"save failed ({status})";
                if (status == 429) MarkDirty();
            }
            catch (Exception e) { Status = "offline: " + e.Message; }
            finally { Busy = false; }
        }

        /// <summary>Fetch and apply. Returns false if there was nothing to fetch or it failed.</summary>
        public async System.Threading.Tasks.Task<bool> Download(bool applyOnlyIfNewer)
        {
            if (Import == null || string.IsNullOrEmpty(Endpoint)) return false;
            Busy = true;
            try
            {
                var (status, text) = await Send("GET", $"{Endpoint}?id={Id}", null);
                if (status == 404) { Status = "no saved settings for this code yet"; if (applyOnlyIfNewer) MarkDirty(); return false; }
                if (status != 200) { Status = $"load failed ({status})"; return false; }

                var reply = JsonUtility.FromJson<Reply>(text);
                var local = Export?.Invoke();
                if (reply?.settings == null) { Status = "server copy unreadable"; return false; }
                if (applyOnlyIfNewer && local != null && local.savedAt >= reply.settings.savedAt)
                {
                    Status = "this PC is up to date";
                    if (local.savedAt > reply.settings.savedAt) MarkDirty(); // ours is newer: push it
                    return true;
                }
                Import(reply.settings);
                Status = $"loaded from server {DateTime.Now:HH:mm}";
                return true;
            }
            catch (Exception e) { Status = "offline: " + e.Message; return false; }
            finally { Busy = false; }
        }

        [Serializable] private sealed class Reply { public SyncedSettings settings; public long updated; }

        private string Id => Hash("assetbay-settings:" + Code);

        /// <summary>12 characters without look-alikes (no 0/O, 1/I/L), shown as XXXX-XXXX-XXXX.</summary>
        private static string NewCode()
        {
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var bytes = new byte[12];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder(12);
            foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
            return sb.ToString();
        }

        public static string Pretty(string code) =>
            code == null || code.Length != 12 ? code : $"{code.Substring(0, 4)}-{code.Substring(4, 4)}-{code.Substring(8, 4)}";

        private static string Normalize(string code)
        {
            var sb = new StringBuilder();
            foreach (char c in (code ?? "").ToUpperInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static async System.Threading.Tasks.Task<(long, string)> Send(string method, string url, string json)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 10;
                await req.SendWebRequest().Await();
                if (req.result == UnityWebRequest.Result.ConnectionError) throw new InvalidOperationException(req.error);
                return (req.responseCode, req.downloadHandler.text);
            }
        }

        private static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder(64);
                foreach (var b in sha.ComputeHash(Encoding.UTF8.GetBytes(s))) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
