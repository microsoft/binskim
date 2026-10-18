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

The solution targets .NET 9. Build it from the repository root:

```console
dotnet build src/BinSkim.sln -c Release
```

On Windows, `BuildAndTest.cmd` restores and builds the solution, runs tests, creates platform packages and NuGet packages, and regenerates `docs\BinSkimRules.md`.

On Linux and macOS, `BuildAndTest.sh` runs the test suite. It does not perform the packaging and generated-documentation steps from the Windows script.

Use the [rule contribution guide](docs/RuleContributions.md) for rule changes. For other changes, open or locate a relevant [GitHub issue](https://github.com/microsoft/binskim/issues) and keep the implementation focused on that scenario.

## Security

Do not report security vulnerabilities through public GitHub issues. Follow the instructions in [SECURITY.md](SECURITY.md).

## License

BinSkim is licensed under the [MIT License](LICENSE).
