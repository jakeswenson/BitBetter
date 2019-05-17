# BitBetter

BitBetter is is a tool to modify bitwardens core dll to allow you to generate your own individual and organisation licenses. Please see the FAQ below for details on why this software was created.

_Beware! BitBetter does janky IL magic to rewrite the bitwarden core dll and install a self signed certificate. Use at your own risk!_

Credit to https://github.com/h44z/BitBetter and https://github.com/jakeswenson/BitBetter 

# Table of Contents
1. [Getting Started](#gettingstarted)
    + [Pre-requisites](#prereq)
    + [Setting up BitBetter](#setup)
    + [Building BitBetter](#building)
    + [Generating Signed Licenses](#generating)
2. [FAQ](#faq)
3. [Footnotes](#footnotes)

# Getting Started <a name=#gettingstarted></a>
The following instructions are for unix-based systems (Linux, BSD, macOS), it is possible to use a Windows systems assuming you are able to enable and install [WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10).

## Pre-requisites <a name=#prereq></a>
Aside from docker, which you also need for Bitwarden, BitBetter requires the following:

* openssl (probably already installed on most Linux or WSL systems)
* dotnet-sdk-2.1 (install instructions can be found [here](https://dotnet.microsoft.com/download/linux-package-manager/rhel/sdk-2.1.604))

## Setting up BitBetter <a name=#setup></a>
With your pre-requisites installed, begin the installation of BitBetter by downloading it through Github or using the git command:

```bash
git clone https://github.com/online-stuff/BitBetter.git
```

First, we need to add the correct version of Newtonsoft.Json to the license generator and the BitBetter docker directories.

```bash
cd BitBetter/src/licenseGen/
dotnet add package Newtonsoft.Json --version 11.0.0 

cd ../bitBetter
dotnet add package Newtonsoft.Json --version 11.0.0 
```

Next, we need to generate the self-signed certificate we will use to sign any licenses we generate.

To sign your own license you first need to generate your own signing cert using the `.keys/generate-keys.sh` script.

Running this script will prompt you to enter some information about your new certificate, you may leave these at the defaults or set them to your preference. The script will then create a pkcs12 file (.pfx) containing your new key/cert.

You may also choose to do this manually via the following commands.

```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.cert -days 36500 -outform DER -passout pass:test
openssl x509 -inform DER -in cert.cert -out cert.pem
openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -passin pass:test -passout pass:test
```

Note that the password here must be `test`.<sup>[1](#f1)</sup>

## Building BitBetter <a name=#building></a>

Now that you've generated your own own self-signed certificate, you can run the main `BitBetter/build.sh` script to generate a modified version of the `bitwarden/api` docker image.

From the BitBetter directory, simply run:
```bash
./build.sh
```

This will create a modified version of the official `bitwarden/api` called `bitbetter/api`. You may now simply edit your bitwarden docker-compose.yml to utilize the modified image.

Edit your  `/path/to/bwdata/docker/docker-compose.yml`.

> Replace `image: bitwarden/api:x.xx.x`<br>with `image: bitbetter/api`

You'll also want to edit the `/path/to/bwdata/scripts/run.sh` file. In the `function restart()` block, comment out the call to `dockerComposePull`.

> Replace `dockerComposePull`<br>with `#dockerComposePull`

You can now start or restart Bitwarden as normal and the modified api will be used. <b>It is now ready to accept self-issued licenses.</b>

## Generating Signed Licenses <a name=#generating></a>

There is a tool included in the directory `src/licenseGen/` that will generate new individual and organization licenses. These licenses will be accepted by the modified Bitwarden because they will be signed by the certificate you generated in earlier steps.

First, from the `BitBetter/src/licenseGen` directory, build the license generator.<sup>[2](#f2)</sup>

```bash
./build.sh
```

Now, from the `BitBetter/src/licenseGen` directory, you can run the tool to generate licenses.

You'll need to get a user's <b>GUID</b> in order to generate an <b>invididual license</b> and the server's <b>install ID</b> to generate an <b>Organization license</b>. These can be retrieved most easily through the Bitwarden [Admin Portal](https://help.bitwarden.com/article/admin-portal/).

```bash
./run.sh ~/BitBetter/.keys/cert.pfx user "Name" "EMail" "User-GUID"
./run.sh ~/BitBetter/.keys/cert.pfx org "Name" "EMail" "Install-ID used to install the server"
```

<b>The license generator will spit out a JSON-formatted license which can then be used within the Bitwarden web front-end to license your user or org!</b>

# FAQ: Questions (you might have?) <a name=#faq></a>

I'll work on updates in the next couple weeks, right now, I just wanted something to start with.

## But why? Its open source?

Yes, bitwarden is great. If I didn't care about it i wouldn't be doing this.
I was bothered that if i want to host bitwarden myself, at my house, 
for my family to use (with the ability to share access) I would still have to pay a monthly ENTERPRISE organization fee.
To host it myself. And maintain it myself. Basically WTH was bitwarden doing that I was paying them for?

## You should have reached out to bitwarden

Thanks, good idea. And I did. Currently they're not focused on solving this issue - yet. 
To be clear i'm totally happy to give them my money. Offer a perpetual server license, and i'd pay for it.  Let me license the server, period.  Allow an orginzation to have Premium for all users..  500 seats, let the 500 users in the orginzation have the Premium features too.

I'm still in the testing/evaluating phase.  If I am hosting the server/data, let me license the server, period.  How many licenses does one user need to have...

# Footnotes <a name=#footnotes></a>

<a name="#f1"><sup>1</sup></a> If you wish to change this you'll need to change the value that `src/licenseGen/Program.cs` uses for it's `GenerateUserLicense` and `GenerateOrgLicense` calls, but this is really unnecessary as this certificate does not represent any type of security issue.

<a name="#f2"><sup>2</sup></a>This tool build ontop of the `bitbetter/api` container image so make sure you've built that above using the root `./build.sh` script.