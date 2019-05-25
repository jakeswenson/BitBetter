#!/bin/sh

# Check for openssl
command -v openssl >/dev/null 2>&1 || { echo >&2 "openssl required but not found.  Aborting."; exit 1; }

# Remove any existing key files
[ ! -e cert.pem ]  || rm cert.pem
[ ! -e key.pem ]   || rm key.pem
[ ! -e cert.cert ] || rm cert.cert
[ ! -e cert.pfx ]  || rm cert.pfx

# Generate new keys
openssl	req -x509 -newkey rsa:4096 -keyout .keys/key.pem -out .keys/cert.cert -days 36500 -subj '/CN=www.mydom.com/O=My Company Name LTD./C=US'  -outform DER -passout pass:test
openssl x509 -inform DER -in .keys/cert.cert -out .keys/cert.pem
openssl pkcs12 -export -out .keys/cert.pfx -inkey .keys/key.pem -in .keys/cert.pem -passin pass:test -passout pass:test

ls
