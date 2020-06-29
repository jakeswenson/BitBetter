#!/bin/bash

SCRIPT_BASE="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
BW_VERSION="$(curl --silent https://raw.githubusercontent.com/bitwarden/server/master/scripts/bitwarden.sh | grep 'COREVERSION="' | sed 's/^[^"]*"//; s/".*//')"

echo "Starting Bitwarden update, newest server version: $BW_VERSION"

# Default path is the parent directory of the BitBetter location
BITWARDEN_BASE="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." >/dev/null 2>&1 && pwd )"

# Get Bitwarden base from user (or keep default value)
read -p "Enter Bitwarden base directory [$BITWARDEN_BASE]: " tmpbase
BITWARDEN_BASE=${tmpbase:-$BITWARDEN_BASE}

# Check if directory exists and is valid
[ -d "$BITWARDEN_BASE" ] || { echo "Bitwarden base directory $BITWARDEN_BASE not found!"; exit 1; }
[ -f "$BITWARDEN_BASE/bitwarden.sh" ] || { echo "Bitwarden base directory $BITWARDEN_BASE is not valid!"; exit 1; }

# Check if user wants to recreate the docker-compose override file
RECREATE_OV="y"
read -p "Rebuild docker-compose override? [Y/n]: " tmprecreate
RECREATE_OV=${tmprecreate:-$RECREATE_OV}

if [[ $RECREATE_OV =~ ^[Yy]$ ]]
then
    {
        echo "version: '3'"
        echo ""
        echo "services:"
        echo "  api:"
        echo "    image: bitbetter/api:$BW_VERSION"
        echo ""
        echo "  identity:"
        echo "    image: bitbetter/identity:$BW_VERSION"
        echo ""
    } > $BITWARDEN_BASE/bwdata/docker/docker-compose.override.yml
    echo "BitBetter docker-compose override created!"
else
    echo "Make sure to check if the docker override contains the correct image version ($BW_VERSION) in $BITWARDEN_BASE/bwdata/docker/docker-compose.override.yml!"
fi

# Check if user wants to rebuild the bitbetter images
docker images bitbetter/api --format="{{ .Tag }}" | grep -F -- "${BW_VERSION}" > /dev/null
retval=$?
REBUILD_BB="n"
REBUILD_BB_DESCR="[y/N]"
if [ $retval -ne 0 ]; then
    REBUILD_BB="y"
    REBUILD_BB_DESCR="[Y/n]"
fi
read -p "Rebuild BitBetter images? $REBUILD_BB_DESCR: " tmprebuild
REBUILD_BB=${tmprebuild:-$REBUILD_BB}

if [[ $REBUILD_BB =~ ^[Yy]$ ]]
then
    ./build.sh
    echo "BitBetter images updated to version: $BW_VERSION"
fi

# Now start the bitwarden update
cd $BITWARDEN_BASE

./bitwarden.sh updateself

# Update the bitwarden.sh: automatically patch run.sh to fix docker-compose pull errors for private images
awk '1;/function downloadRunFile/{c=6}c&&!--c{print "sed -i '\''s/docker-compose pull/docker-compose pull --ignore-pull-failures || true/g'\'' $SCRIPTS_DIR/run.sh"}' $BITWARDEN_BASE/bitwarden.sh > tmp_bw.sh && mv tmp_bw.sh $BITWARDEN_BASE/bitwarden.sh
chmod +x $BITWARDEN_BASE/bitwarden.sh
echo "Patching bitwarden.sh completed..."

./bitwarden.sh update

cd $SCRIPT_BASE
echo "Bitwarden update completed!"
