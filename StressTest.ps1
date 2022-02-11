[CmdletBinding()]
param(
    [string]
    $BinSkimFolder = "bld\bin\x64_Release\netcoreapp3.1",

    [string]
    $OutputFolder = "c:\temp\",

    [string]
    $OutputFile = "binskim",

    [string]
    $InputPaths,

    [int]
    $Times = 1000
)

for ($i = 0; $i -lt $Times; $i++)
{
    $command = $BinSkimFolder + "\BinSkim.exe analyze --recurse --hashes --force --output " + $OutputFolder + $OutputFile + $i + ".sarif " + $InputPaths
    Write-Host $command
    Invoke-Expression $command
}