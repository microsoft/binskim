# Known Issues

## **BA2022**: [SignSecurely](https://github.com/microsoft/binskim/blob/main/src/BinSkim.Rules/PERules/BA2022.SignSecurely.cs)

+ *Problem*: BA2022 is reported to raise a false positive in some contexts, indicating that a binary is signed with an insecure algorithm (typically SHA1) when examining the file manually (or rerunning the tool) shows that it is signed with a secure algorithm. This check calls directly into the Windows operating system WinTrustVerify API to perform its work.
+ *Causes*: The root cause for this problem has not been established. The following problems could be in play:
  + Transient failure in signing service.
  + API failure due to low environment conifguration or available resources.
+ *Resolution*: Rerun the analysis. If analysis succeeds, request an exception for the issue or otherwise proceed with your engineering process.
+ *Advanced guidance*: To further troubleshoot this problem, it would be helpful to have hashing enabled on the BinSkim command-line (--hashes) and for a user to have access to the actual machine environment that produced the false positive. We have not received confirmation yet that BinSkim reports inconsistent results against precisely the same binary in precisely the same runtime environment. This information would be helpful in narrowing the problem. This bug is currently tracked as issue [#243](https://github.com/microsoft/binskim/issues/243) on the BinSkim repository.
