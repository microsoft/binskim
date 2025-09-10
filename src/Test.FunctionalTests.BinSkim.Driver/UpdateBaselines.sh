#!/bin/bash

ScriptDir=`dirname $0`

TOOL="BinSkim"
repoRoot=`readlink -f $ScriptDir/../../`
BuildOutputPath="$repoRoot/bld/bin/AnyCPU_Release/net9.0/linux-x64"
PROJECT="$repoRoot/src/BinSkim.Driver/BinSkim.Driver.csproj"

BuildTool () 
{
    # Linux specific for now.
    echo "Building BinSkim..."
    dotnet build $PROJECT -c Release --framework net9.0 --runtime linux-x64
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

        echo "$TOOLPATH analyze $targetFile --output $outputTemp --kind 'Fail;Pass' --level 'Error;Warning;Note' --remove NondeterministicProperties --config default --quiet true --enlistmentRoot $repoRoot --log ForceOverwrite"
        $TOOLPATH analyze $targetFile --output $outputTemp --kind 'Fail;Pass' --level 'Error;Warning;Note' --insert Hashes --remove NondeterministicProperties --config default --quiet true --enlistmentRoot $repoRoot --log ForceOverwrite

        mv $outputTemp $output
    done
}

BuildTool
RunBaseline *.dll
RunBaseline *.exe
RunBaseline gcc.*
RunBaseline clang.*
RunBaseline macho.*