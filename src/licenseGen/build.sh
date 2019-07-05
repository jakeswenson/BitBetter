#!/bin/bash

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker build -t bitbetter/licensegen "$DIR" # --squash
