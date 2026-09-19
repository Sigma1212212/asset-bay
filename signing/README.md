# Release signing

Every `BundleMenu.dll` release is signed. The launcher refuses to inject any DLL whose signature doesn't
check out against the public key it's built with. This applies to downloaded, cached and built-in copies alike.

## Why a SHA-256 checksum wasn't enough

The `.sha256` file sits in the same GitHub release as the DLL. Anyone who can upload a DLL (for example
someone who got into the GitHub account) can upload a matching checksum too. A signature can only be made
with the **private key**, which never leaves the publisher's PC. Taking over the GitHub account isn't enough
to push a malicious update.

## Files

| What | Where | Secret? |
|---|---|---|
| Private key | `%APPDATA%\AssetBay\signing\release-key.pem` (or the path in `ASSETBAY_SIGNING_KEY`) | **Yes. Never commit or share it.** |
| Public key | `signing/release-public-key.pem`, also compiled into the launcher (`src/ReleaseSignature.cs`) | No |
| Signing tool | `tools/sign` (`AssetBaySign keygen / sign / verify`) | No |

`publish.ps1` signs every build automatically, checks the signature against the public key, and uploads
`BundleMenu.dll.sig` next to the DLL.

## Back up the private key

**If you lose it, launchers already out there will reject every new release.** You'd have to publish a new
launcher with a new key, and everyone would have to download it by hand. Copy `release-key.pem` somewhere
safe and offline, such as a USB stick or a password manager's file attachment.

To publish from another PC, put the key file on it and point `ASSETBAY_SIGNING_KEY` at it.

## If the key leaks

1. Generate a new key: move the old file away, then run `AssetBaySign keygen`.
2. Put the new public key in `signing/release-public-key.pem` and `Injector/src/ReleaseSignature.cs`.
3. Release a new launcher and tell people to update. Old launchers will keep trusting the old key.
