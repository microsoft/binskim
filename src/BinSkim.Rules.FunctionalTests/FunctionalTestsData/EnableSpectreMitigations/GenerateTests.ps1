
param(
[ValidateNotNullOrEmpty()][string]$TargetMachineArch
)

class TestVariant
{
    [ValidateNotNullOrEmpty()][string]$Name
    [string]$ClSwitches
    [bool]$PassExpected

    TestVariant($Name, $ClSwitches, $PassExpected) 
    {
       $this.Name = $Name
       $this.ClSwitches = $ClSwitches
       $this.PassExpected = $PassExpected
    }
}

function CompileTest()
{
    param(
    [string]$CompilerExe,
    [string]$CompilerName,
    [TestVariant]$Variant,
    [string]$TargetPlatform
    )
   
    $outputdir = ".\Fail"

    if ($Variant.PassExpected -eq $true)
    {
        $outputdir = ".\Pass"
    }
    
    if ((Test-Path -Path $outputdir) -eq $false)
    {    
        New-Item -ItemType directory -Path $outputdir
    }

    $BinName = $CompilerName + "_" + $Variant.Name + "_" + $TargetPlatform
    

    $args = "/Z7 /LD " + $variant.ClSwitches + " /Fo:$outputdir\$BinName.obj /Fe:$outputdir\$BinName.dll"+ " " + $inputFile + " /link /MACHINE:$TargetPlatform"
    
    Write-Host "$CompilerExe $args"

    # wait on process to facilitate clean up of temp files later
    Start-Process $CompilerExe -ArgumentList $args -NoNewWindow -wait
}

function FindFirstInstanceOnPath()
{
    param(
    [ValidateNotNullOrEmpty()][string]$Compiler
    )

    $matches = @()

    foreach($testpath in $env:Path.Split(';'))
    {
        if($testpath) 
        {
            $testfile = join-path $testpath $Compiler
            if(test-path -Path $testfile)
            {
                $matches += $testfile
            }
        }
    }

    if ($matches.Length -eq 0)
    {
        return $null
    }
    else
    {
        return $matches[0]
    }
}


<# 
My set of questions for test binaries would be:

1)	Do we correctly test against the correct major / minor version in the list
2)	Do we correctly detect a build/revision below but matching major/minor version of a listed version and correctly recognize it as not supporting the switches
3)	Do we correctly detect an unsupported minor version
4)	Do we correctly detect an unsupported major version
# These will depend on producing binaries with the correct set of compilers

5)	Do we independently recognize the compiler switches as valid when thrown
# Will require compiler drops that support /QSpectre 

6)	Do we correctly fail for a explicitly disabled switch
7)	Do we correctly pass for multiple switch uses where enabled wins (last)
8)	Do we correctly error for multiple switch uses where disabled wins (last)

9)	Do we correctly fail for ambiguous switch (use of both that disagree on enabled / disabled) 
# This isn't necessary at the moment as 
# a) /d2guardspecload and /Qspectre alias eachother
# b) We now should process commandlines in a more sophisticated fashion that should cope with most of this (/Od /O1 /O1- is a case that we don't currently cope with)

10)	Do we correctly identify MASM and issue a warning
# Just about every non-trivial binary that is linking in the CRT is going to have asm files from the CRTs, so this comes down to the exclusion list testing

11)	Do we correctly identify empty contributions from .lib
12)	Do we correctly identify an unsupported compiler (CLANG / LLVM) 
# NYI

13)	Do we correctly identify linked in MSIL code for mixed mode binaries for linked in netmodules
#NYI

14) Do we correctly identify mitigations disabled / enabled by optimization options
# This will depend on compiler versions

15) Do we correctly identify functions where mitigations are disabled via  
#>

#Simple compile for version checking (Questions 1 through 4)
$VersionCheck = @()
$VersionCheck += [TestVariant]::new("VersionCheck_NoFlags", "/O1", $false)

#d2guardspecload variations (Questions 5 through 8)
$Supportsd2guardspecload = @()
$Supportsd2guardspecload += [TestVariant]::new("d2guardspecload_enabled", "/O1 /d2guardspecload", $true)
$Supportsd2guardspecload += [TestVariant]::new("d2guardspecload_disabled", "/O1 /d2guardspecload-", $false)
$Supportsd2guardspecload += [TestVariant]::new("d2guardspecload_disabled_enabled", "/O1 /d2guardspecload- /d2guardspecload", $true)
$Supportsd2guardspecload += [TestVariant]::new("d2guardspecload_enabled_disabled", "/O1 /d2guardspecload /d2guardspecload-", $false)

#Qspectre variations (Questions 5 through 8)
$SupportsSpectre = @()
$SupportsSpectre += [TestVariant]::new("Qspectre", "/O1 /Qspectre", $true)
$SupportsSpectre += [TestVariant]::new("Qspectre_disabled", "/O1 /Qspectre-", $false)
$SupportsSpectre += [TestVariant]::new("Qspectre_disabled_enabled", "/O1 /Qspectre- /Qspectre", $true)
$SupportsSpectre += [TestVariant]::new("Qspectre_enabled_disabled", "/O1 /Qspectre /Qspectre-", $false)

#Qspectre/d2guardspecload variations (Questions 5 through 8)
$SupportsBoth = @()
$SupportsBoth += [TestVariant]::new("Qspectre_enabled_d2guardspecload_enabled", "/O1 /Qspectre /d2guardspecload", $true)
$SupportsBoth += [TestVariant]::new("Qspectre_enabled_d2guardspecload_disabled", "/O1 /Qspectre /d2guardspecload-", $false)
$SupportsBoth += [TestVariant]::new("Qspectre_disabled_d2guardspecload_enabled", "/O1 /Qspectre- /d2guardspecload", $true)
$SupportsBoth += [TestVariant]::new("d2guardspecload_enabled_Qspectre_disabled", "/O1 /d2guardspecload /Qspectre-", $false)

#Question 9 - unneeded now
#Question 10 - Almost every non-trivial C/C++ binary will have ASM from the CRT - we will need to add some excluded functions in testing 
#              Or have a list of well known contributions (MD5 hash check?)
#Question 11 / 12 / 13 - Different compilers needed so not for this script

#Optimization Flags testing (Question 14)
$OptimizationFlagVariants = @()
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_DefaultOpt", "/d2guardspecload", $false)
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_Od", "/d2guardspecload /Od", $false)
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_Og", "/d2guardspecload /Og", $true)
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_Ox", "/d2guardspecload /Ox", $true)
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_O1", "/d2guardspecload /O1", $true)
$OptimizationFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_O2", "/d2guardspecload /O2", $true)


#Optimization Flags testing (Question 14 variant of Questions 7 and 8)
$MultiOptFlagVariants = @()
$MultiOptFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_O1Od", "/d2guardspecload /O1 /Od", $false)
$MultiOptFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_OdO1", "/d2guardspecload /Od /O1", $true)
$MultiOptFlagVariants += [TestVariant]::new("d2guardspecloadOptimizationFlags_OdO1Od", "/d2guardspecload /Od /O1 /Od", $false)

#Function level disabled (Question 15)
$FunctionMitigationVariants = @()
$FunctionMitigationVariants += [TestVariant]::new("d2guardspecload_declspec_detection", "/d2guardspecload /O2 /DTESTDISABLESPECTREBYDECLSPEC", $false)
$FunctionMitigationVariants += [TestVariant]::new("d2guardspecload_pragmaoptoff_detection", "/d2guardspecload /O2 /DTESTDISABLESPECTREBYPRAGMA", $false)

class CL_Version
{
    #[ValidateNotNullOrEmpty()][string]$Version
    [Version]$Version
    [bool]$Supportsd2guardspecload
    [bool]$SupportsQspectre

    CL_Version($Version, $Supportsd2guardspecload, $SupportsQspectre) 
    {
       $this.Version = $Version
       $this.Supportsd2guardspecload = $Supportsd2guardspecload
       $this.SupportsQspectre = $SupportsQspectre
    }
}

#compiler versions are complex and a bit confusing
$ClVersions = @()
$ClVersions += [CL_Version]::new("14.13.26115.0", $true, $false)
$ClVersions += [CL_Version]::new("14.13.26029.0", $true, $false)
$ClVersions += [CL_Version]::new("14.12.25830.0", $true, $false)
$ClVersions += [CL_Version]::new("0.0.0.0", $false, $false)

$inputfile = ".\donkey.cpp"

# Get the version of cl.exe
$cl = FindFirstInstanceOnPath "cl.exe"

if ($cl -ne $null)
{
    $ProdVer = (Get-Command $cl).FileVersionInfo.ProductVersion
    
    Write-Host "Generating tests for cl.exe version $ProdVer"
    
    #Get the features for this compiler
    $clVersion = $null

    foreach ($clversion in $ClVersions)
    {
        $ver = $clVersion.Version
        # Write-Host "Checking if  $ProdVer -ge $ver"

        if($ProdVer.FileMajorPart -eq $ver.FileMajorPart -and 
           $ProdVer.FileMinorPart -eq $ver.FileMinorPart -and 
           $ProdVer -ge $ver)
        {
            Write-Host "Checking against functionality in $ver"
            break;
        }
    }

    $dospectre = $false
    $dod2guardspecload = $false

    if ($clVersion -ne $null)
    {
        $dospectre = $clVersion.SupportsQspectre
        $dod2guardspecload = $clVersion.Supportsd2guardspecload
    }
    else
    {
        Write-Host "No specific version found - assuming no support for Spectre mitigations, generating default set of tests"
    }

    $TestsToGenerate = @()
    #always generate the version check
    $TestsToGenerate += $VersionCheck

    if ($dod2guardspecload)
    {
        $TestsToGenerate += $Supportsd2guardspecload
        $TestsToGenerate += $OptimizationFlagVariants
        $TestsToGenerate += $MultiOptFlagVariants
        # NYI in checker
        $TestsToGenerate += $FunctionMitigationVariants
    }

    if ($dospectre)
    {
        $TestsToGenerate += $SupportsSpectre

        if ($dod2guardspecload)
        {
            $TestsToGenerate += $SupportsBoth
        }
    }

    Write-Host "Generating tests for $TargetMachineArch architecture"
    Foreach($variant in $TestsToGenerate)
    {
        CompileTest "cl.exe" "CL_$ProdVer" $variant $TargetMachineArch
    }

    #Clean up temp files
    Remove-Item * -include *.ilk -Recurse
    Remove-Item * -include *.exp -Recurse
    Remove-Item * -include *.obj -Recurse
    Remove-Item * -include *.lib -Recurse
}
else
{
    Write-Host "Compiler not found"
    return -1
}

