#!/bin/sh
set -e
DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`
BW_VERSION=$(curl -sL https://go.btwrdn.co/bw-sh-versions | grep '^ *"'coreVersion'":' | awk -F\: '{ print $2 }' | sed -e 's/,$//' -e 's/^"//' -e 's/"$//')

echo "Building BitBetter for BitWarden version $BW_VERSION"

# Enable BuildKit for better build experience and to ensure platform args are populated
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

# Determine host architecture to use as default BUILDPLATFORM / TARGETPLATFORM if not supplied.
# Allow override via environment variables when invoking the script.
HOST_UNAME_ARCH=$(uname -m 2>/dev/null || echo unknown)
case "$HOST_UNAME_ARCH" in
    x86_64|amd64)   DEFAULT_ARCH=amd64 ;;
    aarch64|arm64)  DEFAULT_ARCH=arm64 ;;
    armv7l|armv7)   DEFAULT_ARCH=arm/v7 ;;
    *)              DEFAULT_ARCH=amd64 ;;
esac

: "${BUILDPLATFORM:=linux/${DEFAULT_ARCH}}"
: "${TARGETPLATFORM:=linux/${DEFAULT_ARCH}}"

echo "Using BUILDPLATFORM=$BUILDPLATFORM TARGETPLATFORM=$TARGETPLATFORM"

# If there aren't any keys, generate them first.
[ -e "$DIR/.keys/cert.cert" ] || "$DIR/.keys/generate-keys.sh"

# Prepare Bitwarden server repository
rm -rf $DIR/server
git clone --branch "v${BW_VERSION}" --depth 1 https://github.com/bitwarden/server.git $DIR/server

# Replace certificate file and thumbprint
old_thumbprint=$(openssl x509 -inform DER -fingerprint -noout -in $DIR/server/src/Core/licensing.cer | cut -d= -f2 | tr -d ':')
new_thumbprint=$(openssl x509 -inform DER -fingerprint -noout -in $DIR/.keys/cert.cert | cut -d= -f2 | tr -d ':')
sed -i -e "s/$old_thumbprint/$new_thumbprint/g" $DIR/server/src/Core/Billing/Services/Implementations/LicensingService.cs
cp $DIR/.keys/cert.cert $DIR/server/src/Core/licensing.cer

docker build \
	--no-cache \
	--platform "$TARGETPLATFORM" \
	--build-arg BUILDPLATFORM="$BUILDPLATFORM" \
	--build-arg TARGETPLATFORM="$TARGETPLATFORM" \
	--label com.bitwarden.product="bitbetter" \
	-f $DIR/server/src/Api/Dockerfile \
	-t bitbetter/api \
	$DIR/server

docker build \
	--no-cache \
	--platform "$TARGETPLATFORM" \
	--build-arg BUILDPLATFORM="$BUILDPLATFORM" \
	--build-arg TARGETPLATFORM="$TARGETPLATFORM" \
	--label com.bitwarden.product="bitbetter" \
	-f $DIR/server/src/Identity/Dockerfile \
	-t bitbetter/identity \
	$DIR/server

docker tag bitbetter/api bitbetter/api:latest
docker tag bitbetter/identity bitbetter/identity:latest
docker tag bitbetter/api bitbetter/api:$BW_VERSION
docker tag bitbetter/identity bitbetter/identity:$BW_VERSION

# Remove old instances of the image after a successful build.
ids=$( docker images bitbetter/* | grep -E -v -- "CREATED|latest|${BW_VERSION}" | awk '{ print $3 }' )
[ -n "$ids" ] && docker rmi $ids || true
