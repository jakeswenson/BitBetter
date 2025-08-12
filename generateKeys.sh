#!/bin/bash
set -e

# Check for openssl
command -v openssl >/dev/null 2>&1 || { echo >&2 "openssl required but not found.  Aborting."; exit 1; }

DIR="$PWD/.keys"

# if previous keys exist, remove them
if [ -d "$DIR" ]; then
	rm -rf "$DIR"
fi

# create new directory 
mkdir "$DIR"

# Generate new keys
openssl	req -x509 -newkey rsa:4096 -keyout "$DIR/key.pem" -out "$DIR/cert.cer" -days 36500 -subj '/CN=www.mydom.com/O=My Company Name LTD./C=US'  -outform DER -passout pass:test
openssl x509 -inform DER -in "$DIR/cert.cer" -out "$DIR/cert.pem"
openssl pkcs12 -export -out "$DIR/cert.pfx" -inkey "$DIR/key.pem" -in "$DIR/cert.pem" -passin pass:test -passout pass:test
