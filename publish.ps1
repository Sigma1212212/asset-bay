<#
  Publishes a new BundleMenu.dll to GitHub Releases. Every launcher picks it up on the next Inject.

  Usage (from this folder):
      .\publish.ps1 -Version 1.0.1
      .\publish.ps1 -Version 1.1.0 -Notes "New Cascade animation"
      .\publish.ps1 -Version 1.2.0-beta1 -Prerelease

  Needs: dotnet SDK, GitHub CLI (`gh auth login` once), Gorilla Tag installed (the DLL compiles against it).
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

if ($Notes -eq "") { $Notes = "BundleMenu $Version" }
$releaseArgs = @("release", "create", $tag, $dll, $shaFile, "--repo", $Repo, "--title", "BundleMenu $Version", "--notes", $Notes)
if ($Prerelease) { $releaseArgs += "--prerelease" }

Write-Host "Creating release $tag on $Repo..." -ForegroundColor Cyan
& gh @releaseArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create failed (is `gh auth login` done, and does the tag already exist?)." }

Write-Host "Published $tag. Launchers will offer it on their next check." -ForegroundColor Green
