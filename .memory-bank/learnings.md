# Learnings

- Initialized learnings file; no project-specific learnings recorded yet.
- DwarfAttributeValue represents DWARF attribute values with a Type enum (DwarfAttributeValueType) and a boxed Value, plus an optional Offset used for deferred resolution.
- Equality in DwarfAttributeValue is type-sensitive and uses special handling for some enum values (Address, Constant, Reference, SecOffset, Block, ExpressionLocation, Flag, String); other enum values currently fall through to a default “equal” path regardless of Value, which is important to keep in mind when adding tests.
- DwarfCompilationUnit maps DWARF4 forms (Data*, Address, Block*, String/Strp, Flag*, Ref*, ExpressionLocation, SecOffset, etc.) and DWARF5/extended forms (LineStrp, StrpSup, Strx*/GNUStrIndex, Addrx*/GNUAddrIndex, Rnglistx, Loclistx, RefSup*, RefSig8) into DwarfAttributeValue instances by setting Type, Value, and/or Offset according to the DWARF spec and LLVM’s DWARFFormValue behavior.
## Guardian / 1ES Infrastructure
- "Guardian" in this repo refers to Microsoft 1ES SDL infrastructure agent pools (guardian-build-infra-windows-x64, guardian-build-infra-linux-x64), NOT a separate tool or NuGet package.
- 1ESPT (1 Engineering System Perf and Test) is the compliance scanning pool (pool-1espt-mseng).
- Pipeline templates come from 1ESPipelineTemplates/1ESPipelineTemplates repo (refs/tags/release): Official template for build, Unofficial for compliance.
- Compliance pipeline runs BinSkim@4 (v4.3.1), CodeQL, CredScan@2, SPMI on net9.0 release outputs.
- Build pipeline uses custom NuGet feed (BinSkim.Build on mseng/1ES), validates release PRs are merged, triggers internal BinSkimInternal pipeline (ID 21327).
- No .gdnconfig/.gdn guardian config files exist; all config is in pipeline YAML.

## Test Infrastructure
- Functional tests use xUnit + FluentAssertions on .NET 9 (netcoreapp9.0).
- Test.FunctionalTests.BinSkim.Driver uses baseline SARIF comparison (Expected/ vs NonWindowsExpected/ folders).
- Test.FunctionalTests.BinSkim.Rules uses Pass/Fail binary folders per rule (BAXXX.RuleFriendlyName pattern).
- Test assets include PE (exe/dll), ELF, Mach-O binaries from various compilers.
- UpdateBaselines.ps1 / .sh scripts regenerate expected SARIF outputs.