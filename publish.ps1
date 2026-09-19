<#
  Publishes a new BundleMenu.dll to GitHub Releases. Every launcher picks it up on the next Inject.

  Usage (from this folder):
      .\publish.ps1 -Version 1.0.1
      .\publish.ps1 -Version 1.1.0 -Notes "New Cascade animation"
      .\publish.ps1 -Version 1.2.0-beta1 -Prerelease

  Needs: dotnet SDK, GitHub CLI (`gh auth login` once), Gorilla Tag installed (the DLL compiles against it),
  and the release signing key (see signing\README.md). Launchers refuse DLLs without a valid signature.
  The repository defaults to the one in Injector\launcher.json.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $Repo = "",
    [string] $Notes = "",
    [string] $GameDir = "",
    [switch] $Prerelease
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') {
    throw "Version must look like 1.2.3 or 1.2.3-beta1 (got '$Version')."
}
$tag = "v$Version"

if ($Repo -eq "") {
    $config = Get-Content (Join-Path $root "Injector\launcher.json") -Raw | ConvertFrom-Json
    $Repo = $config.repository
}
if ($Repo -like "YOUR-*" -or $Repo -notmatch '^[^/]+/[^/]+$') {
    throw "Set `"repository`" in Injector\launcher.json (or pass -Repo owner/name) first."
}

Write-Host "Building BundleMenu.dll $Version..." -ForegroundColor Cyan
$project = Join-Path $root "Injectable\BundleMenu.Injectable.csproj"
$assemblyVersion = ($Version -split '-')[0]
$buildArgs = @("build", $project, "-c", "Release", "-p:Version=$Version", "-p:AssemblyVersion=$assemblyVersion.0", "-nologo", "-v", "q")
if ($GameDir -ne "") { $buildArgs += "-p:GameDir=$GameDir" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dll = Join-Path $root "Injectable\bin\Release\BundleMenu.dll"
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
$shaFile = "$dll.sha256"
# "<hash>  <name>" - the same format sha256sum writes; the launcher reads the first word.
[IO.File]::WriteAllText($shaFile, "$hash  BundleMenu.dll`n")
Write-Host "SHA-256 $hash" -ForegroundColor DarkGray

# Sign with the private key from %APPDATA%\AssetBay\signing (never in the repo), then verify with the
# public key the launcher ships with, so a broken signature can never be published.
Write-Host "Signing..." -ForegroundColor Cyan
$signer = Join-Path $root "tools\sign\AssetBaySign.csproj"
& dotnet build $signer -c Release -nologo -v q | Out-Null
$signDll = Join-Path $root "tools\sign\bin\Release\net8.0\AssetBaySign.dll"
& dotnet $signDll sign $dll
if ($LASTEXITCODE -ne 0) { throw "Signing failed - is the signing key on this PC? See signing\README.md." }
& dotnet $signDll verify $dll (Join-Path $root "signing\release-public-key.pem")
if ($LASTEXITCODE -ne 0) { throw "Signature did not verify against signing\release-public-key.pem." }
$sigFile = "$dll.sig"

if ($Notes -eq "") { $Notes = "BundleMenu $Version" }
$releaseArgs = @("release", "create", $tag, $dll, $shaFile, $sigFile, "--repo", $Repo, "--title", "BundleMenu $Version", "--notes", $Notes)
if ($Prerelease) { $releaseArgs += "--prerelease" }

Write-Host "Creating release $tag on $Repo..." -ForegroundColor Cyan
& gh @releaseArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create failed (is `gh auth login` done, and does the tag already exist?)." }

Write-Host "Published $tag. Launchers will offer it on their next check." -ForegroundColor Green
