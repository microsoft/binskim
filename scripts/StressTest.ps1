<#
.SYNOPSIS
    Stress test a BinSkim build.
.PARAMETER BinSkimFolder
    The path to a folder where we would find the BinSkim executable.
.PARAMETER OutputFileName
    The name that will be used to output the SARIF log file.
.PARAMETER InputPaths
    The paths that will be used as argument in BinSkim.
#>

[CmdletBinding()]
param(
    [string]
    $BinSkimFolder = "..\bld\bin\x64_Release\netcoreapp3.1",

    [string]
    $OutputFileName = "binskim-hash-run-",

    [string]
    [Parameter(Mandatory=$true)]
    $InputPaths
)

$OutputFolder = $env:TEMP + "\BinSkimStressTest\"
New-Item -Path $OutputFolder -ItemType Directory -Force

$i = 0
while (1)
{
    $PreviousOutputFilePath = ""
    $CurrentOutputFilePath = $OutputFolder + $OutputFileName + $i + ".sarif"
    
    $command = $BinSkimFolder + "\BinSkim.exe analyze --recurse --hashes --force --quiet --output " + $CurrentOutputFilePath + " " + $InputPaths

    Write-Host "Analyzing iteration " $i
    Invoke-Expression $command

    if ($i -ne 0)
    {
        $PreviousOutputFilePath = $OutputFolder + $OutputFileName + ($i - 1) + ".sarif"

        $CurrentFileSize = (Get-Item $CurrentOutputFilePath).Length/1KB
        $PreviousFileSize = (Get-Item $PreviousOutputFilePath).Length/1KB

        if ($CurrentFileSize -eq $PreviousFileSize)
        {
            Remove-Item $PreviousOutputFilePath
        }
        else
        {
            Write-Host "Previous output was $($PreviousFileSize) but latest is $($CurrentFileSize)."
            break
        }
    }

    $i++
}