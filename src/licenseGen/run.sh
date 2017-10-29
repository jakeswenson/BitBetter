#!/bin/bash

script_dir=`cd $(dirname $0); pwd`

if [ "$#" -lt "1" ]; then
    echo "USAGE: $0 <ABSOLUTE PATH TO CERT.PFX> [License Gen args...]"
    exit 1
fi
cert_path=$1
shift
docker run -it -v "$cert_path:/cert.pfx" bitbetter/licensegen "$@"

