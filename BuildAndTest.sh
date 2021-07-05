#!/bin/bash

dotnet build src/Binskim.sln --configuration Release
dotnet test src/Binskim.sln --configuration Release --no-build