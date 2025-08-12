#!/bin/bash
set -e

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

if [ -f "$PWD/src/bitBetter/cert.cert" ]; then
    rm -f "$PWD/src/bitBetter/cert.cert"
fi

# generate keys if none are available
if [ ! -d "$PWD/.keys" ]; then
	./generateKeys.sh
fi

# copy the key to bitBetter and licenseGen
cp -f "$PWD/.keys/cert.cert" "$PWD/src/bitBetter"
cp -f "$PWD/.keys/cert.pfx" "$PWD/src/licenseGen"

# build bitBetter and clean the source directory after
docker build --no-cache -t bitbetter/bitbetter "$PWD/src/bitBetter"
rm -f "$PWD/src/bitBetter/cert.cert"

# gather all running instances
OLDINSTANCES=$(docker container ps --all -f Name=bitwarden --format '{{.ID}}')

# stop all running instances
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker stop $INSTANCE
	docker rm $INSTANCE
done

# update bitwarden itself
if [ "$1" = "y" ]; then
	docker pull ghcr.io/bitwarden/self-host:beta
else
	read -p "Update (or get) bitwarden source container (y/n): " -n 1 -r
	echo
	if [[ $REPLY =~ ^[Yy]$ ]]
	then
		docker pull ghcr.io/bitwarden/self-host:beta
	fi
fi

# stop and remove previous existing patch(ed) container
docker stop bitwarden-patch
docker rm bitwarden-patch
docker image rm bitwarden-patch

# start a new bitwarden instance so we can patch it
PATCHINSTANCE=$(docker run -d --name bitwarden-patch ghcr.io/bitwarden/self-host:beta)

# create our temporary directory
mkdir $TEMPDIRECTORY

# extract the files that need to be patched from the services that need to be patched into our temporary directory
for COMPONENT in ${COMPONENTS[@]}; do
	mkdir "$TEMPDIRECTORY/$COMPONENT"
	docker cp $PATCHINSTANCE:/app/$COMPONENT/Core.dll "$TEMPDIRECTORY/$COMPONENT/Core.dll"
done

# run bitBetter, this applies our patches to the required files
docker run -v "$TEMPDIRECTORY:/app/mount" --rm bitbetter/bitbetter

# create a new image with the patched files
docker build . --tag bitwarden-patch --file "$PWD/src/bitBetter/Dockerfile-bitwarden-patch"

# stop and remove our temporary container
docker stop bitwarden-patch
docker rm bitwarden-patch

# copy our patched library to the licenseGen source directory
cp -f "$TEMPDIRECTORY/Identity/Core.dll" "$PWD/src/licenseGen"

# remove our temporary directory
rm -rf "$TEMPDIRECTORY"

# start all user requested instances
sed -i 's/\r$//' "$PWD/.servers/serverlist.txt"
cat "$PWD/.servers/serverlist.txt" | while read -r LINE; do
	bash -c "$LINE"
done

# remove our bitBetter image
docker image rm bitbetter/bitbetter

# build the licenseGen
docker build -t bitbetter/licensegen "$PWD/src/licenseGen"

# clean the licenseGen source directory
rm -f "$PWD/src/licenseGen/Core.dll"
rm -f "$PWD/src/licenseGen/cert.pfx"
