Param(
    [string]$RuleName
)

$tool = "BinSkim"
$repoRoot = ( Resolve-Path "$PSScriptRoot\..\..\" ).ToString()
$utility = "$repoRoot\bld\bin\$tool.Driver\x86_Release\$tool.exe"
$msbuild = "C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"

function Build-Tool()
{
    Write-Host "Building the tool..."  -NoNewline
    # Out-Null *and* /noconsolelogger here because our scripts call out to batch files and similar
    # that don't respect msbuild's /noconsolelogger switch.
    &$msbuild $PSScriptRoot\..\$tool.Driver\$tool.Driver.csproj /p:"Platform=x86`;Configuration=Release" /m 
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
	Write-Host "$utility analyze "$input" --output "$outputTemp" --verbose --config default"
        &$utility analyze "$input" --output "$outputTemp" --verbose --hashes --config default

        # Replace repository root absolute path with Z:\ for machine and enlistment independence
        $text = [IO.File]::ReadAllText($outputTemp)
        $text = $text.Replace($repoRoot.Replace("\", "\\"), "Z:\\")
        $text = $text.Replace($repoRoot.Replace("\", "/"), "Z:/")

        # Remove stack traces as they can change due to inlining differences by configuration and runtime.
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\\r\\n   at [^""]+", "", [Text.RegularExpressions.RegexOptions]::Singleline)

        # Remove log file details that change on every tool run
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"time`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"startTime`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"endTime`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"processId`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "      `"id`"[^,]+,\s+`"tool`"", "      `"tool`"", [Text.RegularExpressions.RegexOptions]::Singleline)
    
        [IO.File]::WriteAllText($outputTemp, $text, [Text.Encoding]::UTF8)
        Move-Item $outputTemp $output -Force
    }
}

Build-Tool
Build-Baselines "*.dll"
Build-Baselines "*.exe"

Write-Host "Finished! Terminate."
