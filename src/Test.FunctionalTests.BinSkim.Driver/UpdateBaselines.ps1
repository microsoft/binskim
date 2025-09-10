Param(
    [string]$RuleName
)

$tool = "BinSkim"
$repoRoot = ( Resolve-Path "$PSScriptRoot\..\.." ).ToString()
$utility = "$repoRoot\bld\bin\BinSkim.Driver\release\$tool.exe"

function Build-Tool()
{
    Write-Host "Building the tool..."  -NoNewline
    # Out-Null *and* /noconsolelogger here because our scripts call out to batch files and similar
    # that don't respect msbuild's /noconsolelogger switch.
    &dotnet build $PSScriptRoot\..\$tool.Driver\$tool.Driver.csproj -c Release --runtime win-x64 /m /verbosity:minimal
    Write-Host " done."
}


function Build-Baselines($sourceExtension)
{
    Write-Host "Building baselines..."
    $expectedDirectory = Join-Path "$PSScriptRoot\BaselineTestData" $ruleName
    $expectedDirectory = Join-Path $expectedDirectory "Expected"
    $testsDirectory = "$PSScriptRoot\BaselineTestData\" 
    Write-Host "$sourceExtension"

    Get-ChildItem $testsDirectory -Filter $sourceExtension | ForEach-Object {
        Write-Host ""
        $input = $_.FullName
        $outputFile = $_.Name
        $output = Join-Path $expectedDirectory "$outputFile.sarif"
        $outputTemp = "$output.temp"

        # Actually run the tool
        Remove-Item $outputTemp -ErrorAction SilentlyContinue
        Write-Host "$utility analyze "$input" --output "$output" --kind "Fail`;Pass" --level "Error`;Warning`;Note" --remove NondeterministicProperties --config default --quiet true --enlistment-root $repoRoot --log ForceOverwrite"
        &           $utility analyze "$input" --output "$output" --kind Fail`;Pass --level Error`;Warning`;Note --remove NondeterministicProperties --config default --quiet true --enlistment-root $repoRoot --log ForceOverwrite
    }
}

Build-Tool
#Build-Baselines "Binskim.linux-x64.dll"
Build-Baselines "*.dll"
Build-Baselines "*.exe"
Build-Baselines "gcc.*"
Build-Baselines "clang.*"
Build-Baselines "macho.*"

Write-Host "Finished! Terminate."
