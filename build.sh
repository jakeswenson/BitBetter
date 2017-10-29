#!/bin/bash

mkdir ./src/bitbetter/.keys

cp .keys/cert.cert ./src/bitbetter/.keys

cd ./src/bitbetter

dotnet restore
dotnet publish

docker build --pull . -t bitbetter/api # --squash

