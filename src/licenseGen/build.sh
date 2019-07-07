#!/bin/sh

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

docker build -t bitbetter/licensegen "$DIR" # --squash
