#!/bin/bash

# If there aren't any keys, generate them first.
[ -e ./.keys/cert.cert ] || ./.keys/generate-keys.sh

[ -e ./src/bitBetter/api/.keys ]  || mkdir ./src/bitBetter/api/.keys
[ -e ./src/bitBetter/identity/.keys ]  || mkdir ./src/bitBetter/identity/.keys

cp .keys/cert.cert ./src/bitBetter/api/.keys
cp .keys/cert.cert ./src/bitBetter/identity/.keys

cd ./src/bitBetter

dotnet restore
dotnet publish

cp -r bin/ api/
cp -r bin/ identity/

cd ./api
docker build --pull . -t bitbetter/api # --squash

cd ../identity
docker build --pull . -t bitbetter/identity # --squash
