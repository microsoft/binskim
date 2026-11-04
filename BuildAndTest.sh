#!/bin/bash
Configuration=$1

if [[ -z $Configuration ]]; then
  Configuration=Release
fi

exec dotnet test --solution src/BinSkim.sln -c "$Configuration" --report-trx --results-directory bld/TestResults