
---
applyTo: "**"
---
## Feature Instructions
this two ocmands you will be runnign for test this: 
.\buildAndTest.cmd 
binskim analyze "C:\Users\sraroseck\OneDrive - Microsoft\01Project\BinSkim ERR998\libmsalruntime-brokenFIle.so" 
---- 
I need to fix the error ERR998. in the comamnd line. 
---- 
BinSkim get's broken by DWARF5 *.so file
- Original support post: [Link to Teams Post](https://teams.microsoft.com/l/message/19:8af096ab499d496799ffa3e0c34f1f3d%40thread.tacv2/1767623815103?groupId=886b1d22-e648-4492-af5f-6fbe81e73d4a&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47&createdTime=1767623815103&parentMessageId=1767623815103) (with linkt to pipelines etc)
- Broken file attached to ticket.    
- Binskim configuration triggering the error as well.
## repro
Just copy the *.so file to some temp folder, run guardian init, copy binskimall.gdnconfig to .gdn/e and then run guardian run -c binskimall  
  
- BinSkim 1.9.5 works
- BinSkim 4.3.1 breaks
- BinSkim 4.8.2.1 breaks
When browsed fields with copilot drafted rust DWARF5 parser, only issues discovered were these (so we should doublecheck on BinSkim and it's handling of DWARF5 encoding, which seems to be fishy):
**Non-canonical encodings** are LEB128 values that use more bytes than necessary to represent a number.
Example: ULEB128
----------------
The value **2** can be encoded as:
*   **Canonical**: `02` (1 byte) ✓
*   **Non-canonical**: `82 00` (2 bytes) - same value, wasteful padding
The value **0** can be encoded as:
*   **Canonical**: `00` (1 byte) ✓
*   **Non-canonical**: `80 00` (2 bytes) - unnecessary continuation byte
Why it matters
--------------
| Aspect | Impact |
| --- | --- |
| **Correctness** | Non-canonical encodings are technically valid and decode correctly |
| **Spec compliance** | DWARF spec doesn't require canonical encoding, but recommends it |
| **Size** | Wastes space in debug sections |
| **Security** | Can be used to hide data or evade signature-based detection |
| **Tooling** | Some parsers may behave unexpectedly with non-canonical values |
