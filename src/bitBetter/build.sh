#!/bin/bash

set -e
set -x

dotnet restore
dotnet publish
