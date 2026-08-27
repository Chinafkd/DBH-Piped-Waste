param(
    [string]$RimWorldManagedDir = "D:\Software\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed",
    [string]$DBHAssembliesDir = "D:\Software\Steam\steamapps\workshop\content\294100\836308268\1.6\Assemblies",
    [string]$HarmonyAssembliesDir = "D:\Software\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $projectRoot "build.ps1") -RimWorldManagedDir $RimWorldManagedDir -DBHAssembliesDir $DBHAssembliesDir -HarmonyAssembliesDir $HarmonyAssembliesDir

$demoRoot = Join-Path $projectRoot "Demo\DBHPipedWaste"
$resolvedProject = [IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
$resolvedDemo = [IO.Path]::GetFullPath($demoRoot)
if (-not $resolvedDemo.StartsWith($resolvedProject, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedDemo) -ne "DBHPipedWaste") {
    throw "Unsafe demo output path: $resolvedDemo"
}
if (Test-Path -LiteralPath $demoRoot) {
    Remove-Item -Recurse -Force -LiteralPath $demoRoot
}

$directories = @(
    "About",
    "1.6\Defs",
    "1.6\Languages",
    "1.6\Compat",
    "Docs"
)
foreach ($relative in $directories) {
    $source = Join-Path $projectRoot $relative
    $destination = Join-Path $demoRoot $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -Recurse -Force -LiteralPath $source -Destination $destination
}
New-Item -ItemType Directory -Force -Path (Join-Path $demoRoot "1.6\Assemblies") | Out-Null
Copy-Item -Force -LiteralPath (Join-Path $projectRoot "1.6\Assemblies\DBHPipedWaste.dll") -Destination (Join-Path $demoRoot "1.6\Assemblies\DBHPipedWaste.dll")
Copy-Item -Force -LiteralPath (Join-Path $projectRoot "LoadFolders.xml"),(Join-Path $projectRoot "README.md") -Destination $demoRoot

Write-Output "Demo ready: $demoRoot"
