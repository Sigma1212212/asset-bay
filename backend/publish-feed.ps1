<#
  Publishes Asset Bay broadcast content to randomthingsthatarecool.dev.

      .\publish-feed.ps1 -Deploy                                   # first time: create the bucket, deploy the Worker
      .\publish-feed.ps1 -Upload C:\clips\intro.mp4, C:\clips\tour.mp4   # upload videos to R2 as videos/<name>
      .\publish-feed.ps1                                           # sign + publish feed.json (edit it first)

  feed.json lists the videos for the tablet, optional world screens, the announcement, and the tablet bundle.
  Paths in it are relative to /asset-bay/media/, e.g. "videos/intro.mp4".

  Needs: `npx wrangler login` once (opens a browser), and the feed key from tools\sign (feed-keygen).
  Only upload videos you made or have the rights to.
  Uploads are refused if they'd take storage past 9 GB (the free tier is 10 GB).
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

# ---- 9 GB guard: R2's free tier is 10 GB of storage. Uploads are refused if they would take the
#      bucket past 9 GB, so storage can never reach the paid tier. If the size can't be read, nothing is
#      uploaded (fail safe). Note: Cloudflare's bucket metrics can lag a few minutes behind recent uploads.
$LimitBytes = 9000000000

function Get-BucketBytes {
    $json = & node $wrangler r2 bucket info asset-bay-media --json 2>$null | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Couldn't read the bucket size from Cloudflare, so nothing was uploaded (9 GB guard)." }
    $start = $json.IndexOf("{")
    if ($start -lt 0) { throw "Unexpected answer from Cloudflare, so nothing was uploaded (9 GB guard)." }
    $info = $json.Substring($start) | ConvertFrom-Json
    # bucket_size looks like "1.23 GB" (decimal units, as printed by wrangler)
    if ("$($info.bucket_size)" -notmatch '^\s*([\d.,]+)\s*([kMGTP]?B)\s*$') {
        throw "Couldn't understand the bucket size '$($info.bucket_size)', so nothing was uploaded (9 GB guard)."
    }
    $number = [double]::Parse($Matches[1].Replace(",", ""), [Globalization.CultureInfo]::InvariantCulture)
    $scale = @{ "B" = 1; "kB" = 1e3; "MB" = 1e6; "GB" = 1e9; "TB" = 1e12; "PB" = 1e15 }[$Matches[2]]
    return [long]($number * $scale)
}

if ($Upload.Count -gt 0) {
    $incoming = 0L
    foreach ($file in $Upload) {
        if (-not (Test-Path $file)) { throw "Not found: $file" }
        $incoming += (Get-Item $file).Length
    }
    $current = Get-BucketBytes
    $after = $current + $incoming
    $fmt = { param($b) "{0:N2} GB" -f ($b / 1e9) }
    Write-Host ("Storage: {0} used + {1} new = {2} of the 9 GB limit" -f (& $fmt $current), (& $fmt $incoming), (& $fmt $after)) -ForegroundColor DarkGray
    if ($after -gt $LimitBytes) {
        throw ("Upload refused: it would bring storage to {0}, past the 9 GB safety limit (free tier is 10 GB). Delete old videos first." -f (& $fmt $after))
    }
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
