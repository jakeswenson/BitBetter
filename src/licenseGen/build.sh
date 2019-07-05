#!/bin/bash

script_dir=`cd $(dirname $0); pwd`

cd "$script_dir"

dotnet restore
dotnet publish

docker build . -t bitbetter/licensegen # --squash

