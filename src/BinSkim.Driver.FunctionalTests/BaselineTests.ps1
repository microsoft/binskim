Param(
    [string]$RuleName
)

$tool = "BinSkim"
$utility = "$PSScriptRoot\..\..\bld\bin\$tool.Driver\x86_Release\$tool.exe"

function Build-Tool()
{
    Write-Host "Building the tool..."  -NoNewline
    # Out-Null *and* /noconsolelogger here because our scripts call out to batch files and similar
    # that don't respect msbuild's /noconsolelogger switch.
    msbuild $PSScriptRoot\..\$tool.Driver\$tool.Driver.csproj /p:"Platform=x86`;Configuration=Release" /m 
    Write-Host " done."
}


function Build-Baselines($sourceExtension)
{
    Write-Host "Building baselines..."
    $expectedDirectory = Join-Path "$PSScriptRoot\BaselineTestsData" $ruleName
	$expectedDirectory = Join-Path $expectedDirectory "Expected"
    $testsDirectory = "$PSScriptRoot\BaselineTestsData\" 
	Write-Host "$sourceExtension"

    Get-ChildItem $testsDirectory -Filter $sourceExtension | ForEach-Object {
        Write-Host "    $_ -> $_.sarif"
        $input = $_.FullName
		$outputFile = $_.Name
        $output = Join-Path $expectedDirectory "$outputFile.sarif"
        $outputTemp = "$output.temp"

        # Actually run the tool
        Remove-Item $outputTemp -ErrorAction SilentlyContinue
        &$utility analyze "$input" --output "$outputTemp" --verbose --config default

        Move-Item $outputTemp $output -Force
    }
}

Build-Tool
Build-Baselines "*.dll"
Build-Baselines "*.exe"

Write-Host "Finished! Terminate."
