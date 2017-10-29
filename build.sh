#!/bin/bash

mkdir ./src/bitBetter/.keys

cp .keys/cert.cert ./src/bitBetter/.keys

cd ./src/bitbetter

dotnet restore
dotnet publish

docker build --pull . -t bitbetter/api # --squash

