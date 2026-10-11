param([switch]$DownloadPrevious)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    [xml]$project = Get-Content src/CategoryBar/CategoryBar.csproj
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part stable version.' }
    $notes = "Documentation/release-notes-$version.md"
    if (!(Test-Path -LiteralPath $notes)) { throw "Missing release notes: $notes" }
    # Each version gets a fresh directory; stale packages must never enter a later release.
    $publish = "artifacts/publish-$version"
    if (Test-Path -LiteralPath $publish) { throw "Publish folder already exists: $publish. Use a fresh checkout for releases." }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    & ./scripts/build.ps1
    dotnet publish src/CategoryBar -c Release -r win-x64 --self-contained true -o $publish -p:PublishSingleFile=false -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if ($DownloadPrevious) {
        $latest = gh api repos/Chikanut/Windows-Category-Bar/releases/latest | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect previous release.' }
        # The initial portable release has no Velopack feed to download.
        if ($latest.assets.name -contains 'releases.win.json') {
            # Anonymous requests share the runner's address and run into GitHub's rate limit.
            $token = if ($env:GH_TOKEN) { @('--token', $env:GH_TOKEN) } else { @() }
            dotnet vpk download github --repoUrl https://github.com/Chikanut/Windows-Category-Bar --outputDir artifacts/releases @token
            if ($LASTEXITCODE -ne 0) { throw 'Previous package download failed.' }
        }
    }
    # Keep the installation root distinct from %LOCALAPPDATA%/WindowsCategoryBar (user data).
    dotnet vpk pack --packId CategoryBar --packVersion $version --packDir $publish --mainExe CategoryBar.exe --packTitle 'Category Bar' --packAuthors Chikanut --icon src/CategoryBar/Assets/App.ico --releaseNotes $notes --runtime win-x64 --outputDir artifacts/releases --shortcuts StartMenuRoot
    if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
} finally { Pop-Location }
