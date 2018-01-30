#!/bin/bash

ScriptDir=`dirname $0`

TOOL="BinSkim"
repoRoot=`readlink -f $ScriptDir/../../`
BuildOutputPath="$repoRoot/bld/bin/AnyCPU_Release/netcoreapp2.0/linux-x64"
PROJECT="$repoRoot/src/BinSkim.Driver/BinSkim.Driver.csproj"

BuildTool () 
{
    # Linux specific for now.
    echo "Building BinSkim..."
    dotnet build $PROJECT -c Release --framework netcoreapp2.0 --runtime linux-x64
}

RunBaseline () 
{
    TOOLPATH="$BuildOutputPath/$TOOL"
    expectedDirectory="$ScriptDir/BaselineTestsData/NonWindowsExpected"
    mkdir -p $expectedDirectory

    for targetFile in $ScriptDir/BaselineTestsData/$1; do
        echo "Analyzing $targetFile"
        input=$targetFile
        outputFile=`basename $targetFile`
        output="$expectedDirectory/$outputFile.sarif"
        outputTemp="$output.temp"

        echo "$TOOLPATH analyze $targetFile -o $outputTemp --pretty --verbose --config default"
        $TOOLPATH analyze $targetFile -o $outputTemp --pretty --verbose --config default

        # Normalize paths--replace the repository root with '/'
        echo "Normalizing file output"
        sed s#$repoRoot/#Z:/#g $outputTemp -i

        # Potential future work--remove stack traces/etc., similar to the powershell script.
        # At the moment, BinSkim doesn't include stack traces, and the comparison shouldn't 
        # compare times--so this isn't strictly necessary.

        mv $outputTemp $output
    done
}

BuildTool
RunBaseline *.dll
RunBaseline *.exe
RunBaseline gcc.*
RunBaseline clang.*