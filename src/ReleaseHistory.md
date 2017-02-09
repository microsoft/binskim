# BinSkim Release History

## **v1.3.3-beta** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.3-beta) 
* Update Sarif dependency to Sarif SDK/Driver 1.5.22-beta (Sarif JSON format 1.0.0)

## **v1.3.4-beta** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.4-beta) 
* Force load PDBs in some circumstances where they have failed to do so

## **v1.3.5** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.5
* Fix false positives in 'sign securely' analysis for multi-signed binaries
* Eliminate noise in stack protection analysis against .NET native binaries
* Update Sarif dependency to 1.5.28

## **v1.3.6** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.6
* Update Sarif dependency to 1.5.36
* Improves output in error cases

## **v1.3.7** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.7
* Update Sarif dependency to 1.5.38
* More incidental reporting improvements

## **v1.3.8** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.8
* Update Sarif dependency to 1.5.40
* --config argument is now optional
* Fix false positives of BA2008:EnableControlFlowGuard firing against MC++ mixed mode binaries
* Fix false positives of BA2008:EnableControlFlowGuard firing against resource-only dll that include exported API forwarders (but no code)
* XML-based configuration now functional
* Eliminated compiler tool version false positives for Intel compiler and MASM

## **v1.3.9** [NuGet Package](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BinSkim/1.3.9
* Fix false positives of BA2008:EnableControlFlowGuard firing on x86 kernel mode binaries
* Eliminate high-entropy VA analysis for binaries with no entry points
* Update various checks to eliminate noise analyzing boot binaries

