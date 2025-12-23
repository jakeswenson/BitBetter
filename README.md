# BitBetter lite

BitBetter is is a tool to modify Bitwarden's core dll to allow you to generate your own individual and organisation licenses.

Please see the FAQ below for details on why this software was created.

Be aware that this branch is **only** for the lite (formerly unified) version of bitwarden. It has been rewritten and works in different ways than the master branch.

_Beware! BitBetter is a solution that generates a personal certificate and uses that to generate custom licences. This requires (automated) modifying of libraries. Use at your own risk!_

Credit to https://github.com/h44z/BitBetter and https://github.com/jakeswenson/BitBetter and https://github.com/GieltjE/BitBetter

# Table of Contents
- [BitBetter](#bitbetter)
- [Table of Contents](#table-of-contents)
- [Getting Started](#getting-started)
  - [Dependencies](#dependencies)
  - [Setting up BitBetter](#setting-up-bitbetter)
    - [Optional: Manually generating Certificate & Key](#optional-manually-generating-certificate--key)
  - [Building BitBetter](#building-bitbetter)
  - [Updating Bitwarden and BitBetter](#updating-bitwarden-and-bitbetter)
  - [Generating Signed Licenses](#generating-signed-licenses)
    - [Note: Alternative Ways to Generate License](#note-alternative-ways-to-generate-license)
- [FAQ: Questions you might have.](#faq-questions-you-might-have)
  - [Why build a license generator for open source software?](#why-build-a-license-generator-for-open-source-software)
  - [Shouldn't you have reached out to Bitwarden to ask them for alternative licensing structures?](#shouldnt-you-have-reached-out-to-bitwarden-to-ask-them-for-alternative-licensing-structures)
- [Footnotes](#footnotes)

# Getting Started
The following instructions are for unix-based systems (Linux, BSD, macOS) and Windows, just choose the correct script extension (.sh or .ps1 respectively).

## Dependencies
Aside from docker, which you also need for Bitwarden, BitBetter requires the following:

* Bitwarden (tested with 2025.11.1 might work on lower versions), for safety always stay up to date
* openssl (probably already installed on most Linux or WSL systems, any version should work, on Windows it will be auto installed using winget)

## Setting up BitBetter
With your dependencies installed, begin the installation of BitBetter by downloading it through Github or using the git command:

```
git clone https://github.com/jakeswenson/BitBetter.git -b lite
```

### Optional: Manually generating Certificate & Key

If you wish to generate your self-signed cert & key manually, you can run the following commands.

```bash
cd .keys
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.cer -days 36500 -outform DER -passout pass:test
openssl x509 -inform DER -in cert.cer -out cert.pem
openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -passin pass:test -passout pass:test
```

> Note that the password here must be `test`.<sup>[1](#f1)</sup>
---


## Building BitBetter

Now that you've set up your build environment, we need to specify which servers to start after the work is done.
The scripts supports running and patching multi instances.

Edit the .servers/serverlist.txt file and fill in the missing values, they can be replaced with existing installation values.
This file may be empty, but there will be no containers will be spun up automatically.

Now it is time to **run the main build script** to generate a modified version of the `ghcr.io/bitwarden/lite` docker image and the license generator.

From the BitBetter directory, simply run:
```
./build.[sh|ps1]
```

This will create a new self-signed certificate in the `.keys` directory if one does not already exist and then create a modified version of the official `ghcr.io/bitwarden/lite` image called `bitwarden-patched`.

Afterwards it will automatically generate the license generator and start all previously specified containers which are **now ready to accept self-issued licenses.**


---

## Updating Bitwarden and BitBetter

To update Bitwarden, the same `build.[sh|ps1]` script can be used. It will rebuild the BitBetter image and automatically update Bitwarden before doing so.

## Generating Signed Licenses

There is a tool included in the directory `licenseGen/` that will generate new individual and organization licenses. These licenses will be accepted by the modified Bitwarden because they will be signed by the certificate you generated in earlier steps.

In order to run the tool and generate a license you'll need to get a **user's GUID** in order to generate an **invididual license** or the server's **install ID** to generate an **Organization license**. These can be retrieved most easily through the Bitwarden [Admin Portal](https://help.bitwarden.com/article/admin-portal/).

**The user must have a verified email address at the time of license import, otherwise Bitwarden will reject the license key. Nevertheless, the license key can be generated even before the user's email is verified.**

If you ran the build script, you can **simply run the license gen in interactive mode** from the `Bitbetter` directory and **follow the prompts to generate your license**.

```
./licenseGen.[sh|ps1] interactive
```

**The license generator will spit out a JSON-formatted license which can then be used within the Bitwarden web front-end to license your user or org!**


## Migrating from mssql to a real database

Prepare a new database and bwdata directory, download and prepare the new settings.env (https://raw.githubusercontent.com/bitwarden/self-host/refs/heads/main/bitwarden-lite/settings.env)

Make sure you can get the data from either the backup file or by connecting directly to the mssql database (navicat has a trial).

If required (e.g. you cannot connect to your docker mssql server directly) download Microsoft SQL Server 2022 and SQL Server Management Studio (the latter can be used to import the .bak file)

After cloning this repo and modifying .servers/serverlist.txt to suit your new environment do the following:

```
docker exec -i bitwarden-mssql /backup-db.sh
./bitwarden.sh stop
```

Run build.sh and ensure your new instance serves a webpage AND has populated the new database with the tables (should be empty now)

Proceed to stop the new container for now.

Copy from the old to the new bwdata directory (do not copy/overwrite identity.pfx!):
 - bwdata/core/licenses to bwdata-new/licenses
 - bwdata/core/aspnet-dataprotection to bwdata-new/data-protection
 - bwdata/core/attachments to bwdata-new/attachments

Export data only from the old sql server database, if needed import the .bak file to a local mssql instance.

Only export tables that have rows, makes it much quicker, .json is the easiest with navicat.

Import the rows to the real database, start the new docker container.

---

# FAQ: Questions you might have.

## Why build a license generator for open source software?

We agree that Bitwarden is great. If we didn't care about it then we wouldn't be doing this. We believe that if a user wants to host Bitwarden themselves, in their house, for their family to use and with the ability to share access, they would still have to pay a **monthly** enterprise organization fee. When hosting and maintaining the software yourself there is no need to pay for the level of service that an enterprise customer needs.

Unfortunately, Bitwarden doesn't seem to have any method for receiving donations so we recommend making a one-time donation to your open source project of choice for each BitBetter license you generate if you can afford to do so.

## Shouldn't you have reached out to Bitwarden to ask them for alternative licensing structures?

In the past we have done so but they were not focused on the type of customer that would want a one-time license and would be happy to sacrifice customer service. We believe the features that are currently behind this subscription paywall to be critical ones and believe they should be available to users who can't afford an enterprise payment structure. We'd even be happy to see a move towards a Gitlab-like model where premium features are rolled out *first* to the enterprise subscribers before being added to the fully free version.

UPDATE: Bitwarden now offers a cheap license called [Families Organization](https://bitwarden.com/pricing/) that provides premium features and the ability to self-host Bitwarden for six persons.

## 2fa doesn't work

Unfortunately the new BitWarden container doesn't set the timezone and ignores TZ= from the environment, can be fixed by:

```
docker exec bitwarden ln -s /usr/share/zoneinfo/Europe/Amsterdam /etc/localtime
```

## Changes in settings.env

Require a recreation of the docker container, build.sh will suffice too.

## Migrating from the old unified branch

```
git branch -m unified lite
git fetch origin
git branch -u origin/lite lite
git remote set-head origin -a
```

# Footnotes

<a name="#f1"><sup>1</sup></a>This tool builds on top of the `bitbetter/api` container image so make sure you've built that above using the root `./build.sh` script.

<a name="#f2"><sup>2</sup></a> If you wish to change this you'll need to change the value that `licenseGen/Program.cs` uses for its `GenerateUserLicense` and `GenerateOrgLicense` calls. Remember, this is really unnecessary as this certificate does not represent any type of security-related certificate.
