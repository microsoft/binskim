# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Compare a trusted candidate native DIA DLL with the current build's DIA.
.PARAMETER CandidateDll
    Path to an x64 msdia140.dll to execute in an isolated copy of the parser tests.
#>

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDll
)

$ErrorActionPreference = 'Stop'
if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
    throw 'DIA comparison requires Windows and x64 PowerShell.'
}

$candidate = Get-Item -LiteralPath $CandidateDll
if ($candidate.PSIsContainer) {
    throw 'CandidateDll must be a file.'
}
$candidateHash = (Get-FileHash -LiteralPath $candidate.FullName -Algorithm SHA256).Hash
$repoRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
$project = Join-Path $repoRoot 'src\Test.UnitTests.BinaryParsers\Test.UnitTests.BinaryParsers.csproj'
& dotnet build $project --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "Building the parser tests failed (exit $LASTEXITCODE)."
}

$source = Join-Path $repoRoot 'bld\bin\Test.UnitTests.BinaryParsers\release'
$baselineDll = Join-Path $source 'msdia140.dll'
$baselineHash = (Get-FileHash -LiteralPath $baselineDll -Algorithm SHA256).Hash
$runId = [Guid]::NewGuid().ToString('N')
$executionRoot = Join-Path $repoRoot "bld\bin\DiaCompatibility_$runId"
$resultsRoot = Join-Path $repoRoot "bld\dia-compatibility\$runId"

try {
    foreach ($name in @('baseline', 'candidate')) {
        # Preserve the test assembly's depth so its existing fixture locator still works.
        $directory = Join-Path $executionRoot $name
        $snapshots = Join-Path $resultsRoot $name
        New-Item -ItemType Directory -Path $directory, $snapshots | Out-Null
        Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $directory -Recurse
        }
        if ($name -eq 'candidate') {
            Copy-Item -LiteralPath $candidate.FullName -Destination (Join-Path $directory 'msdia140.dll') -Force
        }

        $dll = Join-Path $directory 'msdia140.dll'
        $expectedHash = if ($name -eq 'candidate') { $candidateHash } else { $baselineHash }
        if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $expectedHash) {
            throw "The $name test directory contains the wrong native DLL."
        }

        Write-Host "$name DIA: $((Get-Item -LiteralPath $dll).VersionInfo.FileVersion), SHA256=$expectedHash"
        & dotnet vstest (Join-Path $directory 'Test.UnitTests.BinaryParsers.dll') `
            "--ResultsDirectory:$snapshots" '--Logger:trx;LogFileName=tests.trx' `
            "--Environment:BINSKIM_DIA_SNAPSHOTS=$snapshots"
        if ($LASTEXITCODE -ne 0) {
            throw "$name parser tests failed (exit $LASTEXITCODE). Results: $snapshots"
        }
    }

    $baseline = @(Get-ChildItem -LiteralPath (Join-Path $resultsRoot 'baseline') -Filter '*.json' -File)
    $candidateResults = @(Get-ChildItem -LiteralPath (Join-Path $resultsRoot 'candidate') -Filter '*.json' -File)
    if ($baseline.Count -eq 0 -or $baseline.Count -ne $candidateResults.Count) {
        throw "Missing or unequal DIA snapshots. Results: $resultsRoot"
    }

    $differences = @()
    foreach ($file in $baseline) {
        $otherPath = Join-Path $resultsRoot "candidate\$($file.Name)"
        $expected = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $actual = Get-Content -LiteralPath $otherPath -Raw | ConvertFrom-Json
        $sections = @($expected.Data.PSObject.Properties.Name)
        if (@(Compare-Object $sections @($actual.Data.PSObject.Properties.Name)).Count -ne 0) {
            throw "Snapshot sections differ for $($file.Name). Results: $resultsRoot"
        }
        foreach ($section in $sections) {
            $expectedData = ConvertTo-Json -InputObject @($expected.Data.$section) -Depth 20 -Compress
            $actualData = ConvertTo-Json -InputObject @($actual.Data.$section) -Depth 20 -Compress
            if ($expectedData -cne $actualData) {
                $differences += "$($file.Name): $section"
            }
        }
    }
    if ($differences.Count -ne 0) {
        throw "DIA behavior differs in: $($differences -join '; '). Results: $resultsRoot"
    }

    Write-Host "DIA comparison passed: $($baseline.Count) fixture snapshots match. Results: $resultsRoot"
}
finally {
    if (Test-Path -LiteralPath $executionRoot) {
        Remove-Item -LiteralPath $executionRoot -Recurse -Force
    }
}
