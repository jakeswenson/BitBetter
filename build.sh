#!/bin/bash
set -e

# detect buildx, set -e will ensure the script stops execution if not found
docker buildx version

# Enable BuildKit for better build experience and to ensure platform args are populated
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

# define temporary directory
TEMPDIRECTORY="$PWD/temp"

# define services to patch
COMPONENTS=("Api" "Identity")

# delete old directories / files if applicable
if [ -d "$TEMPDIRECTORY" ]; then
	rm -rf "$TEMPDIRECTORY"
fi

if [ -f "$PWD/src/licenseGen/Core.dll" ]; then
	rm -f "$PWD/src/licenseGen/Core.dll"
fi

if [ -f "$PWD/src/licenseGen/cert.pfx" ]; then
	rm -f "$PWD/src/licenseGen/cert.pfx"
fi

if [ -f "$PWD/src/bitBetter/cert.cer" ]; then
	rm -f "$PWD/src/bitBetter/cert.cer"
fi

if [ -f "$PWD/.keys/cert.cert" ]; then
	mv "$PWD/.keys/cert.cert" "$PWD/.keys/cert.cer"
fi

# generate keys if none are available
if [ ! -d "$PWD/.keys" ]; then
	./generateKeys.sh
fi

# copy the key to bitBetter
cp -f "$PWD/.keys/cert.cer" "$PWD/src/bitBetter"

# build bitBetter and clean the source directory after
docker build --no-cache -t bitbetter/bitbetter "$PWD/src/bitBetter"
rm -f "$PWD/src/bitBetter/cert.cer"

# gather all running instances, cannot run a wildcard filter on Ancestor= :(, does find all where name = *bitwarden*
OLDINSTANCES=$(docker container ps --all -f Name=bitwarden --format '{{.ID}}')

# stop and remove all running instances
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker stop $INSTANCE
	docker rm $INSTANCE
done

# update bitwarden itself
if [ "$1" = "update" ]; then
	docker pull ghcr.io/bitwarden/lite:latest
else
	read -p "Update (or get) bitwarden source container (y/n): "
	if [[ $REPLY =~ ^[Yy]$ ]]; then
		docker pull ghcr.io/bitwarden/lite:latest
	fi
fi

# stop and remove previous existing patch(ed) container
OLDINSTANCES=$(docker container ps --all -f Ancestor=bitwarden-patched --format '{{.ID}}')
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker stop $INSTANCE
	docker rm $INSTANCE
done
OLDINSTANCES=$(docker image ls bitwarden-patched --format '{{.ID}}')
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker image rm $INSTANCE
done

# remove old extract containers
OLDINSTANCES=$(docker container ps --all -f Name=bitwarden-extract --format '{{.ID}}')
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker stop $INSTANCE
	docker rm $INSTANCE
done

# start a new bitwarden instance so we can patch it
PATCHINSTANCE=$(docker run -d --name bitwarden-extract ghcr.io/bitwarden/lite:latest)

# create our temporary directory
mkdir $TEMPDIRECTORY

# extract the files that need to be patched from the services that need to be patched into our temporary directory
for COMPONENT in ${COMPONENTS[@]}; do
	mkdir "$TEMPDIRECTORY/$COMPONENT"
	docker cp $PATCHINSTANCE:/app/$COMPONENT/$COMPONENT "$TEMPDIRECTORY/$COMPONENT/$COMPONENT"
	docker cp $PATCHINSTANCE:/etc/supervisor.d/${COMPONENT,,}.ini "$TEMPDIRECTORY/${COMPONENT,,}.ini"
	cp "$PWD/src/bitBetter/runtimeconfig.json" "$TEMPDIRECTORY/$COMPONENT/$COMPONENT.runtimeconfig.json"
done

# stop and remove our temporary container
docker stop bitwarden-extract
docker rm bitwarden-extract

# run bitBetter, this applies our patches to the required files
docker run -v "$TEMPDIRECTORY:/app/mount" --rm bitbetter/bitbetter

# create a new image with the patched files
if [ -f "$PWD/Dockerfile-bitwarden-patch" ]; then
	rm -f "$PWD/Dockerfile-bitwarden-patch"
fi
echo "FROM ghcr.io/bitwarden/lite:latest" >> "$PWD/Dockerfile-bitwarden-patch"
for COMPONENT in ${COMPONENTS[@]}; do
	echo "" >> "$PWD/Dockerfile-bitwarden-patch"
	echo "RUN rm -f /app/$COMPONENT/$COMPONENT" >> "$PWD/Dockerfile-bitwarden-patch"
	echo "COPY ./temp/${COMPONENT,,}.ini /etc/supervisor.d/${COMPONENT,,}.ini" >> "$PWD/Dockerfile-bitwarden-patch"
	echo "COPY ./temp/$COMPONENT/ /app/$COMPONENT/" >> "$PWD/Dockerfile-bitwarden-patch"
done
docker build . --tag bitwarden-patched --file "$PWD/Dockerfile-bitwarden-patch"
rm -f "$PWD/Dockerfile-bitwarden-patch"

# start all user requested instances
if [ -f "$PWD/.servers/serverlist.txt" ]; then
	# convert line endings to unix
	sed -i 's/\r$//' "$PWD/.servers/serverlist.txt"
	cat "$PWD/.servers/serverlist.txt" | while read -r LINE; do
		if [[ $LINE != "#"* ]]; then
			bash -c "$LINE"
		fi
	done
fi

# remove our bitBetter image
docker image rm bitbetter/bitbetter

# copy our patched library to the licenseGen source directory
cp -f "$TEMPDIRECTORY/Identity/Core.dll" "$PWD/src/licenseGen"
cp -f "$PWD/.keys/cert.pfx" "$PWD/src/licenseGen"

# build the licenseGen
docker build -t bitbetter/licensegen "$PWD/src/licenseGen"

# clean the licenseGen source directory
rm -f "$PWD/src/licenseGen/Core.dll"
rm -f "$PWD/src/licenseGen/cert.pfx"

# remove our temporary directory
rm -rf "$TEMPDIRECTORY"
