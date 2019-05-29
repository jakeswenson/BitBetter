#!/bin/bash

script_dir=`cd $(dirname $0); pwd`

# Grab the absolute path to the default pfx location
cert_path=`cd ./.keys; ls -d -1 $PWD/cert.pfx`

if [ "$#" -lt "1" ]; then
    echo "USAGE: $0 <ABSOLUTE PATH TO CERT.PFX> [License Gen args...]"
    exit 1
elif [ "$#" -ge "2" ]; then
    # If a cert path is provided manually, override the default
    cert_path=$1
    shift
fi

docker run -it -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"

