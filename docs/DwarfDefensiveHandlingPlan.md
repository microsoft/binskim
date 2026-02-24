# DWARF4/5 Defensive Error Handling & Reading Path Plan

## Goals
- Strengthen defensive error handling in DWARF parsing for both DWARF4 and DWARF5.
- Prevent malformed or malicious DWARF data from causing BinSkim ERR998 / hard failures.
- Keep overall ELF validity separate from DWARF availability ("no DWARF" vs. "broken binary").

## Dwarf-specific error type
- Add `DwarfParseException : Exception` in the DWARF namespace (e.g., src/BinaryParsers/ElfBinary/Dwarf).
- Use it only for "malformed / unexpected DWARF data" (not for generic IO/ELF issues).

## Harden low-level DWARF reading (shared for DWARF4/5)
- In DwarfMemoryReader (src/BinaryParsers/ElfBinary/Dwarf/DwarfMemoryReader.cs):
  - Before any read (`ReadByte`, `ReadUshort`, `ReadUint`, `ReadUlong`, `ReadBlock`, `ReadString`), check that the requested bytes fit; if not, throw `DwarfParseException`.
  - In `ULEB128()`:
    - Check `Position < Data.Length` each loop.
    - Guard `shift` (e.g., stop or throw if `shift >= 64`).
    - Accept non-canonical encodings (extra continuation bytes) as long as all reads are in-bounds.
  - In `SLEB128()`:
    - Same bounds/shift checks as `ULEB128()`.
    - Keep existing sign-extension logic but throw `DwarfParseException` if the value requires reading beyond buffer end.
  - In `ReadString()`:
    - Walk until `\0` or `Data.Length`; if no terminator found before the end, throw `DwarfParseException`.

## DWARF5 string-offset handling
- In `ParseDebugStringOffsets` (DwarfSymbolProvider.cs):
  - If `debugStringOffsets` is `null` or empty → return empty `List<int>` early.
  - While reading offsets, rely on safe `ReadOffset`; catch `DwarfParseException` and break out, returning offsets collected so far (instead of crashing).

## DWARF4/5 compilation-unit header parsing
- In `DwarfCompilationUnit.ReadData` (DwarfCompilationUnit.cs):
  - After `ReadLength`, compute `endPosition` and:
    - If `endPosition > debugData.Data.Length`, throw `DwarfParseException("CU length beyond section")`.
  - Version branching:
    - `version == 5`: read `DwarfUnitType`, `addressSize`, `debugDataDescriptionOffset`, and optional split-DWARF fields; validate bounds at each step.
    - `0 < version < 5`: keep existing header logic but with the same bounds checks.
    - Otherwise: throw `DwarfParseException("Unsupported DWARF version")`.
  - If header parsing fails with `DwarfParseException`, stop reading this CU and let callers decide whether to continue with others.

## DWARF4/5 attribute decoding & safety
- In the attribute loop inside `ReadData`:
  - For each `DwarfFormat`, ensure enough bytes remain before reading; if not, throw `DwarfParseException`.
  - For `UData` / `SData`, rely on hardened `ULEB128` / `SLEB128`.
  - For string formats (`String`, `Strp`, `LineStrp`, `StrpSup`), ensure referenced offsets are within the target section; otherwise, throw `DwarfParseException`.
- Treat any malformed attribute as "CU invalid" (fail the whole CU) to keep behavior predictable and simple.

## Safer compilation-unit iteration (DWARF4/5 path)
- In `ParseAllCompilationUnits` (DwarfSymbolProvider.cs):
  - Wrap each `ParseOneCompilationUnitByOffset` in `try { … } catch (DwarfParseException) { … }`:
    - On error at current `offset`, either:
      - Stop parsing further CUs and return those already parsed, or
      - (More advanced) attempt to skip to `NextOffset` only if it is inside bounds.
  - Treat "no compilations units parsed" as "no DWARF info" rather than a driver-level fatal error.

## DWARF4/5 line-program parsing
- In `ParseLineNumberPrograms` (DwarfSymbolProvider.cs):
  - For each `new DwarfLineNumberProgram(...)`, wrap in `try/catch DwarfParseException`:
    - On error, break and return programs collected so far (or just skip this one).
- In `DwarfLineNumberProgram.ReadData` (DwarfLineNumberProgram.cs):
  - Keep the existing structural checks (`unitLength <= 1`, `version < 2`, `operationCodeBase <= 0`) that return `null`.
  - Add bounds checks around all subsequent reads; on violation, throw `DwarfParseException`.
  - For DWARF5:
    - Handle the DWARF5 header fields (`addressSize`, `segmentSelectorSize`) only when `dwarfVersion >= 5`.
    - Ensure end-of-header position does not exceed `endPosition`; otherwise, `DwarfParseException`.

## Integration with ElfBinary and higher-level behavior
- In ElfBinary.cs:
  - Inside the lazy initializers for:
    - `CompilationUnits`
    - `commandLineInfos`
    - `LineNumberPrograms`
    - `CommonInformationEntries`
  - Wrap calls to `DwarfSymbolProvider` in `try/catch DwarfParseException`:
    - On `DwarfParseException`, set an appropriate "DWARF not available" flag (e.g., `DebugFileLoaded = false`) and return an empty list.
  - Keep the outer `catch (Exception e)` only for truly fatal conditions (ELF unreadable), which set `Valid = false`.
- At the rules/driver level (DWARF-related rules):
  - Interpret "empty or invalid DWARF" as:
    - DWARF-dependent rules skip or emit a clear "DWARF unavailable" message.
    - Overall command does not throw ERR998; ERR998 is reserved for non-recoverable ELF/binary problems.

## Testing and regression coverage
- Extend ElfBinaryTests (Test.UnitTests.BinaryParsers/Elf/ElfBinaryTests.cs):
  - Add DWARF4 and DWARF5 samples with:
    - Canonical LEB128 encodings (control case).
    - Non-canonical but in-bounds LEB128 (should parse successfully).
    - Truncated DWARF sections / invalid string offsets (should not crash; should return "no DWARF" but keep ELF valid).
- Add focused tests for DwarfMemoryReader:
  - Feed minimal byte arrays that end mid-LEB128, mid-string, or mid-block to confirm `DwarfParseException` behavior and no out-of-bounds access.
