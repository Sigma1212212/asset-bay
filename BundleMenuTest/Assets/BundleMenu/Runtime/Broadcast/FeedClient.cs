using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    // ---- feed.json shape (see backend/feed.example.json). JsonUtility needs plain serializable classes.

    [Serializable]
    public sealed class FeedDoc
    {
        public int version;
        public int serial;
        public string announcement;
        public FeedScreen[] screens;
        public FeedVideo[] videos;
        public FeedBundle tablet;
    }

    [Serializable]
    public sealed class FeedScreen
    {
        public string id, title, video, scene;
        public float[] position;
        public float rotationY, width = 2f, volume = 0.5f;
        public bool loop = true;
    }

    [Serializable]
    public sealed class FeedVideo
    {
        public string id, title, video;
        public string length; // optional display text, e.g. "2:14"
    }

    [Serializable]
    public sealed class FeedBundle
    {
        public string path;   // e.g. "bundles/tablet.bundle"
        public string sha256; // checked after download; the feed itself is signed, so this is trusted
        public string prefab; // asset name inside the bundle, e.g. "Tablet"
    }

    /// <summary>
    /// Downloads the broadcast feed from the Asset Bay Worker and only accepts it if:
    ///  - its RSA-SHA256 signature matches the public key below (the private key never leaves the publisher's PC)
    ///  - its serial is not older than the newest one already seen (an old feed can't be replayed)
    ///  - every media path is a plain relative path on this backend (a feed can't send players to other sites)
    /// </summary>
    public sealed class FeedClient
    {
        public const string DefaultBaseUrl = "https://assetbay.randomthingsthatarecool.dev/asset-bay/";

        // RSA public key for the feed (signing/feed-public-key.xml).
        private const string Modulus =
            "tm/4p2JblPdnVN+Yk5N9T8N2c6TSKqxEdJcB2JJ/LwDKkXZ2RSYdfgjsg2yHL/dAHQDDcT2yvXW9AuytNaZ0xUPB+4Y4Qi9unz/LyVugHSF/lRjFXRHl/AbUTBZSq2ay5h3iYgqTLj6hA/xVBuz/KGK2uExbaX68pvO4ziGoYhVG7Ma23uVZBLaN0eTbYPKpMj3eXVyo8eFkZ36Ve2dC1aiyYy5FQiMC5p4VZC4u0iflmje5hwlssfgIIK1zjPcPEJBFGcH11GoKMiGrPvlbPqtMHdOjOx2vTnnO1ASkj2iRQq/9gzlc6Kg0gWK92sP9sd08IC87QAFS+RcAGZeN7Q==";
        private const string Exponent = "AQAB";
        private const string SerialPref = "BundleMenu.feedSerial";

        public string BaseUrl { get; }
        public FeedDoc Current { get; private set; }
        public string LastError { get; private set; }
        public DateTime? LastUpdated { get; private set; }

        public FeedClient(string baseUrl = null)
        {
            BaseUrl = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/') + "/";
        }

        public string MediaUrl(string relativePath) => BaseUrl + "media/" + relativePath;

        [Serializable] private sealed class Envelope { public string feed; public string sig; }

        /// <summary>Fetch and verify. Returns true when a new, valid feed was accepted.</summary>
        public async Task<bool> RefreshAsync()
        {
            try
            {
                string body = await GetText(BaseUrl + "feed");
                var env = JsonUtility.FromJson<Envelope>(body);
                if (env == null || string.IsNullOrEmpty(env.feed) || string.IsNullOrEmpty(env.sig))
                    throw new InvalidOperationException("The feed response was empty.");

                if (!Verify(Encoding.UTF8.GetBytes(env.feed), env.sig))
                    throw new InvalidOperationException("The feed's signature is invalid - ignored.");

                var doc = JsonUtility.FromJson<FeedDoc>(env.feed);
                if (doc == null || doc.version != 1) throw new InvalidOperationException("Unsupported feed version.");

                int newest = PlayerPrefs.GetInt(SerialPref, 0);
                if (doc.serial < newest) throw new InvalidOperationException("Received an older feed than before - ignored.");
                if (!PathsAreSafe(doc)) throw new InvalidOperationException("The feed references files outside the backend - ignored.");

                PlayerPrefs.SetInt(SerialPref, doc.serial);
                bool changed = Current == null || doc.serial != Current.serial;
                Current = doc;
                LastError = null;
                LastUpdated = DateTime.Now;
                return changed;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        public static bool Verify(byte[] data, string signatureBase64)
        {
            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters
                    {
                        Modulus = Convert.FromBase64String(Modulus),
                        Exponent = Convert.FromBase64String(Exponent),
                    });
                    return rsa.VerifyData(data, Convert.FromBase64String(signatureBase64.Trim()),
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool PathsAreSafe(FeedDoc doc)
        {
            bool Safe(string p) => string.IsNullOrEmpty(p) ||
                (!p.Contains("..") && !p.Contains(":") && !p.StartsWith("/") && !p.Contains("\\"));
            if (doc.screens != null) foreach (var s in doc.screens) if (!Safe(s.video)) return false;
            if (doc.videos != null) foreach (var v in doc.videos) if (!Safe(v.video)) return false;
            return doc.tablet == null || Safe(doc.tablet.path);
        }

        public static async Task<string> GetText(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                await req.SendWebRequest().Await();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"Couldn't reach the Asset Bay server ({req.error}).");
                return req.downloadHandler.text;
            }
        }

        public static async Task<byte[]> GetBytes(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 60;
                await req.SendWebRequest().Await();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"Download failed ({req.error}).");
                return req.downloadHandler.data;
            }
        }
    }
}
