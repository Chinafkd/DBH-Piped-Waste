param(
    [string]$Version = "0.1.5"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-ProjectPath([string]$relativePath) {
    $fullPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $relativePath))
    $rootWithSeparator = [IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes project root: $fullPath"
    }
    return $fullPath
}

function Remove-GeneratedPath([string]$relativePath) {
    $path = Resolve-ProjectPath $relativePath
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

function Copy-Tree([string]$relativePath, [string]$destinationRoot) {
    $source = Resolve-ProjectPath $relativePath
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Required directory not found: $source"
    }
    $destination = Join-Path $destinationRoot $relativePath
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $destination -Recurse -Force
}

function Copy-File([string]$relativePath, [string]$destinationRoot) {
    $source = Resolve-ProjectPath $relativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required file not found: $source"
    }
    $destination = Join-Path $destinationRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

# Rebuild both assemblies and the clean test copy before packaging.
& (Resolve-ProjectPath "build_demo.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Demo build failed with exit code $LASTEXITCODE"
}

$releaseRoot = Resolve-ProjectPath "Release"
$githubRoot = Resolve-ProjectPath "GitHub"
$runtimeStage = Resolve-ProjectPath "_package_staging\DBHPipedWaste"
$sourceStage = Resolve-ProjectPath "_package_staging\DBH-Piped-Waste"
$githubSource = Resolve-ProjectPath "GitHub\DBH-Piped-Waste"
$existingPublishedFileIdPath = Resolve-ProjectPath "Release\WorkshopUpload\DBHPipedWaste\About\PublishedFileId.txt"
$publishedFileId = $null
if (Test-Path -LiteralPath $existingPublishedFileIdPath -PathType Leaf) {
    $candidatePublishedFileId = (Get-Content -LiteralPath $existingPublishedFileIdPath -Raw).Trim()
    if ($candidatePublishedFileId -notmatch '^\d+$') {
        throw "Invalid existing Workshop PublishedFileId: $candidatePublishedFileId"
    }
    $publishedFileId = $candidatePublishedFileId
}

Remove-GeneratedPath "Release\DBHPipedWaste"
Remove-GeneratedPath "Release\WorkshopUpload\DBHPipedWaste"
Remove-GeneratedPath "Release\DBHPipedWaste-$Version.zip"
Remove-GeneratedPath "GitHub\DBH-Piped-Waste-Source-$Version.zip"
Remove-GeneratedPath "GitHub\DBH-Piped-Waste"
Remove-GeneratedPath "_package_staging"

New-Item -ItemType Directory -Path $runtimeStage,$sourceStage | Out-Null

$runtimeFiles = @(
    "About\About.xml",
    "About\Preview.png",
    "LoadFolders.xml",
    "README.md",
    "LICENSE",
    "CHANGELOG.md",
    "THIRD_PARTY_NOTICES.md",
    "1.6\Assemblies\DBHPipedWaste.dll"
)
$runtimeTrees = @(
    "1.6\Compat",
    "1.6\Defs",
    "1.6\Languages"
)
foreach ($file in $runtimeFiles) { Copy-File $file $runtimeStage }
foreach ($tree in $runtimeTrees) { Copy-Tree $tree $runtimeStage }

$sourceFiles = $runtimeFiles + @(
    ".gitattributes",
    ".gitignore",
    "build.ps1",
    "build_demo.ps1",
    "build_release.ps1",
    "DBH_Piped_Waste_Addon_Design_Spec_v1.0.md"
)
$sourceTrees = @(
    "1.6\Compat",
    "1.6\Defs",
    "1.6\Languages",
    "Docs",
    "Source"
)
foreach ($file in $sourceFiles) { Copy-File $file $sourceStage }
foreach ($tree in $sourceTrees) { Copy-Tree $tree $sourceStage }

# Parse every shipped XML file before creating archives.
Get-ChildItem -LiteralPath $runtimeStage -Recurse -Filter "*.xml" | ForEach-Object {
    [xml](Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8) | Out-Null
}

$releaseMod = Resolve-ProjectPath "Release\DBHPipedWaste"
$workshopMod = Resolve-ProjectPath "Release\WorkshopUpload\DBHPipedWaste"
New-Item -ItemType Directory -Path (Split-Path -Parent $releaseMod),(Split-Path -Parent $workshopMod),$githubRoot -Force | Out-Null
Copy-Item -LiteralPath $runtimeStage -Destination $releaseMod -Recurse -Force
Copy-Item -LiteralPath $runtimeStage -Destination $workshopMod -Recurse -Force
Copy-Item -LiteralPath $sourceStage -Destination $githubSource -Recurse -Force
if ($publishedFileId) {
    Set-Content -LiteralPath (Join-Path $workshopMod "About\PublishedFileId.txt") -Value $publishedFileId -Encoding Ascii -NoNewline
}

$releaseZip = Resolve-ProjectPath "Release\DBHPipedWaste-$Version.zip"
$sourceZip = Resolve-ProjectPath "GitHub\DBH-Piped-Waste-Source-$Version.zip"
Compress-Archive -LiteralPath $releaseMod -DestinationPath $releaseZip -CompressionLevel Optimal -Force
Compress-Archive -LiteralPath $sourceStage -DestinationPath $sourceZip -CompressionLevel Optimal -Force

Remove-GeneratedPath "_package_staging"

Write-Output "Runtime release: $releaseZip"
Write-Output "Source release: $sourceZip"
Write-Output "Source tree: $githubSource"
Write-Output "Workshop upload: $workshopMod"
