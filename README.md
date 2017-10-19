# BitBetter

This project is a tool to modify bitwardens core dll to allow me to self license.
Beware this does janky IL magic to rewrite the bitwarden core dll and install my self signed certificate.

## Building

There's no formal build script/process yet. To build your own `bitwarden/api` image run
```bash
dotnet restore
dotnet publish
docker build . -t bitbetter/api
```

replace anywhere `bitwarden/api` is used with `bitbetter/api` and give it a go. no promises

## Issuing your own licenses

The repo is setup to replace the licesning signing cert in bitwarden.core with my own personal self signed cert (`cert.cert`)
If you want to be able to sign your own licenses obviously you'll have to replace it with your own self signed cert.

you can generate one with openssl like so:
```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.cert -days 36500 -outform DER
```

## But why? Its open source?

Yes, bitwarden is great. If I didn't care about it i wouldn't be doing this.
I was bothered that if i want to host bitwarden myself, at my house, 
for my family to use (with the ability to share access) I would still have to pay a monthly ENTERPRISE organization fee.
To host it myself. And maintain it myself. Basically WTH was bitwarden doing that I was paying them for?

## You should have reached out to bitwarden

Thanks, good idea. And I did. Currently they're not focused on solving this issue - yet. 
To be clear i'm totally happy to give them my money. Offer a perpetual family license, and i'd pay for it. 
Offer me a license thats tied to a version, I'll gladly rebuy another when a new version comes out AND i'm ready to upgrade.

I provided all these suggestions to bitwarden and they told me to wait until next year. Until then there's this.
