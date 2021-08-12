Param(
    [string]$RuleName
)

$tool = "BinSkim"
$repoRoot = ( Resolve-Path "$PSScriptRoot\..\..\" ).ToString()
$utility = "$repoRoot\bld\bin\AnyCPU_Release\netcoreapp3.1\win-x64\$tool.exe"

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
    $expectedDirectory = Join-Path "$PSScriptRoot\BaselineTestsData" $ruleName
    $expectedDirectory = Join-Path $expectedDirectory "Expected"
    $testsDirectory = "$PSScriptRoot\BaselineTestsData\" 
    Write-Host "$sourceExtension"

    Get-ChildItem $testsDirectory -Filter $sourceExtension | ForEach-Object {
        Write-Host ""
        $input = $_.FullName
        $outputFile = $_.Name
        $output = Join-Path $expectedDirectory "$outputFile.sarif"
        $outputTemp = "$output.temp"

        # Actually run the tool
        Remove-Item $outputTemp -ErrorAction SilentlyContinue
        Write-Host "$utility analyze "$input" --output "$outputTemp" --kind Fail`;Pass`;NotApplicable --level Error`;Warning`;Note --insert Hashes --remove NondeterministicProperties --config default --quiet --sarif-output-version Current"
        &           $utility analyze "$input" --output "$outputTemp" --kind Fail`;Pass`;NotApplicable --level Error`;Warning`;Note --insert Hashes --remove NondeterministicProperties --config default --quiet --sarif-output-version Current

        # Replace repository root absolute path with Z:\ for machine and enlistment independence
        $text = [IO.File]::ReadAllText($outputTemp)
        $text = $text.Replace($repoRoot.Replace("\", "\\"), "Z:\\")
        $text = $text.Replace($repoRoot.Replace("\", "/"), "Z:/")

        # Remove stack traces as they can change due to inlining differences by configuration and runtime.
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\\r\\n   at [^""]+", "", [Text.RegularExpressions.RegexOptions]::Singleline)

        # Remove log file details that change on every tool run
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"time`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"startTimeUtc`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"endTimeUtc`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"processId`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "      `"id`"[^,]+,\s+`"tool`"", "      `"tool`"", [Text.RegularExpressions.RegexOptions]::Singleline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "`"name`": `"BinSkim`"", "`"name`": `"testhost`"")
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"semanticVersion`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"organization`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"product`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"sarifLoggerVersion`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"fullName`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"CompanyName`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "(?is),[^`"]+`"properties[^`"]+`"Comments`"[^}]+}\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "\s*`"ProductName`"[^\n]+?\n", [Environment]::Newline)
        $text = [Text.RegularExpressions.Regex]::Replace($text, "    `"version`"[^,]+?,", "    `"version`": `"15.0.0.0`",")
    
        [IO.File]::WriteAllText($outputTemp, $text, [Text.Encoding]::UTF8)
        Move-Item $outputTemp $output -Force
    }
}

Build-Tool
Build-Baselines "*.dll"
Build-Baselines "*.exe"
Build-Baselines "gcc.*"
Build-Baselines "clang.*"

Write-Host "Finished! Terminate."
