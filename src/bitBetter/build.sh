#!/bin/bash

set -e
set -x

dotnet restore
dotnet publish -c Release -o bin/Release/net8.0/publish
