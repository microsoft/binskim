# DWARF / DwarfMemoryReader Learnings

## Current Work Snapshot
- Branch: `main`, tracking `origin/main`; no local commits ahead of origin (nothing to push).
- Local, uncommitted changes are present in DWARF-related code and tests, plus repo guidance files.

### Modified Files (Uncommitted)
- .github/03-feature.instructions.md
  - Expanded feature description around DwarfMemoryReader bounds checking and other risky methods.
- src/BinaryParsers/ElfBinary/Dwarf/DwarfMemoryReader.cs
  - Added `EnsureAvailable(uint bytesToRead)` helper for bounds checking.
  - Updated `Peek`, `ReadStructure<T>`, `ReadByte`, `ReadUshort`, `ReadUint`, `ReadUlong`, `ULEB128`, and `SLEB128` to call `EnsureAvailable` or otherwise guard against reading past the end of the DWARF buffer.
  - Now throws `InvalidOperationException` with a clear message when truncated/malformed DWARF data would previously cause `IndexOutOfRangeException`.
- src/BinaryParsers/ElfBinary/Dwarf/DwarfSymbolProvider.cs
  - Wraps `DwarfCompilationUnit` construction in a `try`/`catch (InvalidOperationException)` and treats malformed units as "no more units" instead of failing the entire analysis.
  - Makes `ParseDebugStringOffsets` resilient to null/empty and truncated DWARF string-offset tables, stopping cleanly instead of throwing.
- src/Test.UnitTests.BinaryParsers/Dwarf/DwarfMemoryReaderTests.cs
  - Adds tests that verify the new behavior when:
    - `Peek` / `ReadByte` are called with `Position` at the end of the buffer.
    - `ReadUshort` / `ReadUint` / `ReadUlong` are called when fewer than 2/4/8 bytes remain.
    - `ULEB128` / `SLEB128` encounter a continuation bit set (0x80) but no following byte (truncated sequences).
  - Confirms `InvalidOperationException` is thrown and that `Position` is restored to the original value in truncated-LEB128 scenarios.

### New, Untracked Files
- .memory-bank/activeContext.md
  - Describes the current focus on hardening DwarfMemoryReader against truncated/malformed DWARF data.
- .memory-bank/learnings.md
  - Initializes a repository-level memory bank for DWARF-related work.

## Design Decisions
- Prefer throwing `InvalidOperationException` with clear messaging over allowing `IndexOutOfRangeException` from raw buffer access.
- When DWARF data is malformed or truncated, fail gracefully and locally (e.g., specific compilation unit or string-offset scan), rather than aborting the entire analysis.
- Ensure that methods attempting to read multi-byte values never partially advance `Position` on failure in LEB128 decoding scenarios.

## Potential Next Steps
- Run the DWARF-related unit test suite (and broader tests) to confirm behavior and catch regressions.
- Review other DWARF consumers to see if they should also catch `InvalidOperationException` and degrade gracefully.
- Add documentation notes to BinSkim user docs describing how malformed DWARF is now handled.
