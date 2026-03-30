---
applyTo: "**"
---

## Feature Instructions
We have a problem with DwarfMemoryReader.ReadBytes() where it can throw an IndexOutOfRangeException if the caller tries to read more bytes than remain in the buffer. This can happen if the DWARF data is truncated or malformed. We need to add bounds checking to prevent this exception and handle the error gracefully.
 Are There Other Similar Errors? ⚠️ Yes

  The following methods in DwarfMemoryReader access Data[Position] without bounds checking:
  
┌──────────┬──────────────────────────┬───────────────────────────────────────────────────────────────┐
  │ Line     │ Method                   │ Risk                                                          │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 77       │ Peek()                   │ IndexOutOfRangeException if Position at end                   │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 143      │ ReadByte()               │ IndexOutOfRangeException if Position at end                   │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 211, 230 │ ULEB128(), SLEB128()     │ IndexOutOfRangeException in while loop if data is truncated   │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 151      │ ReadUshort()             │ Marshal.ReadInt16 past buffer if <2 bytes remain              │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 170      │ ReadUint()               │ Marshal.ReadInt32 past buffer if <4 bytes remain              │
  ├──────────┼──────────────────────────┼───────────────────────────────────────────────────────────────┤
  │ 181      │ ReadUlong()              │ Marshal.ReadInt64 past buffer if <8 bytes remain

## Testing Configuration
