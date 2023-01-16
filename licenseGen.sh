#!/bin/bash

if [ $# -lt 1 ]; then
    echo "USAGE: <License Gen action> [License Gen args...]"
    echo "ACTIONS:"
    echo " interactive"
    echo " user"
    echo " org"
    exit 1
fi

if [ "$1" = "interactive" ]; then
	docker run -it --rm bitbetter/licensegen interactive
else
	docker run --rm bitbetter/licensegen "$@"
fi
