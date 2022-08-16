#!/bin/sh

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

# Grab the absolute path to the default pfx location
cert_path="$DIR/../../.keys/cert.pfx"

if [ "$#" -lt "2" ]; then
    echo "USAGE: $0 <ABSOLUTE PATH TO CERT.PFX> <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
    exit 1
fi

cert_path="$1"
action="$2"
shift

if [ $action = "interactive" ]; then
    docker run -it --rm -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"
else
    docker run --rm -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"
fi
