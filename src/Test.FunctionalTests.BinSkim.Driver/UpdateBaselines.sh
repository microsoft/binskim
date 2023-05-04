#!/bin/bash

ScriptDir=`dirname $0`

TOOL="BinSkim"
repoRoot=`readlink -f $ScriptDir/../../`
BuildOutputPath="$repoRoot/bld/bin/AnyCPU_Release/netcoreapp3.1/linux-x64"
PROJECT="$repoRoot/src/BinSkim.Driver/BinSkim.Driver.csproj"

BuildTool () 
{
    # Linux specific for now.
    echo "Building BinSkim..."
    dotnet build $PROJECT -c Release --framework netcoreapp3.1 --runtime linux-x64
}

RunBaseline () 
{
    TOOLPATH="$BuildOutputPath/$TOOL"
    expectedDirectory="$ScriptDir/BaselineTestData/NonWindowsExpected"
    mkdir -p $expectedDirectory

    for targetFile in $ScriptDir/BaselineTestData/$1; do
        echo "Analyzing $targetFile"
        input=$targetFile
        outputFile=`basename $targetFile`
        output="$expectedDirectory/$outputFile.sarif"
        outputTemp="$output.temp"

        echo "$TOOLPATH analyze $targetFile --output $outputTemp --kind 'Fail;Pass' --level Error`;Warning`;Note --insert Hashes --remove NondeterministicProperties --config default --quiet true --sarif-output-version Current"
        $TOOLPATH analyze $targetFile --output $outputTemp --kind 'Fail;Pass' --level Error`;Warning`;Note --insert Hashes --remove NondeterministicProperties --config default --quiet true --sarif-output-version Current

        # Normalize paths--replace the repository root with '/home/user'
        echo "Normalizing file output"
        sed s#$repoRoot/#\/home\/user/#g $outputTemp -i

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
RunBaseline macho.*