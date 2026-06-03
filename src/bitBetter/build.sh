#!/bin/bash

set -e
set -x

dotnet restore
dotnet publish -c Release -o bin/Release/net10.0/publish
