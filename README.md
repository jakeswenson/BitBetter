# BitBetter

BitBetter is is a tool to modify Bitwarden's core dll to allow you to generate your own individual and organisation licenses. **You must have an existing installation of Bitwarden for BitBetter to modify.**

Please see the FAQ below for details on why this software was created.

Looking for a solution to the Lite (formerly unified) version of bitwarden, [go to the Lite branch](../../tree/lite).

_Beware! BitBetter does janky stuff to rewrite the bitwarden core dll and allow the installation of a self signed certificate. Use at your own risk!_

Credit to https://github.com/h44z/BitBetter and https://github.com/jakeswenson/BitBetter

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
The following instructions are for unix-based systems (Linux, BSD, macOS), it is possible to use a Windows systems assuming you are able to enable and install [WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10).

## Dependencies
Aside from docker, which you also need for Bitwarden, BitBetter requires the following:

* Bitwarden (tested with 1.47.1, might work on lower versions)
* openssl (probably already installed on most Linux or WSL systems, any version should work)

## Setting up BitBetter
With your dependencies installed, begin the installation of BitBetter by downloading it through Github or using the git command:

```bash
git clone https://github.com/jakeswenson/BitBetter.git
```

### Optional: Manually generating Certificate & Key

If you wish to generate your self-signed cert & key manually, you can run the following commands.

```bash
cd .keys
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.cert -days 36500 -outform DER -passout pass:test
openssl x509 -inform DER -in cert.cert -out cert.pem
openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -passin pass:test -passout pass:test
```

> Note that the password here must be `test`.<sup>[1](#f1)</sup>

---

## Building BitBetter

Now that you've set up your build environment, you can **run the main build script** to generate a modified version of the `bitwarden/api` and `bitwarden/identity` docker images.

From the BitBetter directory, simply run:
```bash
./build.sh
```

This will create a new self-signed certificate in the `.keys` directory if one does not already exist and then create a modified version of the official `bitwarden/api` called `bitbetter/api` and a modified version of the `bitwarden/identity` called `bitbetter/identity`.

You may now simply create the file `/path/to/bwdata/docker/docker-compose.override.yml` with the following contents to utilize the modified images.

```yaml
services:
  api:
    image: bitbetter/api
    pull_policy: never

  identity:
    image: bitbetter/identity
    pull_policy: never
```

You'll also want to edit the `/path/to/bwdata/scripts/run.sh` file. In the `function restart()` block, comment out the call to `dockerComposePull`.

> Replace `dockerComposePull`<br>with `#dockerComposePull`

You can now start or restart Bitwarden as normal and the modified api will be used. **It is now ready to accept self-issued licenses.**

---

## Updating Bitwarden and BitBetter

To update Bitwarden, the provided `update-bitwarden.sh` script can be used. It will rebuild the BitBetter images and automatically update Bitwarden afterwards. Docker pull errors can be ignored for api and identity images.

You can either run this script without providing any parameters in interactive mode (`./update-bitwarden.sh`) or by setting the parameters as follows, to run the script in non-interactive mode:
```bash
./update-bitwarden.sh param1 param2 param3
```
`param1`: The path to the directory containing your bwdata directory

`param2`: If you want the docker-compose file to be overwritten (either `y` or `n`)

`param3`: If you want the bitbetter images to be rebuild (either `y` or `n`)

If you are updating from versions <= 1.46.2, you may need to run `update-bitwarden.sh` twice to complete the update process.

## Generating Signed Licenses

There is a tool included in the directory `src/licenseGen/` that will generate new individual and organization licenses. These licenses will be accepted by the modified Bitwarden because they will be signed by the certificate you generated in earlier steps.


First, from the `BitBetter/src/licenseGen` directory, **build the license generator**.<sup>[2](#f2)</sup>

```bash
./build.sh
```

In order to run the tool and generate a license you'll need to get a **user's GUID** in order to generate an **invididual license** or the server's **install ID** to generate an **Organization license**. These can be retrieved most easily through the Bitwarden [Admin Portal](https://help.bitwarden.com/article/admin-portal/).

**The user must have a verified email address at the time of license import, otherwise Bitwarden will reject the license key. Nevertheless, the license key can be generated even before the user's email is verified.**

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
./src/licenseGen/run.sh /Absolute/Path/To/BitBetter/.keys/cert.pfx user "Name" "E-Mail" "User-GUID" ["Storage Space in GB"] ["Custom LicenseKey"]
./src/licenseGen/run.sh /Absolute/Path/To/BitBetter/.keys/cert.pfx org "Name" "E-Mail" "Install-ID used to install the server" ["Storage Space in GB"] ["Custom LicenseKey"]
```

---


# FAQ: Questions you might have.

## Why build a license generator for open source software?

We agree that Bitwarden is great. If we didn't care about it then we wouldn't be doing this. We believe that if a user wants to host Bitwarden themselves, in their house, for their family to use and with the ability to share access, they would still have to pay a **monthly** enterprise organization fee. When hosting and maintaining the software yourself there is no need to pay for the level of service that an enterprise customer needs.

Unfortunately, Bitwarden doesn't seem to have any method for receiving donations so we recommend making a one-time donation to your open source project of choice for each BitBetter license you generate if you can afford to do so.

## Shouldn't you have reached out to Bitwarden to ask them for alternative licensing structures?

In the past we have done so but they were not focused on the type of customer that would want a one-time license and would be happy to sacrifice customer service. We believe the features that are currently behind this subscription paywall to be critical ones and believe they should be available to users who can't afford an enterprise payment structure. We'd even be happy to see a move towards a Gitlab-like model where premium features are rolled out *first* to the enterprise subscribers before being added to the fully free version.

UPDATE: Bitwarden now offers a cheap license called [Families Organization](https://bitwarden.com/pricing/) that provides premium features and the ability to self-host Bitwarden for six persons.


# Footnotes

<a name="#f1"><sup>1</sup></a> If you wish to change this you'll need to change the value that `src/licenseGen/Program.cs` uses for its `GenerateUserLicense` and `GenerateOrgLicense` calls. Remember, this is really unnecessary as this certificate does not represent any type of security-related certificate.

<a name="#f2"><sup>2</sup></a>This tool builds on top of the `bitbetter/api` container image so make sure you've built that above using the root `./build.sh` script.

