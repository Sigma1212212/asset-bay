using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    /// <summary>
    /// The Spotify sign-in shared with the launcher. You sign in there (it can open a browser); it writes
    /// %APPDATA%\AssetBay\spotify.json, and the menu reads it here and keeps the access token fresh.
    /// Only your own tokens are touched, they never leave your PC, and nothing is sent to the Asset Bay
    /// server. Signing in uses PKCE, so there's no app secret anywhere.
    /// </summary>
    public static class SpotifyAuth
    {
        [Serializable]
        public sealed class Tokens
        {
            public string clientId;
            public string refreshToken;
            public string accessToken;
            public long expiresAt;      // unix seconds
            public string account;      // display name, just for showing
        }

        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AssetBay", "spotify.json");

        public static Tokens Current { get; private set; }
        public static string LastError { get; private set; }
        public static bool SignedIn => Current != null && !string.IsNullOrEmpty(Current.refreshToken);

        private static DateTime lastRead;

        /// <summary>Re-read the file (cheap; only touches the disk once a second at most).</summary>
        public static void Reload(bool force = false)
        {
            if (!force && (DateTime.UtcNow - lastRead).TotalSeconds < 1) return;
            lastRead = DateTime.UtcNow;
            try
            {
                Current = File.Exists(FilePath) ? JsonUtility.FromJson<Tokens>(File.ReadAllText(FilePath)) : null;
                LastError = Current == null ? "not signed in" : null;
            }
            catch (Exception e)
            {
                Current = null;
                LastError = e.Message;
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e) { LastError = e.Message; }
        }

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private static Task<string> refreshing;

        /// <summary>A usable access token, refreshing it first if it's about to run out. Null if signed out.</summary>
        public static async Task<string> TokenAsync()
        {
            Reload();
            if (!SignedIn) return null;
            if (!string.IsNullOrEmpty(Current.accessToken) && Current.expiresAt - 30 > Now) return Current.accessToken;
            // One refresh at a time, however many callers ask.
            return await (refreshing ??= RefreshAsync());
        }

        private static async Task<string> RefreshAsync()
        {
            try
            {
                var form = new List<IMultipartFormSection>
                {
                    new MultipartFormDataSection("grant_type", "refresh_token"),
                    new MultipartFormDataSection("refresh_token", Current.refreshToken),
                    new MultipartFormDataSection("client_id", Current.clientId),
                };
                using (var request = UnityWebRequest.Post("https://accounts.spotify.com/api/token", form))
                {
                    request.timeout = 10;
                    await request.SendWebRequest().Await();
                    if (request.responseCode != 200)
                    {
                        LastError = $"sign-in expired ({request.responseCode}) - sign in again in the launcher";
                        return null;
                    }
                    var reply = JsonUtility.FromJson<TokenReply>(request.downloadHandler.text);
                    Current.accessToken = reply.access_token;
                    Current.expiresAt = Now + Math.Max(60, reply.expires_in);
                    if (!string.IsNullOrEmpty(reply.refresh_token)) Current.refreshToken = reply.refresh_token;
                    Save();
                    LastError = null;
                    return Current.accessToken;
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return null;
            }
            finally { refreshing = null; }
        }

        [Serializable] private sealed class TokenReply
        {
            public string access_token, refresh_token;
            public int expires_in;
        }

        /// <summary>Forget the tokens on this PC.</summary>
        public static void SignOut()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { /* nothing to do */ }
            Current = null;
            Reload(force: true);
        }
    }
}
