#!/bin/bash

mkdir ./src/bitBetter/.keys

cp .keys/cert.cert ./src/bitBetter/.keys

cd ./src/bitBetter

dotnet restore
dotnet publish

cp -r bin/ api/
cp -r bin/ identity/

cd ./api
docker build --pull . -t bitbetter/api # --squash

cd ../identity
docker build --pull . -t bitbetter/identity # --squash
