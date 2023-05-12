<#
.SYNOPSIS
    Stress test a BinSkim build.
.PARAMETER BinSkimFolder
    The BinSkim.exe directory
.PARAMETER SessionName
    A stress test session name
.PARAMETER InputPaths
    The scan target input paths to pass to BinSkim
#>

[CmdletBinding()]
param(
    [string]
    $BinSkimFolder = "..\bld\bin\x64_Release\netcoreapp3.1",

    [string]
    $SessionName = "stress",

    [string]
    [Parameter(Mandatory=$true)]
    $InputPaths
)


$OutputFolder = $env:TEMP + "\BinSkimStressTest\$SessionName\"
$OutputFileName = "binskim-$SessionName"
New-Item -Path $OutputFolder -ItemType Directory -Force


$i = 0
$PreviousOutputFilePath = ""

while (1)
{
    $CurrentOutputFilePath = "$OutputFolder\$OutputFileName-$i.sarif"
    
    $command = $BinSkimFolder + "\BinSkim.exe analyze --recurse true --insert Hashes --force --quiet true --output $CurrentOutputFilePath $InputPaths"

    Write-Host "Analyzing... (Iteration $i)"
    Invoke-Expression $command

    if ($i -ne 0)
    {

        $CurrentFileSize = (Get-Item $CurrentOutputFilePath).Length
        $PreviousFileSize = (Get-Item $PreviousOutputFilePath).Length

        if ($CurrentFileSize -eq $PreviousFileSize)
        {
            Remove-Item $PreviousOutputFilePath
        }
        else
        {
            Write-Host "Breaking. Previous output was $($PreviousFileSize) but latest is $($CurrentFileSize)."
            break
        }
    }
    $PreviousOutputFilePath = $CurrentOutputFilePath
    $i++
}