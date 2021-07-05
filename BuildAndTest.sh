#!/bin/bash

dotnet build src/*.sln --configuration Release
dotnet test src/*.sln --configuration Release --no-build