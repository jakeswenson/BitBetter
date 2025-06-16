#!/bin/sh
set -e
DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`
BW_VERSION=$(curl -sL https://go.btwrdn.co/bw-sh-versions | grep '^ *"'coreVersion'":' | awk -F\: '{ print $2 }' | sed -e 's/,$//' -e 's/^"//' -e 's/"$//')

echo "Building BitBetter for BitWarden version $BW_VERSION"

# If there aren't any keys, generate them first.
[ -e "$DIR/.keys/cert.cert" ] || "$DIR/.keys/generate-keys.sh"

# Prepare Bitwarden repository
rm -rf $DIR/server
git clone --branch "v${BW_VERSION}" --depth 1 https://github.com/bitwarden/server.git $DIR/server
old_thumbprint=$(openssl x509 -fingerprint -noout -in $DIR/server/src/Core/licensing.cer | cut -d= -f2 | tr -d ':')
new_thumbprint=$(openssl x509 -fingerprint -noout -in $DIR/.keys/cert.cert | cut -d= -f2 | tr -d ':')
cp $DIR/.keys/cert.cert $DIR/server/src/Core/licensing.cer
# Optional, has actually no effect
sed -i -e "s/$old_thumbprint/$new_thumbprint/g" $DIR/server/src/Core/Services/Implementations/LicensingService.cs
# Enable loose files for API, so Core.dll is accessible
sed -i -e 's/PublishSingleFile=true/PublishSingleFile=false/g' $DIR/server/src/Api/Dockerfile

docker build --no-cache --label com.bitwarden.product="bitbetter" $DIR/server -f $DIR/server/src/Api/Dockerfile -t bitbetter/api
docker build --no-cache --label com.bitwarden.product="bitbetter" $DIR/server -f $DIR/server/src/Identity/Dockerfile -t bitbetter/identity

docker tag bitbetter/api bitbetter/api:latest
docker tag bitbetter/identity bitbetter/identity:latest
docker tag bitbetter/api bitbetter/api:$BW_VERSION
docker tag bitbetter/identity bitbetter/identity:$BW_VERSION

# Remove old instances of the image after a successful build.
ids=$( docker images bitbetter/* | grep -E -v -- "CREATED|latest|${BW_VERSION}" | awk '{ print $3 }' )
[ -n "$ids" ] && docker rmi $ids || true
