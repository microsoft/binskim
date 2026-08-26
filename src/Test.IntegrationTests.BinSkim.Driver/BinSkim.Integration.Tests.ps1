# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#Requires -Modules Pester

<#
.SYNOPSIS
    Pester integration tests for BinSkim CLI.
    Validates end-to-end behavior by invoking BinSkim as an external process.

.DESCRIPTION
    These tests mirror the C# integration tests in AnalyzeCommandIntegrationTests.cs
    but are written in PowerShell using the Pester framework for use in pipeline scenarios.

.EXAMPLE
    Invoke-Pester -Path ./BinSkim.Integration.Tests.ps1
#>

BeforeAll {
    $RepoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
    $BinSkimDll = Join-Path $RepoRoot "bld/bin/BinSkim.Driver/release/BinSkim.dll"

    if (-not (Test-Path $BinSkimDll)) {
        # Try debug build
        $BinSkimDll = Join-Path $RepoRoot "bld/bin/BinSkim.Driver/debug/BinSkim.dll"
    }

    if (-not (Test-Path $BinSkimDll)) {
        throw "BinSkim.dll not found. Build the BinSkim.Driver project first."
    }

    $FunctionalTestData = Join-Path $RepoRoot "src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestData"
    $BinaryParsersTestData = Join-Path $RepoRoot "src/Test.UnitTests.BinaryParsers/TestData"

    function Invoke-BinSkim {
        param(
            [string[]]$Arguments
        )
        $process = Start-Process -FilePath "dotnet" -ArgumentList (@($BinSkimDll) + $Arguments) `
            -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$env:TEMP\binskim_stdout.txt" `
            -RedirectStandardError "$env:TEMP\binskim_stderr.txt"

        return @{
            ExitCode = $process.ExitCode
            StdOut   = (Get-Content "$env:TEMP\binskim_stdout.txt" -Raw -ErrorAction SilentlyContinue) ?? ""
            StdErr   = (Get-Content "$env:TEMP\binskim_stderr.txt" -Raw -ErrorAction SilentlyContinue) ?? ""
        }
    }

    function Get-SarifLog {
        param([string]$Path)
        if (Test-Path $Path) {
            return Get-Content $Path -Raw | ConvertFrom-Json
        }
        return $null
    }
}

Describe "BinSkim CLI - Self Scan" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPester_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Analyze self-scan exits with zero" {
        $sarifOutput = Join-Path $script:TempDir "selfscan.sarif"
        $result = Invoke-BinSkim -Arguments @("analyze", $BinSkimDll, "-o", $sarifOutput)
        $result.ExitCode | Should -Be 0
    }

    It "Analyze self-scan produces valid SARIF" {
        $sarifOutput = Join-Path $script:TempDir "selfscan-sarif.sarif"
        $result = Invoke-BinSkim -Arguments @("analyze", $BinSkimDll, "-o", $sarifOutput)
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $sarif.runs | Should -HaveCount 1
        $sarif.runs[0].tool.driver.name | Should -Be "BinSkim"
    }
}

Describe "BinSkim CLI - ELF Binary Analysis" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterElf_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Analyze ELF binary exits with zero" {
        $elfBinary = Join-Path $FunctionalTestData "BA3006.EnableNonExecutableStack/Pass/gcc.helloworld.noexecstack.5.o"
        $sarifOutput = Join-Path $script:TempDir "elf-exit.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $elfBinary, "-o", $sarifOutput,
            "--kind", "Fail;Pass;NotApplicable", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0
    }

    It "Analyze ELF binary produces valid SARIF with results" {
        $elfBinary = Join-Path $FunctionalTestData "BA3001.EnablePositionIndependentExecutable/Pass/gcc.pie_executable"
        $sarifOutput = Join-Path $script:TempDir "elf-sarif.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $elfBinary, "-o", $sarifOutput,
            "--kind", "Fail;Pass;NotApplicable", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $sarif.runs[0].tool.driver.name | Should -Be "BinSkim"
        $sarif.runs[0].results.Count | Should -BeGreaterThan 0
    }

    It "Analyze ELF known-fail binary reports BA3006 error" {
        $failBinary = Join-Path $FunctionalTestData "BA3006.EnableNonExecutableStack/Fail/gcc.helloworld.execstack.5.o"
        $sarifOutput = Join-Path $script:TempDir "elf-fail.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $failBinary, "-o", $sarifOutput,
            "--run-only-rules", "BA3006", "--kind", "Fail", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $errorResults = $sarif.runs[0].results | Where-Object { $_.ruleId -eq "BA3006" -and $_.level -eq "error" }
        $errorResults | Should -Not -BeNullOrEmpty
    }

    It "Analyze ELF known-pass binary reports BA3001 pass" {
        $passBinary = Join-Path $FunctionalTestData "BA3001.EnablePositionIndependentExecutable/Pass/gcc.pie_executable"
        $sarifOutput = Join-Path $script:TempDir "elf-pass.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $passBinary, "-o", $sarifOutput,
            "--run-only-rules", "BA3001", "--kind", "Fail;Pass", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $passResults = $sarif.runs[0].results | Where-Object { $_.ruleId -eq "BA3001" -and $_.level -eq "none" }
        $passResults | Should -Not -BeNullOrEmpty
    }
}

Describe "BinSkim CLI - Multi-target and Glob" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterMulti_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Analyze multiple targets scans all" {
        $target1 = $BinSkimDll
        $failBinary = Join-Path $FunctionalTestData "BA2016.MarkImageAsNXCompatible/Fail/ManagedFail.dll"
        $sarifOutput = Join-Path $script:TempDir "multi.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $target1, $failBinary, "-o", $sarifOutput,
            "--kind", "Fail;Pass;NotApplicable", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $uris = $sarif.runs[0].results | ForEach-Object {
            $_.locations[0].physicalLocation.artifactLocation.uri
        } | Sort-Object -Unique
        $uris.Count | Should -BeGreaterThan 1
    }

    It "Analyze with recurse finds nested binaries" {
        $subDir = Join-Path $script:TempDir "nested"
        New-Item -ItemType Directory -Path $subDir -Force | Out-Null

        Copy-Item $BinSkimDll (Join-Path $script:TempDir "top.dll")
        Copy-Item $BinSkimDll (Join-Path $subDir "nested.dll")

        $sarifOutput = Join-Path $script:TempDir "recurse.sarif"
        $globPattern = Join-Path $script:TempDir "*.dll"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $globPattern, "-o", $sarifOutput,
            "--recurse", "True", "--kind", "Fail;Pass;NotApplicable", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $uris = $sarif.runs[0].results | ForEach-Object {
            $_.locations[0].physicalLocation.artifactLocation.uri
        } | Sort-Object -Unique
        $uris.Count | Should -BeGreaterThan 1
    }
}

Describe "BinSkim CLI - Rich Return Code" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterRRC_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Rich return code returns non-zero RuntimeConditions for invalid target" {
        $nonExistent = Join-Path $script:TempDir "does_not_exist.dll"

        $result = Invoke-BinSkim -Arguments @("analyze", $nonExistent, "--rich-return-code")

        $result.ExitCode | Should -Not -Be 0
        $result.ExitCode | Should -Not -Be 1
    }
}

Describe "BinSkim CLI - Local Symbol Directories" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterSym_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Accepts --local-symbol-directories option" {
        $elfBinary = Join-Path $BinaryParsersTestData "Dwarf/hello-dwarf4-o2"
        $sarifOutput = Join-Path $script:TempDir "symdir.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $elfBinary, "-o", $sarifOutput,
            "--local-symbol-directories", $script:TempDir,
            "--kind", "Fail;Pass;NotApplicable", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0
    }
}

Describe "BinSkim CLI - Trace Output" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterTrace_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Trace flags produce output" {
        $sarifOutput = Join-Path $script:TempDir "trace.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $BinSkimDll, "-o", $sarifOutput,
            "--trace", "TargetsScanned;ResultsSummary"
        )
        $result.ExitCode | Should -Be 0

        $combined = $result.StdOut + $result.StdErr
        $combined | Should -Not -BeNullOrEmpty
    }
}

Describe "BinSkim CLI - Error Handling" {
    It "Non-existent target exits with non-zero" {
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterErr_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        try {
            $nonExistent = Join-Path $tempDir "does_not_exist.dll"
            $sarifOutput = Join-Path $tempDir "output.sarif"

            $result = Invoke-BinSkim -Arguments @("analyze", $nonExistent, "-o", $sarifOutput)
            $result.ExitCode | Should -Not -Be 0
        }
        finally {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It "Invalid argument exits with non-zero" {
        $result = Invoke-BinSkim -Arguments @("analyze", "--bogus-flag")
        $result.ExitCode | Should -Not -Be 0
    }

    It "Invalid verb exits with non-zero" {
        $result = Invoke-BinSkim -Arguments @("not-a-real-verb")
        $result.ExitCode | Should -Not -Be 0
    }
}

Describe "BinSkim CLI - Help and Version" {
    It "Help flag exits cleanly" {
        $result = Invoke-BinSkim -Arguments @("help")
        $result.ExitCode | Should -Be 0
        ($result.StdOut + $result.StdErr) | Should -Not -BeNullOrEmpty
    }

    It "--version exits cleanly" {
        $result = Invoke-BinSkim -Arguments @("--version")
        $result.ExitCode | Should -Be 0
        ($result.StdOut + $result.StdErr) | Should -Not -BeNullOrEmpty
    }
}

Describe "BinSkim CLI - Known Fail Binary" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterFail_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Known PE fail binary produces error results" {
        $failBinary = Join-Path $FunctionalTestData "BA2016.MarkImageAsNXCompatible/Fail/ManagedFail.dll"
        $sarifOutput = Join-Path $script:TempDir "pe-fail.sarif"

        $result = Invoke-BinSkim -Arguments @(
            "analyze", $failBinary, "-o", $sarifOutput,
            "--run-only-rules", "BA2016", "--kind", "Fail", "--level", "Error;Warning;Note"
        )
        $result.ExitCode | Should -Be 0

        $sarif = Get-SarifLog -Path $sarifOutput
        $sarif | Should -Not -BeNullOrEmpty
        $errorResults = $sarif.runs[0].results | Where-Object { $_.ruleId -eq "BA2016" -and $_.level -eq "error" }
        $errorResults | Should -Not -BeNullOrEmpty
    }
}

Describe "BinSkim CLI - Dump Command" {
    It "Dump self-scan produces metadata output" {
        $result = Invoke-BinSkim -Arguments @("dump", $BinSkimDll)
        $result.ExitCode | Should -Be 0
        $result.StdOut | Should -Match "BinSkim"
    }

    It "Dump verbose produces more detailed output" {
        $normalResult = Invoke-BinSkim -Arguments @("dump", $BinSkimDll)
        $verboseResult = Invoke-BinSkim -Arguments @("dump", $BinSkimDll, "--verbose")

        $normalResult.ExitCode | Should -Be 0
        $verboseResult.ExitCode | Should -Be 0
        $verboseResult.StdOut.Length | Should -BeGreaterOrEqual $normalResult.StdOut.Length
    }
}

Describe "BinSkim CLI - Export Commands" {
    BeforeAll {
        $script:TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "BinSkimPesterExport_$([guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $script:TempDir -Force | Out-Null
    }

    AfterAll {
        Remove-Item $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "Export-rules produces SARIF with rule metadata" {
        $outputPath = Join-Path $script:TempDir "rules.sarif"

        $result = Invoke-BinSkim -Arguments @("export-rules", $outputPath)
        $result.ExitCode | Should -Be 0

        Test-Path $outputPath | Should -BeTrue
        $content = Get-Content $outputPath -Raw
        $content | Should -Match "BA2016"
    }

    It "Export-config produces JSON config" {
        $outputPath = Join-Path $script:TempDir "config.json"

        $result = Invoke-BinSkim -Arguments @("export-config", $outputPath)
        $result.ExitCode | Should -Be 0

        Test-Path $outputPath | Should -BeTrue
        $content = Get-Content $outputPath -Raw
        $content | Should -Not -BeNullOrEmpty
    }
}
