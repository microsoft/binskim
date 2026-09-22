# BinSkim implementation guidance

Use `README.md` for the repository overview and build entry point. Use `docs\RuleContributions.md` for the complete rule development workflow.

## Repository layout

- `src\BinSkim.Driver` contains the command-line application.
- `src\BinSkim.Rules` contains analysis rules, rule identifiers, and rule resources.
- `src\BinSkim.Sdk` contains shared analysis abstractions.
- `src\BinaryParsers` contains binary format parsing.
- The `src\Test.*` projects contain the corresponding unit and functional tests.
- `docs` contains user guidance, rule documentation, contribution guidance, and test shells.

## Rule changes

- Keep the rule implementation, `RuleIds.cs`, `RuleResources.resx`, functional tests, test assets, and generated rule documentation aligned.
- Do not edit generated resource designer files directly.
- Follow the platform-specific rule and test layout documented in `docs\RuleContributions.md`.
- Review generated SARIF baselines when rule applicability, messages, or output changes.

## Parser and driver changes

- Treat input binaries as untrusted and potentially malformed.
- Preserve behavior across supported operating systems, architectures, and binary formats.
- For command-line changes, consider compatibility of arguments, exit codes, and SARIF output.

## Validation

- Run the smallest affected test project while iterating.
- Use `BuildAndTest.cmd` for full validation. It restores, builds, tests, publishes platform packages, creates NuGet packages, and regenerates `docs\BinSkimRules.md`.
