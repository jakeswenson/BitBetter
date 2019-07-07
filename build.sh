#!/bin/sh

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

# If there aren't any keys, generate them first.
[ -e "$DIR/.keys/cert.cert" ] || "$DIR/.keys/generate-keys.sh"

[ -e "$DIR/src/bitBetter/.keys" ] || mkdir "$DIR/src/bitBetter/.keys"

cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/.keys"

docker run --rm -v "$DIR/src/bitBetter:/bitBetter" -w=/bitBetter mcr.microsoft.com/dotnet/core/sdk:2.1 sh build.sh

docker build --build-arg BITWARDEN_TAG=bitwarden/api -t bitbetter/api "$DIR/src/bitBetter" # --squash
docker build --build-arg BITWARDEN_TAG=bitwarden/identity -t bitbetter/identity "$DIR/src/bitBetter" # --squash


