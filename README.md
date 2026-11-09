# BinSkim

BinSkim is a binary static analysis tool that checks compiler, linker, and other security-relevant characteristics of Windows Portable Executable (PE), Linux ELF, and macOS Mach-O files. Results use the [Static Analysis Results Interchange Format (SARIF)](https://www.oasis-open.org/standard/sarif-v2-1-0/).

## Install and run

Download the [Microsoft.CodeAnalysis.BinSkim NuGet package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/) and select the executable for the platform where BinSkim will run. See [Install BinSkim](docs/UserGuide.md#install-binskim) for the package layout and supported runtime identifiers.

The examples assume the selected executable is available as `BinSkim` on `PATH`. Analyze a binary and write the results to a SARIF file:

```console
BinSkim analyze path-to-binary --output results.sarif
```

Run `BinSkim analyze --help` for the authoritative command-line reference.

## Documentation

| Document | Purpose |
| --- | --- |
| [User guide](docs/UserGuide.md) | Installation, analysis workflows, rule selection, symbols, SARIF output, and automation guidance |
| [Rule reference](docs/BinSkimRules.md) | Generated descriptions and messages for every BinSkim rule |
| [Troubleshooting](docs/Troubleshooting.md) | Common analysis failures and rule-specific remediation |
| [Release history](ReleaseHistory.md) | Changes included in each release |
| [Rule contributions](docs/RuleContributions.md) | End-to-end workflow for adding or changing a rule |

## Build and contribute

Build the solution from the repository root:

```console
dotnet build src/BinSkim.sln -c Release
```

On Windows, `BuildAndTest.cmd` restores and builds the solution, runs tests, creates platform packages and NuGet packages, and regenerates `docs\BinSkimRules.md`.

On Linux and macOS, `BuildAndTest.sh` runs the test suite. It does not perform the packaging and generated-documentation steps from the Windows script.

Use the [rule contribution guide](docs/RuleContributions.md) for rule changes. For other changes, open or locate a relevant [GitHub issue](https://github.com/microsoft/binskim/issues) and keep the implementation focused on that scenario.

### Windows DIA dependency

Builds that deploy Windows x64 DIA download `Microsoft.VisualCpp.DIA.SDK` from
the public Visual Studio Stable distribution. The version, immutable download URL
and SHA-256 hashes of the VSIX and native DLL are pinned in
`src\Directory.Packages.props`. MSBuild caches and verifies the SDK in each
project's intermediate directory before deploying only `msdia140.dll`. A clean
build needs access to `download.visualstudio.microsoft.com`; it does not need
Visual Studio or COM registration. Non-Windows builds do not download the SDK
unless publishing for `win-x64`. The SDK contains no managed `Dia2Lib.dll`, so
the existing interop assembly and managed TraceEvent dependency are unchanged. See
[the runtime notices](./src/BinSkim.Driver/README.txt) for provenance and licensing.

### Validating native DIA versions

The C# tests validate the pinned SDK DLL by default. On Windows x64, run the same
tests against a trusted candidate DLL with the standard .NET test runner:

```powershell
dotnet test src\BinSkim.sln -c Release --environment "BINSKIM_DIA_CANDIDATE=C:\candidate\msdia140.dll"
```

Test-only native-library resolution selects the candidate for BinaryParsers'
in-process DIA calls without replacing build outputs or changing the production
loader. The xUnit tests check compiler options, function security flags, source
checksums, Source Link streams, segments and section contributions, including
classic-PDB/MSFZ equivalence. No PowerShell wrapper or second test framework is
required. This checks BinSkim's exercised contracts, not every DIA SDK interface
or binary compatibility of a replacement managed `Dia2Lib.dll`.

### Validating symbol-server behavior

[`SymsrvTests`](./src/Test.UnitTests.BinaryParsers/PEBinary/SymsrvTests.cs)
exercise DIA/symsrv using self-contained symbol stores on Windows. No external
network or credentials are required.

```powershell
dotnet test src\Test.UnitTests.BinaryParsers\Test.UnitTests.BinaryParsers.csproj -c Release --filter FullyQualifiedName~SymsrvTests --blame-hang --blame-hang-timeout 180s
```

### Live symbol-server integration

[`SymbolServerIntegrationTests`](./src/Test.FunctionalTests.BinSkim.Driver/SymbolServerIntegrationTests.cs)
cover real servers and require Windows with permitted outbound HTTPS.

Local runs exclude `Category=LiveSymbolServer` by default; explicit filters
select live cases. Windows CI runs the public case (`Requires=Network`) inside
`Build and Test`, excluding private cases (`Requires=PrivateSymbolServer`).
Linux CI excludes live cases.

Run the public case with:

```powershell
dotnet test src\Test.FunctionalTests.BinSkim.Driver\Test.FunctionalTests.BinSkim.Driver.csproj -c Release --filter "Category=LiveSymbolServer&Requires!=PrivateSymbolServer" --logger trx --blame-hang --blame-hang-timeout 180s
```

Run private cases only on a protected, network-enabled Windows agent with an
authorized managed/workload identity or another approved authorization method.
The agent must configure the native symbol client's credential provider.
Select private cases with:

```powershell
dotnet test src\Test.FunctionalTests.BinSkim.Driver\Test.FunctionalTests.BinSkim.Driver.csproj -c Release --filter "Requires=PrivateSymbolServer" --logger trx --blame-hang --blame-hang-timeout 180s
```

Set `BINSKIM_SYMBOL_SERVER_TEST_CONFIG` to a protected runtime JSON file.
Each array entry supplies `Name` (a non-sensitive alias), `ServerUrl` (HTTPS, no
user information or query string), and `BinaryPath` (a controlled PE whose full
PDB is on that server). Missing or empty configuration fails a selected test
rather than skipping it. Do not commit this file, private endpoints, customer
binaries or credentials/tokens.
Public access and HTTP 401/403 rejection do not prove private authentication.

For native upgrades, compare baseline and candidate builds in fresh test
processes, retaining TRX results and native version/hash diagnostics. Use
`--no-build` after deploying a candidate to preserve its test output. Private
authentication, proxies and the supported Windows-image matrix need separate
integration/canary coverage.

## Security

Do not report security vulnerabilities through public GitHub issues. Follow the instructions in [SECURITY.md](SECURITY.md).

## License

BinSkim is licensed under the [MIT License](LICENSE).
