# BitBetter

BitBetter is is a tool to modify bitwardens core dll to allow you to generate your own individual and organisation licenses. Please see the FAQ below for details on why this software was created.

_Beware! BitBetter does janky IL magic to rewrite the bitwarden core dll and install a self signed certificate. Use at your own risk!_

Credit to https://github.com/h44z/BitBetter and https://github.com/jakeswenson/BitBetter 

# Table of Contents
1. [Getting Started](#getting-started)
    + [Pre-requisites](#pre-requisites)
    + [Setting up BitBetter](#setting-up-bitbetter)
    + [Building BitBetter](#building-bitbetter)
    + [Generating Signed Licenses](#generating-signed-licenses)
2. [FAQ](#faq-questions-you-might-have-)
3. [Footnotes](#footnotes)

# Getting Started
The following instructions are for unix-based systems (Linux, BSD, macOS), it is possible to use a Windows systems assuming you are able to enable and install [WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10).

## Pre-requisites
Aside from docker, which you also need for Bitwarden, BitBetter requires the following:

* openssl (probably already installed on most Linux or WSL systems)
* dotnet-sdk-2.1 (install instructions can be found [here](https://dotnet.microsoft.com/download/linux-package-manager/rhel/sdk-2.1.604))

## Setting up BitBetter
With your pre-requisites installed, begin the installation of BitBetter by downloading it through Github or using the git command:

```bash
git clone https://github.com/online-stuff/BitBetter.git
```

First, we need to add the correct version of Newtonsoft.Json to the license generator and the BitBetter docker directories.

```bash
cd BitBetter/src/licenseGen/
dotnet add package Newtonsoft.Json --version 12.0.1 

cd ../bitBetter
dotnet add package Newtonsoft.Json --version 12.0.1 
```
## Building BitBetter

Now that you've set up your build environment, you can **run the main build script** to generate a modified version of the `bitwarden/api` and `bitwarden/identity` docker images.

From the BitBetter directory, simply run:
```bash
./build.sh
```

This will create a new self-signed certificate in the `.keys` directory one does not already exist and then create a modified version of the official `bitwarden/api` called `bitbetter/api` and a modified version of the `bitwarden/identity` called `bitbetter/identity`. You may **now simply edit your bitwarden docker-compose.yml to utilize the modified image**.

Edit your  `/path/to/bwdata/docker/docker-compose.yml`.

> Replace `image: bitwarden/api:x.xx.x`<br>with `image: bitbetter/api`

> Replace `image: bitwarden/identity:x.xx.x`<br>with `image: bitbetter/identity`

You'll also want to edit the `/path/to/bwdata/scripts/run.sh` file. In the `function restart()` block, comment out the call to `dockerComposePull`.

> Replace `dockerComposePull`<br>with `#dockerComposePull`

You can now start or restart Bitwarden as normal and the modified api will be used. **It is now ready to accept self-issued licenses.**

---
### Note: Manually generating Certificate & Key

If you wish to generate your self-signed cert & key manually, you can run the following commands.

```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.cert -days 36500 -outform DER -passout pass:test
openssl x509 -inform DER -in cert.cert -out cert.pem
openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -passin pass:test -passout pass:test
```

> Note that the password here must be `test`.<sup>[1](#f1)</sup>

---

## Generating Signed Licenses

There is a tool included in the directory `src/licenseGen/` that will generate new individual and organization licenses. These licenses will be accepted by the modified Bitwarden because they will be signed by the certificate you generated in earlier steps.

First, from the `BitBetter` directory, **build the license generator**.<sup>[2](#f2)</sup>

```bash
./build.sh
```

In order to run the tool and generate a license you'll need to get a **user's GUID** in order to generate an **invididual license** or the server's **install ID** to generate an **Organization license**. These can be retrieved most easily through the Bitwarden [Admin Portal](https://help.bitwarden.com/article/admin-portal/).

If you generated your keys in the default `BitBetter/.keys` directory, you can **simply run the license gen in interactive mode** from the `Bitbetter` directory and **follow the prompts to generate your license**.

```bash
./src/licenseGen/run.sh interactive
```

**The license generator will spit out a JSON-formatted license which can then be used within the Bitwarden web front-end to license your user or org!**

---

### Note: Alternative Ways to Generate License

If you wish to run the license gen from a directory aside from the root `BitBetter` one, you'll have to provide the absolute path to your cert.pfx.

```bash
./src/licenseGen/run.sh /Absolute/Path/To/BitBetter/.keys/cert.pfx interactive
```

Additional, instead of interactive mode, you can also pass the parameters directly to the command as follows.

```bash
./src/licenseGen/run.sh /Absolute/Path/To/BitBetter/.keys/cert.pfx user "Name" "EMail" "User-GUID"
./src/licenseGen/run.sh /Absolute/Path/To/BitBetter/.keys/cert.pfx org "Name" "EMail" "Install-ID used to install the server"
```

---


# FAQ: Questions (you might have?)

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

# Footnotes

<a name="#f1"><sup>1</sup></a> If you wish to change this you'll need to change the value that `src/licenseGen/Program.cs` uses for it's `GenerateUserLicense` and `GenerateOrgLicense` calls, but this is really unnecessary as this certificate does not represent any type of security issue.

<a name="#f2"><sup>2</sup></a>This tool build ontop of the `bitbetter/api` container image so make sure you've built that above using the root `./build.sh` script.