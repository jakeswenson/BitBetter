#!/bin/bash

# define temporary directory
TEMPDIRECTORY="$PWD/temp"

# define services to patch
COMPONENTS=("Api" "Identity")

# delete old directories / files if applicable
if [ -d "$TEMPDIRECTORY" ]; then
	rm -rf "$TEMPDIRECTORY"
fi

if [ -f "$PWD/licenseGen/Core.dll" ]; then
    rm -f "$PWD/licenseGen/Core.dll"
fi

if [ -f "$PWD/licenseGen/cert.pfx" ]; then
    rm -f "$PWD/licenseGen/cert.pfx"
fi

if [ -f "$PWD/bitBetter/cert.cert" ]; then
    rm -f "$PWD/bitBetter/cert.cert"
fi

# generate keys if none are available
if [ ! -d "$PWD/.keys" ]; then
	./generateKeys.sh
fi

# copy the key to bitBetter and licenseGen
cp -f "$PWD/.keys/cert.cert" "$PWD/bitBetter"
cp -f "$PWD/.keys/cert.pfx" "$PWD/licenseGen"

# build bitBetter and clean the source directory after
docker build -t bitbetter/bitbetter "$PWD/bitBetter"
rm -f "$PWD/bitBetter/cert.cert"

# gather all running instances
OLDINSTANCES=$(docker container ps --all -f Name=bitwarden --format '{{.ID}}')

# stop all running instances
for INSTANCE in ${OLDINSTANCES[@]}; do
	docker stop $INSTANCE
	docker rm $INSTANCE
done

# update bitwarden itself
read -p "Update (or get) bitwarden source container: " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
    docker pull bitwarden/self-host:beta
fi

# stop and remove previous existing patch(ed) container
docker stop bitwarden-patch
docker rm bitwarden-patch
docker image rm bitwarden-patch

# start a new bitwarden instance so we can patch it
PATCHINSTANCE=$(docker run -d --name bitwarden-patch bitwarden/self-host:beta)

# create our temporary directory
mkdir $TEMPDIRECTORY

# extract the files that need to be patched from the services that need to be patched into our temporary directory
for COMPONENT in ${COMPONENTS[@]}; do
	mkdir "$TEMPDIRECTORY/$COMPONENT"
	docker cp $PATCHINSTANCE:/app/$COMPONENT/Core.dll "$TEMPDIRECTORY/$COMPONENT/Core.dll"
done

# run bitBetter, this applies our patches to the required files
docker run -v "$TEMPDIRECTORY:/app/mount" --rm bitbetter/bitbetter

# copy the patched files back into the temporary instance
for COMPONENT in ${COMPONENTS[@]}; do
	docker cp "$TEMPDIRECTORY/$COMPONENT/Core.dll" $PATCHINSTANCE:/app/$COMPONENT/Core.dll
done

# create a new image from our patched instanced
docker commit $PATCHINSTANCE bitwarden-patch

# stop and remove our temporary container
docker stop bitwarden-patch
docker rm bitwarden-patch

# copy our patched library to the licenseGen source directory
cp -f "$TEMPDIRECTORY/Identity/Core.dll" "$PWD/licenseGen"

# remove our temporary directory
rm -rf "$TEMPDIRECTORY"

# start all user requested instances
cat "$PWD/.servers/serverlist.txt" | while read LINE; do
	bash -c "$LINE"
done

# remove our bitBetter image
docker image rm bitbetter/bitbetter

# build the licenseGen
docker build -t bitbetter/licensegen "$PWD/licenseGen"

# clean the licenseGen source directory
rm -f "$PWD/licenseGen/Core.dll"
rm -f "$PWD/licenseGen/cert.pfx"