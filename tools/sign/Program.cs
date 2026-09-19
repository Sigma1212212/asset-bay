using System.Security.Cryptography;

// AssetBaySign - ECDSA P-256 / SHA-256 release signing.
//
//   keygen                 create the private key (once) and print the public key
//   sign <file>            write <file>.sig  (base64, IEEE P1363 r||s)
//   verify <file> [pubkey] check <file>.sig against the public key (default: signing/release-public-key.pem)
//
// The private key lives OUTSIDE every repository, at %APPDATA%\AssetBay\signing\release-key.pem
// (override with ASSETBAY_SIGNING_KEY). Anyone holding it can publish updates the launcher trusts,
// so back it up somewhere safe and never commit it.

string keyPath = Environment.GetEnvironmentVariable("ASSETBAY_SIGNING_KEY") is { Length: > 0 } env
    ? env
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AssetBay", "signing", "release-key.pem");

string feedKeyPath = Path.Combine(Path.GetDirectoryName(keyPath)!, "feed-key.pem");

if (args.Length == 0) return Usage();

try
{
    switch (args[0])
    {
        case "keygen":
        {
            if (File.Exists(keyPath))
            {
                Console.Error.WriteLine($"A key already exists at {keyPath}. Refusing to overwrite it.");
                Console.Error.WriteLine("Replacing it would make every existing launcher reject new releases.");
                return 2;
            }
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            File.WriteAllText(keyPath, ecdsa.ExportPkcs8PrivateKeyPem());
            Console.WriteLine($"Private key written to {keyPath}  (back it up; never commit it)");
            Console.WriteLine();
            Console.WriteLine(ecdsa.ExportSubjectPublicKeyInfoPem());
            return 0;
        }

        case "pubkey":
        {
            using var ecdsa = LoadPrivate();
            Console.WriteLine(ecdsa.ExportSubjectPublicKeyInfoPem());
            return 0;
        }

        case "sign" when args.Length >= 2:
        {
            using var ecdsa = LoadPrivate();
            byte[] data = File.ReadAllBytes(args[1]);
            byte[] sig = ecdsa.SignData(data, HashAlgorithmName.SHA256);
            File.WriteAllText(args[1] + ".sig", Convert.ToBase64String(sig) + "\n");

            // Sanity check with the public half before anything gets published.
            using var check = ECDsa.Create();
            check.ImportFromPem(ecdsa.ExportSubjectPublicKeyInfoPem());
            if (!check.VerifyData(data, sig, HashAlgorithmName.SHA256)) throw new CryptographicException("Self-check failed.");
            Console.WriteLine($"Signed {Path.GetFileName(args[1])} -> {Path.GetFileName(args[1])}.sig");
            return 0;
        }

        case "verify" when args.Length >= 2:
        {
            string pubPath = args.Length >= 3 ? args[2]
                : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "signing", "release-public-key.pem");
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(File.ReadAllText(pubPath));
            byte[] data = File.ReadAllBytes(args[1]);
            byte[] sig = Convert.FromBase64String(File.ReadAllText(args[1] + ".sig").Trim());
            bool ok = ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256);
            Console.WriteLine(ok ? "Signature OK" : "Signature INVALID");
            return ok ? 0 : 1;
        }

        // ---- feed signing (RSA-2048, PKCS#1 v1.5, SHA-256). The menu runs on the game's Mono runtime,
        //      whose RSA support is solid; it verifies with the XML public key printed by feed-keygen.
        case "feed-keygen":
        {
            if (File.Exists(feedKeyPath))
            {
                Console.Error.WriteLine($"A feed key already exists at {feedKeyPath}. Refusing to overwrite it.");
                return 2;
            }
            using var rsa = RSA.Create(2048);
            Directory.CreateDirectory(Path.GetDirectoryName(feedKeyPath)!);
            File.WriteAllText(feedKeyPath, rsa.ExportPkcs8PrivateKeyPem());
            Console.WriteLine($"Feed key written to {feedKeyPath}  (back it up; never commit it)");
            Console.WriteLine(rsa.ToXmlString(false));
            return 0;
        }

        case "feed-pubkey":
        {
            using var rsa = LoadFeedKey();
            Console.WriteLine(rsa.ToXmlString(false));
            return 0;
        }

        case "feed-sign" when args.Length >= 2:
        {
            using var rsa = LoadFeedKey();
            byte[] data = File.ReadAllBytes(args[1]);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new CryptographicException("Self-check failed.");
            File.WriteAllText(args[1] + ".sig", Convert.ToBase64String(sig));
            Console.WriteLine($"Signed {Path.GetFileName(args[1])} -> {Path.GetFileName(args[1])}.sig");
            return 0;
        }

        default:
            return Usage();
    }
}
catch (Exception e)
{
    Console.Error.WriteLine("Error: " + e.Message);
    return 1;
}

ECDsa LoadPrivate()
{
    if (!File.Exists(keyPath))
        throw new FileNotFoundException($"No signing key at {keyPath}. Run 'keygen' first (once).");
    var ecdsa = ECDsa.Create();
    ecdsa.ImportFromPem(File.ReadAllText(keyPath));
    return ecdsa;
}

RSA LoadFeedKey()
{
    if (!File.Exists(feedKeyPath))
        throw new FileNotFoundException($"No feed key at {feedKeyPath}. Run 'feed-keygen' first (once).");
    var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(feedKeyPath));
    return rsa;
}

static int Usage()
{
    Console.Error.WriteLine("usage: AssetBaySign keygen | pubkey | sign <file> | verify <file> [public-key.pem] | feed-keygen | feed-pubkey | feed-sign <file>");
    return 64;
}
