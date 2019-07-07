#!/bin/bash

set -e
set -x

dotnet add package Newtonsoft.Json --version 12.0.1
dotnet restore
dotnet publish
