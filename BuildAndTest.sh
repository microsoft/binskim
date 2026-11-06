#!/bin/bash
BinSkimBuildTimestampUtc="${BinSkimBuildTimestampUtc:-$(date -u +%Y%m%d%H%M%S)}" || exit 1
export BinSkimBuildTimestampUtc
Configuration=$1

if [[ -z $Configuration ]]; then
  Configuration=Release
fi

exec dotnet test src/BinSkim.sln -c $Configuration