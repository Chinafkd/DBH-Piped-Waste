param(
    [string]$RimWorldManagedDir = "D:\Software\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed",
    [string]$DBHAssembliesDir = "D:\Software\Steam\steamapps\workshop\content\294100\836308268\1.6\Assemblies",
    [string]$HarmonyAssembliesDir = "D:\Software\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $projectRoot "Source\DBHPipedWaste"
$outputDir = Join-Path $projectRoot "1.6\Assemblies"
$compiler = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }

$frameworkReferences = @("mscorlib.dll", "System.dll", "System.Core.dll", "netstandard.dll") | ForEach-Object { Join-Path $RimWorldManagedDir $_ }
$gameReferences = @("Assembly-CSharp.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.IMGUIModule.dll") | ForEach-Object { Join-Path $RimWorldManagedDir $_ }
$allReferences = $frameworkReferences + $gameReferences + (Join-Path $DBHAssembliesDir "BadHygiene.dll") + (Join-Path $HarmonyAssembliesDir "0Harmony.dll")
foreach ($reference in $allReferences) { if (-not (Test-Path -LiteralPath $reference)) { throw "Required reference not found: $reference" } }

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sources = Get-ChildItem -LiteralPath $sourceDir -Recurse -Filter "*.cs" | Select-Object -ExpandProperty FullName
$referenceArguments = $allReferences | ForEach-Object { "/reference:$($_)" }
$outputPath = Join-Path $outputDir "DBHPipedWaste.dll"
$pdbPath = Join-Path $outputDir "DBHPipedWaste.pdb"

& $compiler /nologo /target:library /langversion:7.3 /nostdlib+ /deterministic+ /optimize+ /debug:pdbonly "/out:$outputPath" "/pdb:$pdbPath" $referenceArguments $sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built $outputPath"
