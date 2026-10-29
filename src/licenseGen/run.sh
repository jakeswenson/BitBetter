#!/bin/sh

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

# Grab the absolute path to the default pfx location
cert_path="$DIR/../../.keys/cert.pfx"

# The cert path is optional: only consume the first argument if it isn't an action
case "$1" in
    interactive|user|org|"") ;;
    *) cert_path="$1"; shift ;;
esac

if [ "$#" -lt "1" ]; then
    echo "USAGE: $0 [ABSOLUTE PATH TO CERT.PFX] <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
    exit 1
fi

if [ ! -f "$cert_path" ]; then
    echo "Error: certificate not found at '$cert_path'. Run ./build.sh first or pass the path to your cert.pfx." >&2
    exit 1
fi

action="$1"

if [ "$action" = "interactive" ]; then
    docker run -it --rm -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"
else
    docker run --rm -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"
fi
