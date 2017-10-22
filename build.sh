#!/bin/bash

dotnet restore
dotnet publish
docker build . -t bitbetter/api # --squash