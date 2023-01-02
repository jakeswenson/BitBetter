#!/bin/sh

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

if [ "$#" -lt "2" ]; then
    echo "USAGE: <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
    exit 1
fi

if [ "$1" = "interactive" ]; then
	shift
    docker run -it --rm bitbetter/licensegen "$@"
else
    docker run --rm bitbetter/licensegen "$@"
fi
