#!/bin/bash
Configuration=$1

if [[ -z $Configuration ]]; then
  Configuration=Release
fi

exec dotnet test src/BinSkim.sln -c $Configuration