Sha1SignedUntrustedRoot.exe is a SHA1 signed binary with an untrusted root.

Produced by the following technique:

  makecert.exe -r -pe -ss BinSkimTestCertStore -n "CN=BinSkimTest" BinSkim.cer
  signtool.exe sign /s BinSkimTestCertStore /n BinSkim Sha1SignedUntrustedRoot.exe 
