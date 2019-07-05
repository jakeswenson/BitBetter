#!/bin/bash

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# If there aren't any keys, generate them first.
[ -e "$DIR/.keys/cert.cert" ] || "$DIR/.keys/generate-keys.sh"

[ -e "$DIR/src/bitBetter/api/.keys" ]  || mkdir "$DIR/src/bitBetter/api/.keys"
[ -e "$DIR/src/bitBetter/identity/.keys" ]  || mkdir "$DIR/src/bitBetter/identity/.keys"

cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/api/.keys"
cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/identity/.keys"

docker run -v "$DIR/src/bitBetter:/bitBetter" -w=/bitBetter mcr.microsoft.com/dotnet/core/sdk:2.1 sh build.sh

cp -r "$DIR/src/bitBetter/bin" "$DIR/src/bitBetter/api/"
cp -r "$DIR/src/bitBetter/bin" "$DIR/src/bitBetter/identity/"

docker build -t bitbetter/api "$DIR/src/bitBetter/api" # --squash
docker build -t bitbetter/identity "$DIR/src/bitBetter/identity" # --squash
