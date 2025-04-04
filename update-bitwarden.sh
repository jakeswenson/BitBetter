#!/bin/bash
ask () {
  local __resultVar=$1
  local __result="$2"
  if [ -z "$2" ]; then
    read -p "$3" __result
  fi
  eval $__resultVar="'$__result'"
}

SCRIPT_BASE="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
BW_VERSION=$(curl -sL https://go.btwrdn.co/bw-sh-versions | grep '^ *"'coreVersion'":' | awk -F\: '{ print $2 }' | sed -e 's/,$//' -e 's/^"//' -e 's/"$//')

echo "Starting Bitwarden update, newest server version: $BW_VERSION"

# Default path is the parent directory of the BitBetter location
BITWARDEN_BASE="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." >/dev/null 2>&1 && pwd )"

# Get Bitwarden base from user (or keep default value) or use first argument
ask tmpbase "$1" "Enter Bitwarden base directory [$BITWARDEN_BASE]: "
BITWARDEN_BASE=${tmpbase:-$BITWARDEN_BASE}

# Check if directory exists and is valid
[ -d "$BITWARDEN_BASE" ] || { echo "Bitwarden base directory $BITWARDEN_BASE not found!"; exit 1; }
[ -f "$BITWARDEN_BASE/bitwarden.sh" ] || { echo "Bitwarden base directory $BITWARDEN_BASE is not valid!"; exit 1; }

# Check if user wants to recreate the docker-compose override file
RECREATE_OV="y"
ask tmprecreate "$2" "Rebuild docker-compose override? [Y/n]: "
RECREATE_OV=${tmprecreate:-$RECREATE_OV}

if [[ $RECREATE_OV =~ ^[Yy]$ ]]
then
    {
        echo "services:"
        echo "  api:"
        echo "    image: bitbetter/api:$BW_VERSION"
        echo "    pull_policy: never"
        echo ""
        echo "  identity:"
        echo "    image: bitbetter/identity:$BW_VERSION"
        echo "    pull_policy: never"        
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
ask tmprebuild "$3" "Rebuild BitBetter images? $REBUILD_BB_DESCR: "
REBUILD_BB=${tmprebuild:-$REBUILD_BB}

if [[ $REBUILD_BB =~ ^[Yy]$ ]]
then
    $SCRIPT_BASE/build.sh
    echo "BitBetter images updated to version: $BW_VERSION"
fi

# Now start the bitwarden update
cd $BITWARDEN_BASE

./bitwarden.sh updateself

# Update the bitwarden.sh: automatically patch run.sh to fix docker-compose pull errors for private images
sed -i 's/chmod u+x $SCRIPTS_DIR\/run.sh/chmod u+x $SCRIPTS_DIR\/run.sh\n        sed -i \x27s\/dccmd pull\/dccmd pull --ignore-pull-failures || true\/g\x27 $SCRIPTS_DIR\/run.sh/g' -i $BITWARDEN_BASE/bitwarden.sh
chmod +x $BITWARDEN_BASE/bitwarden.sh
echo "Patching bitwarden.sh completed..."

./bitwarden.sh update

# Prune Docker images without at least one container associated to them.
echo "Pruning Docker images without at least one container associated to them..."
docker image prune -a

cd $SCRIPT_BASE
echo "Bitwarden update completed!"
