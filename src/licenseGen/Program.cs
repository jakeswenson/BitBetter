using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace licenseGen;

internal class Program
{
    private static readonly CommandLineApplication App = new();
    private static readonly CommandOption Cert = App.Option("--cert", "Certifcate file", CommandOptionType.SingleValue);
    private static readonly CommandOption CoreDll = App.Option("--core", "Path to Core.dll", CommandOptionType.SingleValue);

    private static Int32 Main(String[] args)
    {
        App.HelpOption("-? | -h | --help");

        if (!File.Exists(Cert.Value()))
        {
            App.Error.WriteLine($"Can't find certificate at: {Cert.Value()}");
            App.ShowHelp();
            return 1;
        }
        if (!File.Exists(CoreDll.Value()))
        {
            App.Error.WriteLine($"Can't find core dll at: {CoreDll.Value()}");
            App.ShowHelp();
            return 1;
        }
        if (Cert == null || String.IsNullOrWhiteSpace(Cert.Value()) || CoreDll == null || String.IsNullOrWhiteSpace(CoreDll.Value()))
        {
            App.ShowHelp();
            return 1;
        }

        App.Command("interactive", config =>
        {
            String buff, licenseType = "", name = "", email = "", businessName="";
            Int16 storage = 0;

            Boolean validGuid = false, validInstallid = false;
            Guid guid = Guid.Empty, installid = Guid.Empty;

            config.OnExecute(() =>
            {
                Console.WriteLine("Interactive license mode...");
                
                while (licenseType == "")
                {
                    Console.WriteLine("What would you like to generate, a [u]ser license or an [o]rg license: ");
                    buff = Console.ReadLine();

                    switch (buff)
                    {
                        case "u":
                        {
                            licenseType = "user";
                            Console.WriteLine("Okay, we will generate a user license.");

                            while (!validGuid)
                            {
                                Console.WriteLine("Please provide the user's guid — refer to the Readme for details on how to retrieve this. [GUID]: ");
                                buff = Console.ReadLine();

                                if (Guid.TryParse(buff, out guid))validGuid = true;
                                else Console.WriteLine("The user-guid provided does not appear to be valid!");
                            }
                            break;
                        }
                        case "o":
                        {
                            licenseType = "org";
                            Console.WriteLine("Okay, we will generate an organization license.");

                            while (!validInstallid)
                            {
                                Console.WriteLine("Please provide your Bitwarden Install-ID — refer to the Readme for details on how to retrieve this. [Install-ID]: ");
                                buff = Console.ReadLine();

                                if (Guid.TryParse(buff, out installid)) validInstallid = true;
                                else Console.WriteLine("The install-id provided does not appear to be valid.");
                            }

                            while (businessName == "")
                            {
                                Console.WriteLine("Please enter a business name, default is BitBetter. [Business Name]: ");
                                buff = Console.ReadLine();
                                if (buff == "")
                                {
                                    businessName = "BitBetter";
                                }
                                else if (CheckBusinessName(buff))
                                {
                                    businessName = buff;
                                }
                            }
                            break;
                        }
                        default:
                            Console.WriteLine("Unrecognized option \'" + buff + "\'.");
                            break;
                    }
                }

                while (name == "")
                {
                    Console.WriteLine("Please provide the username this license will be registered to. [username]: ");
                    buff = Console.ReadLine();
                    if (CheckUsername(buff))   name = buff;
                }

                while (email == "")
                {
                    Console.WriteLine("Please provide the email address for the user " + name + ". [email]: ");
                    buff = Console.ReadLine();
                    if (CheckEmail(buff))
                    {
                        email = buff;
                    }
                }

                while (storage == 0)
                {
                    Console.WriteLine("Extra storage space for the user " + name + ". (max.: " + Int16.MaxValue + "). Defaults to maximum value. [storage]");
                    buff = Console.ReadLine();
                    if (String.IsNullOrWhiteSpace(buff))
                    {
                        storage = Int16.MaxValue;
                    }
                    else
                    {
                        if (CheckStorage(buff))
                        {
                            storage = Int16.Parse(buff);
                        }
                    }
                }

                switch (licenseType)
                {
                    case "user":
                    {
                        Console.WriteLine("Confirm creation of \"user\" license for username: \"" + name + "\", email: \"" + email + "\", Storage: \"" + storage + " GB\", User-GUID: \"" + guid + "\"? Y/n");
                        buff = Console.ReadLine();
                        if (buff is "" or "y" or "Y")
                        {
                            GenerateUserLicense(new X509Certificate2(Cert.Value(), "test"), CoreDll.Value(), name, email, storage, guid, null);
                        }
                        else
                        {
                            Console.WriteLine("Exiting...");
                            return 0;
                        }

                        break;
                    }
                    case "org":
                    {
                        Console.WriteLine("Confirm creation of \"organization\" license for business name: \"" + businessName + "\", username: \"" + name + "\", email: \"" + email + "\", Storage: \"" + storage + " GB\", Install-ID: \"" + installid + "\"? Y/n");
                        buff = Console.ReadLine();
                        if (buff is "" or "y" or "Y")
                        {
                            GenerateOrgLicense(new X509Certificate2(Cert.Value(), "test"), CoreDll.Value(), name, email, storage, installid, businessName, null);
                        }
                        else
                        {
                            Console.WriteLine("Exiting...");
                            return 0;
                        }

                        break;
                    }
                }

                return 0;
            });
        });
        App.Command("user", config =>
        {
            CommandArgument name = config.Argument("Name", "your name");
            CommandArgument email = config.Argument("Email", "your email");
            CommandArgument userIdArg = config.Argument("User ID", "your user id");
            CommandArgument storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + Int16.MaxValue + " (optional, default = max)");
            CommandArgument key = config.Argument("Key", "your key id (optional)");

            config.OnExecute(() =>
            {
                if (String.IsNullOrWhiteSpace(name.Value) || String.IsNullOrWhiteSpace(email.Value))
                {
                    config.Error.WriteLine($"Some arguments are missing: Name='{name.Value}' Email='{email.Value}'");
                    config.ShowHelp(true);
                    return 1;
                }

                if (String.IsNullOrWhiteSpace(userIdArg.Value) || !Guid.TryParse(userIdArg.Value, out Guid userId))
                {
                    config.Error.WriteLine("User ID not provided");
                    config.ShowHelp(true);
                    return 1;
                }

                Int16 storageShort = 0;
                if (!String.IsNullOrWhiteSpace(storage.Value))
                {
                    Double parsedStorage = Double.Parse(storage.Value);
                    if (parsedStorage is > Int16.MaxValue or < 0)
                    {
                        config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "]");
                        config.ShowHelp(true);
                        return 1;
                    }
                    storageShort = (Int16) parsedStorage;
                }

                GenerateUserLicense(new X509Certificate2(Cert.Value()!, "test"), CoreDll.Value(), name.Value, email.Value, storageShort, userId, key.Value);

                return 0;
            });
        });
        App.Command("org", config =>
        {
            CommandArgument name = config.Argument("Name", "your name");
            CommandArgument email = config.Argument("Email", "your email");
            CommandArgument installId = config.Argument("InstallId", "your installation id (GUID)");
            CommandArgument storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + Int16.MaxValue + " (optional, default = max)");
            CommandArgument businessName = config.Argument("BusinessName", "name for the organization (optional)");
            CommandArgument key = config.Argument("Key", "your key id (optional)");
            
            config.OnExecute(() =>
            {
                if (String.IsNullOrWhiteSpace(name.Value) || String.IsNullOrWhiteSpace(email.Value) || String.IsNullOrWhiteSpace(installId.Value))
                {
                    config.Error.WriteLine($"Some arguments are missing: Name='{name.Value}' Email='{email.Value}' InstallId='{installId.Value}'");
                    config.ShowHelp(true);
                    return 1;
                }

                if (!Guid.TryParse(installId.Value, out Guid installationId))
                {
                    config.Error.WriteLine("Unable to parse your installation id as a GUID");
                    config.Error.WriteLine($"Here's a new guid: {Guid.NewGuid()}");
                    config.ShowHelp(true);
                    return 1;
                }

                Int16 storageShort = 0;
                if (!String.IsNullOrWhiteSpace(storage.Value))
                {
                    Double parsedStorage = Double.Parse(storage.Value);
                    if (parsedStorage is > Int16.MaxValue or < 0)
                    {
                        config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "]");
                        config.ShowHelp(true);
                        return 1;
                    }
                    storageShort = (Int16)parsedStorage;
                }

                GenerateOrgLicense(new X509Certificate2(Cert.Value()!, "test"), CoreDll.Value(), name.Value, email.Value, storageShort, installationId, businessName.Value, key.Value);

                return 0;
            });
        });

        App.OnExecute(() =>
        {
            App.ShowHelp();
            return 10;
        });

        try
        {
            return App.Execute(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Oops: {0}", e);
            return 100;
        }
    }

    // checkUsername Checks that the username is a valid username
    private static Boolean CheckUsername(String s)
    {
        // TODO: Actually validate
        if (!String.IsNullOrWhiteSpace(s)) return true;

        Console.WriteLine("The username provided doesn't appear to be valid!");
        return false;
    }

    // checkBusinessName Checks that the Business Name is a valid username
    private static Boolean CheckBusinessName(String s)
    {
        // TODO: Actually validate
        if (!String.IsNullOrWhiteSpace(s)) return true;

        Console.WriteLine("The Business Name provided doesn't appear to be valid!");
        return false;
    }

    // checkEmail Checks that the email address is a valid email address
    private static Boolean CheckEmail(String s)
    {
        // TODO: Actually validate
        if (!String.IsNullOrWhiteSpace(s)) return true;

        Console.WriteLine("The email provided doesn't appear to be valid!");
        return false;
    }

    // checkStorage Checks that the storage is in a valid range
    private static Boolean CheckStorage(String s)
    {
        if (String.IsNullOrWhiteSpace(s))
        {
            Console.WriteLine("The storage provided doesn't appear to be valid!");
            return false;
        }

        if (!(Double.Parse(s) > Int16.MaxValue) && !(Double.Parse(s) < 0)) return true;

        Console.WriteLine("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "]!");
        return false;
    }

    private static void GenerateUserLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid userId, String key)
    {
        Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

        Type type = core.GetType("Bit.Core.Billing.Models.Business.UserLicense");
        Type licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");

        if (type == null)
        {
            Console.WriteLine("Could not find type!");
            return;
        }
        if (licenseTypeEnum == null)
        {
            Console.WriteLine("Could not find license licenseTypeEnum!");
            return;
        }

        Object license = Activator.CreateInstance(type);

        MethodInfo computeHash = type.GetMethod("ComputeHash");
        if (computeHash == null)
        {
            Console.WriteLine("Could not find ComputeHash!");
            return;
        }

        MethodInfo sign = type.GetMethod("Sign");
        if (sign == null)
        {
            Console.WriteLine("Could not find sign!");
            return;
        }

        Set("LicenseKey", String.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
        Set("Id", userId);
        Set("Name", userName);
        Set("Email", email);
        Set("Premium", true);
        Set("MaxStorageGb", storage == 0 ? Int16.MaxValue : storage);
        Set("Version", 1);
        Set("Issued", DateTime.UtcNow);
        Set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
        Set("Expires", DateTime.UtcNow.AddYears(100));
        Set("Trial", false);
        Set("LicenseType", Enum.Parse(licenseTypeEnum, "User"));
        Set("Hash", Convert.ToBase64String(((Byte[])computeHash.Invoke(license, []))!));
        Set("Signature", Convert.ToBase64String((Byte[])sign.Invoke(license, [cert])!));

        Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        return;

        void Set(String name, Object value)
        {
            type.GetProperty(name)?.SetValue(license, value);
        }
    }

    private static void GenerateOrgLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid instalId, String businessName, String key)
    {
        Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

        Type type = core.GetType("Bit.Core.Billing.Organizations.Models");
        Type licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");
        Type planTypeEnum = core.GetType("Bit.Core.Billing.Enums.PlanType");

        if (type == null)
        {
            Console.WriteLine("Could not find type!");
            return;
        }
        if (licenseTypeEnum == null)
        {
            Console.WriteLine("Could not find licenseTypeEnum!");
            return;
        }
        if (planTypeEnum == null)
        {
            Console.WriteLine("Could not find planTypeEnum!");
            return;
        }

        Object license = Activator.CreateInstance(type);

        MethodInfo computeHash = type.GetMethod("ComputeHash");
        if (computeHash == null)
        {
            Console.WriteLine("Could not find ComputeHash!");
            return;
        }

        MethodInfo sign = type.GetMethod("Sign");
        if (sign == null)
        {
            Console.WriteLine("Could not find sign!");
            return;
        }

        Set("LicenseKey", String.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
        Set("InstallationId", instalId);
        Set("Id", Guid.NewGuid());
        Set("Name", userName);
        Set("BillingEmail", email);
        Set("BusinessName", String.IsNullOrWhiteSpace(businessName) ? "BitBetter" : businessName);
        Set("Enabled", true);
		Set("Plan", "Enterprise (Annually)");
		Set("PlanType", Enum.Parse(planTypeEnum, "EnterpriseAnnually"));
        Set("Seats", Int32.MaxValue);
        Set("MaxCollections", Int16.MaxValue);
        Set("UsePolicies", true);
        Set("UseSso", true);
        Set("UseKeyConnector", true);
        Set("UseScim", true);
        Set("UseGroups", true);
        Set("UseEvents", true);
        Set("UseDirectory", true);
        Set("UseTotp", true);
        Set("Use2fa", true);
        Set("UseApi", true);
        Set("UseResetPassword", true);
		Set("UseCustomPermissions", true);
        Set("MaxStorageGb", storage == 0 ? Int16.MaxValue : storage);
        Set("SelfHost", true);
        Set("UsersGetPremium", true);
        Set("UsePasswordManager", true);
        Set("UseSecretsManager", true);
        Set("SmSeats", Int32.MaxValue);
        Set("SmServiceAccounts", Int32.MaxValue);
        Set("Version", 15); //This is set to 15 to use AllowAdminAccessToAllCollectionItems can be changed to 13 to just use Secrets Manager
        Set("Issued", DateTime.UtcNow);
        Set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
        Set("Expires", DateTime.UtcNow.AddYears(100));
        Set("Trial", false);
        Set("LicenseType", Enum.Parse(licenseTypeEnum, "Organization"));
		Set("LimitCollectionCreationDeletion", true); //This will be used in the new version of BitWarden but can be applied now
		Set("AllowAdminAccessToAllCollectionItems", true);
		Set("UseRiskInsights", true);
		Set("UseOrganizationDomains", true);
		Set("UseAdminSponsoredFamilies", true);
		Set("UseRiskInsights", true);
        Set("UseOrganizationDomains", true);
        Set("UseAdminSponsoredFamilies", true);
        Set("Hash", Convert.ToBase64String((Byte[])computeHash.Invoke(license, [])!));
        Set("Signature", Convert.ToBase64String((Byte[])sign.Invoke(license, [cert])!));

        Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        return;

        void Set(String name, Object value)
        {
            type.GetProperty(name)?.SetValue(license, value);
        }
    }
}