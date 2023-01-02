$opensslbinary = "$Env:Programfiles\OpenSSL-Win64\bin\openssl.exe"

if (!(Get-Command $opensslbinary -errorAction SilentlyContinue))
{
    winget install openssl
}

if (Test-Path "$pwd\.keys")
{
	Remove-Item "$pwd\.keys" -Recurse -Force
}
New-item -ItemType Directory -Path "$pwd\.keys"

Invoke-Expression "& '$opensslbinary' req -x509 -newkey rsa:4096 -keyout `"$pwd\.keys\key.pem`" -out `"$pwd\.keys\cert.cert`" -days 36500 -subj '/CN=www.mydom.com/O=My Company Name LTD./C=US' -outform DER -passout pass:test"
Invoke-Expression "& '$opensslbinary' x509 -inform DER -in `"$pwd\.keys\cert.cert`" -out `"$pwd\.keys\cert.pem`""
Invoke-Expression "& '$opensslbinary' pkcs12 -export -out `"$pwd\.keys\cert.pfx`" -inkey `"$pwd\.keys\key.pem`" -in `"$pwd\.keys\cert.pem`" -passin pass:test -passout pass:test"