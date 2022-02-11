<#
.SYNOPSIS
    Stress test a BinSkim build.
.PARAMETER BinSkimFolder
    The path to a folder where we would find the BinSkim executable.
.PARAMETER OutputFileName
    The name that will be used to output the SARIF log file.
.PARAMETER InputPaths
    The paths that will as argument in BinSkim.
#>

[CmdletBinding()]
param(
    [string]
    $BinSkimFolder = "..\bld\bin\x64_Release\netcoreapp3.1",

    [string]
    $OutputFileName = "binskim-hash-run-",

    [string]
    $InputPaths
)

$OutputFolder = $env:TEMP + "\BinSkimStressTest\"
New-Item -Path $OutputFolder -ItemType Directory -Force

$i = 0
while (1)
{
    $PreviousOutputFilePath = ""
    $CurrentOutputFilePath = $OutputFolder + $OutputFileName + $i + ".sarif"
    
    if ($i -ne 0)
    {
        $PreviousOutputFilePath = $OutputFolder + $OutputFileName + ($i - 1) + ".sarif"
    }
    
    $command = $BinSkimFolder + "\BinSkim.exe analyze --recurse --hashes --force --output " + $CurrentOutputFilePath + " " + $InputPaths
    Invoke-Expression $command

    if ($PreviousOutputFilePath -ne "")
    {
        $CurrentFileSize = (Get-Item $CurrentOutputFilePath).Length/1KB
        $PreviousFileSize = (Get-Item $PreviousOutputFilePath).Length/1KB

        if ($CurrentFileSize -eq $PreviousFileSize)
        {
            Remove-Item $PreviousOutputFilePath
        }
        else
        {
            break
        }
    }

    $i++
}