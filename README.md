# BitBetter
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

We agree that Bitwarden is great. If we didn't care about it then we wouldn't be doing this. We believe that if a user wants to host Bitwarden themselves on their own infrastructure, for their family to use, and with the ability to share access, they should still pay the fee charged by the software publishers.

Such use cases may not require enterprise level support and unfortunately Bitwarden doesn't have any method for receiving donations - therefore we recommend you purchasing the relevant licenses for your use directly from Bitwarden: https://bitwarden.com/pricing/.

## Shouldn't you have reached out to Bitwarden to ask them for alternative licensing structures?

In the past we have done so but they were not focused on the type of customer that would want a one-time license and would be happy to sacrifice customer service. We believe the features that are currently behind this subscription to be critical ones and believe they should be available to users who do not require an enterprise structure. We'd even be happy to see a move towards a Gitlab-like model where premium features are rolled out *first* to the enterprise subscribers before being added to the fully free version.

UPDATE: Bitwarden now offers a cheap license called [Families Organization](https://bitwarden.com/pricing/) that provides premium features and the ability to self-host Bitwarden for six persons. Please use this option as it is the correct and supported method by Bitwarden.


# Footnotes

<a name="#f1"><sup>1</sup></a> If you wish to change this you'll need to change the value that `src/licenseGen/Program.cs` uses for its `GenerateUserLicense` and `GenerateOrgLicense` calls. Remember, this is really unnecessary as this certificate does not represent any type of security-related certificate.

<a name="#f2"><sup>2</sup></a>This tool builds on top of the `bitbetter/api` container image so make sure you've built that above using the root `./build.sh` script.
