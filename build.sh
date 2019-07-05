#!/bin/bash

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# If there aren't any keys, generate them first.
[ -e "$DIR/.keys/cert.cert" ] || "$DIR/.keys/generate-keys.sh"

[ -e "$DIR/src/bitBetter/api/.keys" ]  || mkdir "$DIR/src/bitBetter/api/.keys"
[ -e "$DIR/src/bitBetter/identity/.keys" ]  || mkdir "$DIR/src/bitBetter/identity/.keys"

cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/api/.keys"
cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/identity/.keys"

cd "$DIR/src/bitBetter"

dotnet restore
dotnet publish

cp -r ./bin ./api/
cp -r ./bin ./identity/

docker build -t bitbetter/api ./api # --squash
docker build -t bitbetter/identity ./identity # --squash
