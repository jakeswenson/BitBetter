#!/bin/bash

script_dir=`cd $(dirname $0); pwd`

if [ "$#" -ne "1" ]; then
    echo "USAGE: $0 <ABSOLUTE PATH TO CERT.PFX>"
    exit 1
fi

docker run -it -v "$1:/cert.pfx" bitbetter/licensegen

