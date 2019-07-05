#!/bin/sh

# Check for openssl
command -v openssl >/dev/null 2>&1 || { echo >&2 "openssl required but not found.  Aborting."; exit 1; }

DIR=`dirname "$0"`
DIR=`exec 2>/dev/null;(cd -- "$DIR") && cd -- "$DIR"|| cd "$DIR"; unset PWD; /usr/bin/pwd || /bin/pwd || pwd`

# Remove any existing key files
[ ! -e "$DIR/cert.pem" ]  || rm "$DIR/cert.pem"
[ ! -e "$DIR/key.pem" ]   || rm "$DIR/key.pem"
[ ! -e "$DIR/cert.cert" ] || rm "$DIR/cert.cert"
[ ! -e "$DIR/cert.pfx" ]  || rm "$DIR/cert.pfx"

# Generate new keys
openssl	req -x509 -newkey rsa:4096 -keyout "$DIR/key.pem" -out "$DIR/cert.cert" -days 36500 -subj '/CN=www.mydom.com/O=My Company Name LTD./C=US'  -outform DER -passout pass:test
openssl x509 -inform DER -in "$DIR/cert.cert" -out "$DIR/cert.pem"
openssl pkcs12 -export -out "$DIR/cert.pfx" -inkey "$DIR/key.pem" -in "$DIR/cert.pem" -passin pass:test -passout pass:test

ls
