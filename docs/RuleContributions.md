# Contributing a BinSkim rule

A rule contribution starts with an agreed specification and ends with implementation, focused tests, representative test binaries, reviewed SARIF baselines, and regenerated rule documentation.

## Start with a rule specification

Open or locate a [rule request](https://github.com/microsoft/binskim/issues/new?template=rule-request.md) before implementation. The specification should define:

- The rule ID and friendly name.
- The supported binary formats and platforms.
- Applicable and non-applicable targets.
- Pass, failure, and other user-facing messages.
- The security or correctness outcome and recommended remediation.
- Any configurable policy and its default behavior.

Resolve specification questions with a maintainer before encoding them in resources or tests. The issue is the source of truth for the intended behavior.

## Choose a representative rule

Do not start from a generic rule template. Find a maintained rule with the same binary format, metadata requirements, and platform constraints:

| Scenario | Representative implementation |
| --- | --- |
| PE metadata without PDB data | [`PERules\BA2029.EnableIntegrityCheck.cs`](../src/BinSkim.Rules/PERules/BA2029.EnableIntegrityCheck.cs) |
| PE metadata with PDB data | [`PERules\BA2027.EnableSourceLink.cs`](../src/BinSkim.Rules/PERules/BA2027.EnableSourceLink.cs) |
| ELF structure | [`ElfRules\BA3001.EnablePositionIndependentExecutable.cs`](../src/BinSkim.Rules/ElfRules/BA3001.EnablePositionIndependentExecutable.cs) |
| ELF or Mach-O DWARF data | [`DwarfRules\BA3003.EnableStackProtector.cs`](../src/BinSkim.Rules/DwarfRules/BA3003.EnableStackProtector.cs) |
| Mach-O structure | [`MachORules\BA5001.EnablePositionIndependentExecutable.cs`](../src/BinSkim.Rules/MachORules/BA5001.EnablePositionIndependentExecutable.cs) |

Use the platform-specific base class and parser abstractions already used by the nearest rule. Input binaries are untrusted and may be malformed, truncated, or internally inconsistent.

## Add rule metadata

1. Add the friendly-name constant and assigned ID to [`RuleIds.cs`](../src/BinSkim.Rules/RuleIds.cs).
2. Add the full description and result messages to [`RuleResources.resx`](../src/BinSkim.Rules/RuleResources.resx), following the naming pattern of adjacent rules.
3. Do not edit `RuleResources.Designer.cs`. The resource designer regenerates it from the `.resx` file.
4. Add the rule class under `PERules`, `ElfRules`, `DwarfRules`, or `MachORules`.
5. Export the rule as a skimmer and reporting descriptor, then expose its ID, full description, and message resource names.

Implement `IOptionsProvider` only when the rule has configurable policy. Override default enablement only when the specification requires it.

## Implement applicability and analysis

Keep applicability separate from analysis:

- Applicability should reject every target that the rule cannot evaluate reliably.
- Return an existing `MetadataConditions` reason where one describes the observed condition.
- Add a new metadata condition only when existing reasons are not accurate.
- Analysis should emit results only after applicability succeeds.
- Use the shared result-building and logging helpers so SARIF output remains consistent.

Preserve behavior across supported operating systems and architectures. A rule that depends on Windows APIs or Windows PDB parsing must expose that platform constraint through the existing base classes and tests.

## Prepare functional test assets

Rule test assets live under [`FunctionalTestData`](../src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestData) in a directory named `RuleId.FriendlyName`, matching the rule ID and class name.

Add the applicable subdirectories:

- `Pass` contains binaries that satisfy the rule.
- `Fail` contains binaries that produce the intended warning or error.
- `NotApplicable` contains rule-specific binaries that exercise applicability boundaries. Shared applicability fixtures may be reused where appropriate.

Passing and failing binaries should differ only in conditions relevant to the rule. Enable unrelated mitigations so a fixture does not introduce noise into broader test runs.

Check in the fixture source and a deterministic build script beside newly added binaries whenever possible. The build path should identify the required toolchain, architecture, configuration, and meaningful compiler or linker options. If exact reproduction depends on external state such as a signing certificate or unavailable toolchain, document that constraint in the affected rule directory rather than recording an unverified historical recipe.

## Add functional rule tests

Add focused methods to [`RuleTests.cs`](../src/Test.FunctionalTests.BinSkim.Rules/RuleTests.cs). Its helpers derive the test-data directory from the rule class name and `RuleIds` entry.

- Use `VerifyPass` for the `Pass` directory.
- Use `VerifyFail` for the `Fail` directory.
- Use `VerifyApplicability` or the condition-specific helper for applicability boundaries.
- Use platform guards for rules that cannot execute on every test platform.
- Add dedicated tests for behavior that the directory-driven helpers cannot express, such as parser failures or files removed during analysis.

Run the affected test project with a filter for the rule ID while iterating:

```console
dotnet test src/Test.FunctionalTests.BinSkim.Rules/Test.FunctionalTests.BinSkim.Rules.csproj -c Release --filter "FullyQualifiedName~RULE_ID"
```

## Review driver baselines

Changes to rule applicability, messages, levels, or output can affect the driver SARIF baselines under [`BaselineTestData`](../src/Test.FunctionalTests.BinSkim.Driver/BaselineTestData).

From `src\Test.FunctionalTests.BinSkim.Driver`, regenerate the platform baseline:

```powershell
.\UpdateBaselines.ps1
```

```bash
./UpdateBaselines.sh
```

Windows expectations are stored under `Expected`. Non-Windows expectations are stored under `NonWindowsExpected`. Review every changed SARIF file and include only intentional output changes.

## Regenerate rule documentation

`BuildAndTest.cmd` performs the full Windows validation workflow and regenerates [`BinSkimRules.md`](BinSkimRules.md) from rule metadata. Review the generated diff whenever descriptions, messages, levels, default enablement, or configuration change.

Do not edit generated rule documentation directly.

## Completion checklist

- The approved rule specification matches the implementation.
- Rule IDs, resources, implementation, and generated documentation agree.
- Applicability covers all unsupported target conditions.
- Pass, fail, and relevant non-applicable cases have representative assets.
- Test-asset provenance is recorded beside the assets.
- Focused rule tests pass on supported platforms.
- Windows and non-Windows baseline changes are intentional.
- Full validation completes successfully.
