#!/bin/sh
set -e
DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

# Check prerequisite libraries and executables
for cmd in docker curl openssl jq; do
    command -v "$cmd" >/dev/null 2>&1 || { echo "Error: '$cmd' is required but not installed." >&2; exit 1; }
done
if [ "${BITBETTER_BUILD_FROM_SOURCE:-0}" = "1" ]; then
    command -v git >/dev/null 2>&1 || { echo "Error: 'git' is required for BITBETTER_BUILD_FROM_SOURCE=1 but not installed." >&2; exit 1; }
fi
BW_VERSION=$(curl -fsSL https://raw.githubusercontent.com/bitwarden/self-host/refs/heads/main/version.json | jq -er '.versions.coreVersion')

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

if [ "${BITBETTER_BUILD_FROM_SOURCE:-0}" = "1" ]; then
	echo "--- Source build mode ---"

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
else
	echo "--- Fast patch mode ---"

	mkdir -p "$DIR/src/bitBetter/.keys"
	cp "$DIR/.keys/cert.cert" "$DIR/src/bitBetter/.keys/cert.cert"

	# Build the patcher tool inside the SDK container
	docker run --rm \
		-v "$DIR/src/bitBetter:/bitBetter" \
		-w /bitBetter \
		mcr.microsoft.com/dotnet/sdk:10.0 sh build.sh

	docker build \
		--no-cache \
		--platform "$TARGETPLATFORM" \
		--label com.bitwarden.product="bitbetter" \
		--build-arg BITWARDEN_TAG="ghcr.io/bitwarden/api:$BW_VERSION" \
		-t bitbetter/api \
		"$DIR/src/bitBetter"

	docker build \
		--no-cache \
		--platform "$TARGETPLATFORM" \
		--label com.bitwarden.product="bitbetter" \
		--build-arg BITWARDEN_TAG="ghcr.io/bitwarden/identity:$BW_VERSION" \
		-t bitbetter/identity \
		"$DIR/src/bitBetter"
fi

docker tag bitbetter/api bitbetter/api:latest
docker tag bitbetter/identity bitbetter/identity:latest
docker tag bitbetter/api bitbetter/api:$BW_VERSION
docker tag bitbetter/identity bitbetter/identity:$BW_VERSION

# Remove old instances of the image after a successful build.
ids=$( docker image ls --format="{{ .ID }} {{ .Tag }}" 'bitbetter/*' | grep -E -v -- "CREATED|latest|${BW_VERSION}" | awk '{ ids = (ids ? ids FS $1 : $1) } END { print ids }' )
[ -n "$ids" ] && docker rmi $ids || true
