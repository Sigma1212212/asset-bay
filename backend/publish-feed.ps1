<#
  Publishes Asset Bay broadcast content to randomthingsthatarecool.dev.

      .\publish-feed.ps1 -Deploy                                   # first time: create the bucket, deploy the Worker
      .\publish-feed.ps1 -Upload C:\clips\intro.mp4, C:\clips\tour.mp4   # upload videos to R2 as videos/<name>
      .\publish-feed.ps1                                           # sign + publish feed.json (edit it first)

  feed.json lists the videos for the tablet, optional world screens, the announcement, and the tablet bundle.
  Paths in it are relative to /asset-bay/media/, e.g. "videos/intro.mp4".

  Needs: `npx wrangler login` once (opens a browser), and the feed key from tools\sign (feed-keygen).
  Only upload videos you made or have the rights to.
#>
param(
    [string[]] $Upload = @(),
    [string] $Feed = "",
    [switch] $Deploy
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$wrangler = Join-Path $root "node_modules\wrangler\bin\wrangler.js"
if (-not (Test-Path $wrangler)) { throw "Run 'npm install' in $root first." }
function Wrangler { & node $wrangler @args; if ($LASTEXITCODE -ne 0) { throw "wrangler $($args[0]) failed." } }

if ($Deploy) {
    Write-Host "Creating the R2 bucket (fine if it already exists)..." -ForegroundColor Cyan
    & node $wrangler r2 bucket create asset-bay-media 2>&1 | Out-Host
    Write-Host "Deploying the Worker..." -ForegroundColor Cyan
    Push-Location $root
    try { Wrangler deploy } finally { Pop-Location }
}

foreach ($file in $Upload) {
    if (-not (Test-Path $file)) { throw "Not found: $file" }
    $name = [IO.Path]::GetFileName($file).ToLowerInvariant() -replace '[^a-z0-9._-]', '-'
    $ext = [IO.Path]::GetExtension($name)
    $type = switch ($ext) { ".mp4" { "video/mp4" } ".webm" { "video/webm" } ".bundle" { "application/octet-stream" } default { "application/octet-stream" } }
    $folder = if ($ext -eq ".bundle") { "bundles" } else { "videos" }
    Write-Host "Uploading $name -> media/$folder/$name" -ForegroundColor Cyan
    Wrangler r2 object put "asset-bay-media/$folder/$name" --file $file --content-type $type --remote
    if ($ext -eq ".bundle") {
        $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Host "  sha256 $hash  (put this in feed.json -> tablet.sha256)" -ForegroundColor DarkGray
    }
}

if ($Upload.Count -gt 0 -and -not $PSBoundParameters.ContainsKey('Feed')) { return }

# ---- sign and publish the feed
if ($Feed -eq "") { $Feed = Join-Path $root "feed.json" }
if (-not (Test-Path $Feed)) { throw "No feed at $Feed. Copy feed.example.json to feed.json and edit it." }

$doc = Get-Content $Feed -Raw | ConvertFrom-Json
# A fresh, always-increasing serial: launchers refuse any feed older than one they've already seen.
$doc.serial = [int][Math]::Floor(((Get-Date).ToUniversalTime() - [DateTime]'1970-01-01').TotalSeconds)
$out = Join-Path $env:TEMP "asset-bay-feed.json"
[IO.File]::WriteAllText($out, ($doc | ConvertTo-Json -Depth 10))

$signer = Join-Path $root "..\tools\sign\bin\Release\net8.0\AssetBaySign.dll"
if (-not (Test-Path $signer)) { & dotnet build (Join-Path $root "..\tools\sign\AssetBaySign.csproj") -c Release -nologo -v q | Out-Null }
& dotnet $signer feed-sign $out
if ($LASTEXITCODE -ne 0) { throw "Signing the feed failed - is the feed key on this PC? (tools\sign feed-keygen)" }

Write-Host "Publishing feed (serial $($doc.serial))..." -ForegroundColor Cyan
Wrangler r2 object put "asset-bay-media/feed.json" --file $out --content-type "application/json" --remote
Wrangler r2 object put "asset-bay-media/feed.json.sig" --file "$out.sig" --content-type "text/plain" --remote
Write-Host "Live. Menus pick it up within 5 minutes (or instantly via Videos > Refresh library)." -ForegroundColor Green
