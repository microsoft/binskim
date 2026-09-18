param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-True {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Open-Package {
    param([string] $Id)

    $path = Join-Path $PackageDirectory "$Id.$Version.nupkg"
    Assert-True (Test-Path -LiteralPath $path) "Package not found: $path"

    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    $nuspecEntry = $archive.Entries |
        Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1

    Assert-True ($null -ne $nuspecEntry) "No nuspec found in $path"
    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml] $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    return @{
        Path = $path
        Archive = $archive
        Nuspec = $nuspec
        Entries = @($archive.Entries | ForEach-Object { $_.FullName })
    }
}

function Get-Metadata {
    param([xml] $Nuspec)

    return $Nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
}

function Assert-PackageIdentity {
    param(
        [hashtable] $Package,
        [string] $Id
    )

    $metadata = Get-Metadata $Package.Nuspec
    Assert-True ($metadata.id -eq $Id) "Expected package ID '$Id', found '$($metadata.id)'."
    Assert-True ($metadata.version -eq $Version) "Expected package version '$Version', found '$($metadata.version)'."
}

$binSkim = Open-Package "Microsoft.CodeAnalysis.BinSkim"
$binaryParsers = Open-Package "Microsoft.Binary.Parsers"

try {
    Assert-PackageIdentity $binSkim "Microsoft.CodeAnalysis.BinSkim"
    Assert-True (-not ($binSkim.Entries | Where-Object { $_ -like "lib/*" })) "The BinSkim package must not contain lib assets."
    Assert-True ($binSkim.Entries -contains "tools/ReleaseHistory.md") "The BinSkim package must contain tools/ReleaseHistory.md."

    foreach ($sourceDirectory in "BinaryParsers", "BinSkim.Driver", "BinSkim.Rules", "BinSkim.Sdk") {
        Assert-True ($null -ne ($binSkim.Entries | Where-Object { $_ -like "src/$sourceDirectory/*.cs" } | Select-Object -First 1)) "The BinSkim package is missing $sourceDirectory source files."
    }

    foreach ($runtimeIdentifier in "win-x64", "linux-x64", "linux-arm64", "osx-x64") {
        $prefix = "tools/net9.0/$runtimeIdentifier/"
        Assert-True ($null -ne ($binSkim.Entries | Where-Object { $_.StartsWith($prefix, [System.StringComparison]::Ordinal) } | Select-Object -First 1)) "The BinSkim package is missing the $runtimeIdentifier payload."

        $runtimeConfigPath = "${prefix}BinSkim.runtimeconfig.json"
        $runtimeConfigEntry = $binSkim.Archive.GetEntry($runtimeConfigPath)
        Assert-True ($null -ne $runtimeConfigEntry) "The BinSkim package is missing $runtimeConfigPath."
        $reader = [System.IO.StreamReader]::new($runtimeConfigEntry.Open())
        try {
            $runtimeConfig = $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
        }

        Assert-True ($runtimeConfig.runtimeOptions.tfm -eq "net9.0") "$runtimeConfigPath must target net9.0."
    }

    Assert-PackageIdentity $binaryParsers "Microsoft.Binary.Parsers"
    Assert-True ($binaryParsers.Entries -contains "lib/net9.0/BinaryParsers.dll") "The BinaryParsers package must contain lib/net9.0/BinaryParsers.dll."

    $nuspecText = $binaryParsers.Nuspec.OuterXml
    Assert-True (-not $nuspecText.Contains(".NETFramework9.0")) "The BinaryParsers nuspec contains the invalid .NETFramework9.0 dependency group."

    $dependencyGroup = $binaryParsers.Nuspec.SelectSingleNode("//*[local-name()='dependencies']/*[local-name()='group']")
    Assert-True ($null -ne $dependencyGroup) "The BinaryParsers package is missing its dependency group."
    Assert-True ($dependencyGroup.targetFramework -eq "net9.0") "The BinaryParsers dependency group must target net9.0."

    [xml] $centralVersionsXml = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\Directory.Packages.props")
    [xml] $packageProjectXml = Get-Content -LiteralPath (Join-Path $PSScriptRoot "BinaryParsers.Package.csproj")
    [xml] $productProjectXml = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\BinaryParsers\BinaryParsers.csproj")
    $centralVersions = @{}
    foreach ($packageVersion in $centralVersionsXml.SelectNodes("/Project/ItemGroup/PackageVersion")) {
        $centralVersions[$packageVersion.Include] = $packageVersion.Version
    }

    $expectedDependencies = @{}
    foreach ($packageReference in $packageProjectXml.SelectNodes("/Project/ItemGroup/PackageReference")) {
        $expectedDependencies[$packageReference.Include] = $centralVersions[$packageReference.Include]
    }

    $productDependencies = @($productProjectXml.SelectNodes("/Project/ItemGroup/PackageReference") | ForEach-Object { $_.Include } | Sort-Object)
    $packageDependencies = @($expectedDependencies.Keys | Sort-Object)
    Assert-True (-not (Compare-Object $productDependencies $packageDependencies)) "The BinaryParsers packaging project must mirror the product project's direct PackageReferences."

    $actualDependencies = @{}
    foreach ($dependency in $dependencyGroup.SelectNodes("*[local-name()='dependency']")) {
        $actualDependencies[$dependency.id] = $dependency
    }

    Assert-True ($actualDependencies.Count -eq $expectedDependencies.Count) "The BinaryParsers package dependency count does not match its direct PackageReferences."
    foreach ($dependencyId in $expectedDependencies.Keys) {
        Assert-True ($actualDependencies.ContainsKey($dependencyId)) "The BinaryParsers package is missing dependency '$dependencyId'."
        Assert-True ($actualDependencies[$dependencyId].version -eq $expectedDependencies[$dependencyId]) "Dependency '$dependencyId' has version '$($actualDependencies[$dependencyId].version)' instead of '$($expectedDependencies[$dependencyId])'."
        Assert-True ($actualDependencies[$dependencyId].exclude -eq "Build,Analyzers") "Dependency '$dependencyId' must exclude Build and Analyzers assets."
    }

    Write-Host "Validated package contracts for version $Version."
}
finally {
    $binSkim.Archive.Dispose()
    $binaryParsers.Archive.Dispose()
}
