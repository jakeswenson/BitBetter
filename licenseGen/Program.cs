using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace licenseGen;

internal class Program
{
    private static Int32 Main(String[] args)
    {
        CommandLineApplication app = new();
        CommandOption cert = app.Option("--cert", "cert file", CommandOptionType.SingleValue);
        CommandOption coreDll = app.Option("--core", "path to core dll", CommandOptionType.SingleValue);

        Boolean CertExists()
        {
            return File.Exists(cert.Value());
        }

        Boolean CoreExists()
        {
            return File.Exists(coreDll.Value());
        }

        Boolean VerifyTopOptions()
        {
            return !String.IsNullOrWhiteSpace(cert.Value()) &&
                   !String.IsNullOrWhiteSpace(coreDll.Value()) &&
                   CertExists() && CoreExists();
        }

        app.Command("interactive", config =>
        {
            String buff, licensetype="", name="", email="", businessname="";
            Int16 storage = 0;

            Boolean validGuid = false, validInstallid = false;
            Guid guid = new(), installid = new();

            config.OnExecute(() =>
            {
                if (!VerifyTopOptions())
                {
                    if (!CoreExists()) config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                    if (!CertExists()) config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");

                    config.ShowHelp();
                    return 1;
                }

                WriteLine("Interactive license mode...");

                while (licensetype == "")
                {
                    WriteLine("What would you like to generate, a [u]ser license or an [o]rg license?");
                    buff = Console.ReadLine();

                    if(buff == "u")
                    {
                        licensetype = "user";
                        WriteLineOver("Okay, we will generate a user license.");

                        while (validGuid == false)
                        {
                            WriteLine("Please provide the user's guid — refer to the Readme for details on how to retrieve this. [GUID]:");
                            buff = Console.ReadLine();

                            if (Guid.TryParse(buff, out guid))validGuid = true;
                            else WriteLineOver("The user-guid provided does not appear to be valid.");
                        }
                    }
                    else if (buff == "o")
                    {
                        licensetype = "org";
                        WriteLineOver("Okay, we will generate an organization license.");

                        while (validInstallid == false)
                        {
                            WriteLine("Please provide your Bitwarden Install-ID — refer to the Readme for details on how to retrieve this. [Install-ID]:");
                            buff = Console.ReadLine();

                            if (Guid.TryParse(buff, out installid)) validInstallid = true;
                            else WriteLineOver("The install-id provided does not appear to be valid.");
                        }

                        while (businessname == "")
                        {
                            WriteLineOver("Please enter a business name, default is BitBetter. [Business Name]:");
                            buff = Console.ReadLine();
                            if (buff == "")                     businessname = "BitBetter";
                            else if (CheckBusinessName(buff))   businessname = buff;
                        }
                    }
                    else
                    {
                        WriteLineOver("Unrecognized option \'" + buff + "\'. ");
                    }
                }

                while (name == "")
                {
                    WriteLineOver("Please provide the username this license will be registered to. [username]:");
                    buff = Console.ReadLine();
                    if ( CheckUsername(buff) )   name = buff;
                }

                while (email == "")
                {
                    WriteLineOver("Please provide the email address for the user " + name + ". [email]");
                    buff = Console.ReadLine();
                    if ( CheckEmail(buff) )   email = buff;
                }

                while (storage == 0)
                {
                    WriteLineOver("Extra storage space for the user " + name + ". (max.: " + Int16.MaxValue + "). Defaults to maximum value. [storage]");
                    buff = Console.ReadLine();
                    if (String.IsNullOrWhiteSpace(buff))
                    {
                        storage = Int16.MaxValue;
                    }
                    else
                    {
                        if (CheckStorage(buff)) storage = Int16.Parse(buff);
                    }
                }

                if (licensetype == "user")
                {
                    WriteLineOver("Confirm creation of \"user\" license for username: \"" + name + "\", email: \"" + email + "\", Storage: \"" + storage + " GB\", User-GUID: \"" + guid + "\"? Y/n");
                    buff = Console.ReadLine();
                    if ( buff == "" || buff == "y" || buff == "Y" )
                    {
                        GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name, email, storage, guid, null);
                    }
                    else
                    {
                        WriteLineOver("Exiting...");
                        return 0;
                    }
                }
                else if (licensetype == "org")
                {
                    WriteLineOver("Confirm creation of \"organization\" license for business name: \"" + businessname + "\", username: \"" + name + "\", email: \"" + email + "\", Storage: \"" + storage + " GB\", Install-ID: \"" + installid + "\"? Y/n");
                    buff = Console.ReadLine();
                    if ( buff == "" || buff == "y" || buff == "Y" )
                    {
                        GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name, email, storage, installid, businessname, null);
                    }
                    else
                    {
                        WriteLineOver("Exiting...");
                        return 0;
                    }
                }

                return 0;
            });
        });

        app.Command("user", config =>
        {
            CommandArgument name = config.Argument("Name", "your name");
            CommandArgument email = config.Argument("Email", "your email");
            CommandArgument userIdArg = config.Argument("User ID", "your user id");
            CommandArgument storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + Int16.MaxValue + " (optional, default = max)");
            CommandArgument key = config.Argument("Key", "your key id (optional)");

            config.OnExecute(() =>
            {
                if (!VerifyTopOptions())
                {
                    if (!CoreExists())
                    {
                        config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                    }
                    if (!CertExists())
                    {
                        config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                    }

                    config.ShowHelp();
                    return 1;
                }
                else if (String.IsNullOrWhiteSpace(name.Value) || String.IsNullOrWhiteSpace(email.Value))
                {
                    config.Error.WriteLine($"Some arguments are missing: Name='{name.Value}' Email='{email.Value}'");
                    config.ShowHelp("user");
                    return 1;
                }

                if (String.IsNullOrWhiteSpace(userIdArg.Value) || !Guid.TryParse(userIdArg.Value, out Guid userId))
                {
                    config.Error.WriteLine("User ID not provided");
                    config.ShowHelp("user");
                    return 1;
                }

                Int16 storageShort = 0;
                if (!String.IsNullOrWhiteSpace(storage.Value))
                {
                    Double parsedStorage = Double.Parse(storage.Value);
                    if (parsedStorage > Int16.MaxValue || parsedStorage < 0)
                    {
                        config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "]");
                        config.ShowHelp("org");
                        return 1;
                    }
                    storageShort = (Int16) parsedStorage;
                }

                GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, storageShort, userId, key.Value);

                return 0;
            });
        });
        app.Command("org", config =>
        {
            CommandArgument name = config.Argument("Name", "your name");
            CommandArgument email = config.Argument("Email", "your email");
            CommandArgument installId = config.Argument("InstallId", "your installation id (GUID)");
            CommandArgument storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + Int16.MaxValue + " (optional, default = max)");
            CommandArgument businessName = config.Argument("BusinessName", "name for the organization (optional)");
            CommandArgument key = config.Argument("Key", "your key id (optional)");
            
            config.OnExecute(() =>
            {
                if (!VerifyTopOptions())
                {
                    if (!CoreExists())
                    {
                        config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                    }
                    if (!CertExists())
                    {
                        config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                    }

                    config.ShowHelp();
                    return 1;
                }
                else if (String.IsNullOrWhiteSpace(name.Value) ||
                         String.IsNullOrWhiteSpace(email.Value) ||
                         String.IsNullOrWhiteSpace(installId.Value))
                {
                    config.Error.WriteLine($"Some arguments are missing: Name='{name.Value}' Email='{email.Value}' InstallId='{installId.Value}'");
                    config.ShowHelp("org");
                    return 1;
                }

                if (!Guid.TryParse(installId.Value, out Guid installationId))
                {
                    config.Error.WriteLine("Unable to parse your installation id as a GUID");
                    config.Error.WriteLine($"Here's a new guid: {Guid.NewGuid()}");
                    config.ShowHelp("org");
                    return 1;
                }

                Int16 storageShort = 0;
                if (!String.IsNullOrWhiteSpace(storage.Value))
                {
                    Double parsedStorage = Double.Parse(storage.Value);
                    if (parsedStorage > Int16.MaxValue || parsedStorage < 0)
                    {
                        config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "]");
                        config.ShowHelp("org");
                        return 1;
                    }
                    storageShort = (Int16) parsedStorage;
                }

                GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, storageShort, installationId, businessName.Value, key.Value);

                return 0;
            });
        });

        app.OnExecute(() =>
        {
            app.ShowHelp();
            return 10;
        });

        app.HelpOption("-? | -h | --help");

        try
        {
            return app.Execute(args);
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
        if ( String.IsNullOrWhiteSpace(s) ) {
            WriteLineOver("The username provided doesn't appear to be valid.\n");
            return false;
        }
        return true;    // TODO: Actually validate
    }

    // checkBusinessName Checks that the Business Name is a valid username
    private static Boolean CheckBusinessName(String s)
    {
        if ( String.IsNullOrWhiteSpace(s) ) {
            WriteLineOver("The Business Name provided doesn't appear to be valid.\n");
            return false;
        }
        return true;    // TODO: Actually validate
    }

    // checkEmail Checks that the email address is a valid email address
    private static Boolean CheckEmail(String s)
    {
        if ( String.IsNullOrWhiteSpace(s) ) {
            WriteLineOver("The email provided doesn't appear to be valid.\n");
            return false;
        }
        return true;    // TODO: Actually validate
    }

    // checkStorage Checks that the storage is in a valid range
    private static Boolean CheckStorage(String s)
    {
        if (String.IsNullOrWhiteSpace(s))
        {
            WriteLineOver("The storage provided doesn't appear to be valid.\n");
            return false;
        }
        if (Double.Parse(s) > Int16.MaxValue || Double.Parse(s) < 0)
        {
            WriteLineOver("The storage value provided is outside the accepted range of [0-" + Int16.MaxValue + "].\n");
            return false;
        }
        return true;
    }

    // WriteLineOver Writes a new line to console over last line.
    private static void WriteLineOver(String s)
    {
        Console.SetCursorPosition(0, Console.CursorTop -1);
        Console.WriteLine(s);
    }

    // WriteLine This wrapper is just here so that console writes all look similar.
    private static void WriteLine(String s)
    {
        Console.WriteLine(s);
    }

    private static void GenerateUserLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid userId, String key)
    {
        Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

        Type type = core.GetType("Bit.Core.Models.Business.UserLicense");
        Type licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");

        Object license = Activator.CreateInstance(type);

        void Set(String name, Object value)
        {
            type.GetProperty(name).SetValue(license, value);
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

        Set("Hash", Convert.ToBase64String((Byte[])type.GetMethod("ComputeHash").Invoke(license, new Object[0])));
        Set("Signature", Convert.ToBase64String((Byte[])type.GetMethod("Sign").Invoke(license, new Object[] { cert })));

        Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
    }

    private static void GenerateOrgLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid instalId, String businessName, String key)
    {
        Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

        Type type = core.GetType("Bit.Core.Models.Business.OrganizationLicense");
        Type licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");
        Type planTypeEnum = core.GetType("Bit.Core.Enums.PlanType");

        Object license = Activator.CreateInstance(type);

        void set(String name, Object value)
        {
            type.GetProperty(name).SetValue(license, value);
        }

        set("LicenseKey", String.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
        set("InstallationId", instalId);
        set("Id", Guid.NewGuid());
        set("Name", userName);
        set("BillingEmail", email);
        set("BusinessName", String.IsNullOrWhiteSpace(businessName) ? "BitBetter" : businessName);
        set("Enabled", true);
        set("Plan", "Custom");
        set("PlanType", Enum.Parse(planTypeEnum, "Custom"));
        set("Seats", (Int32)Int16.MaxValue);
        set("MaxCollections", Int16.MaxValue);
        set("UsePolicies", true);
        set("UseSso", true);
        set("UseKeyConnector", true);
        //set("UseScim", true); // available in version 10, which is not released yet
        set("UseGroups", true);
        set("UseEvents", true);
        set("UseDirectory", true);
        set("UseTotp", true);
        set("Use2fa", true);
        set("UseApi", true);
        set("UseResetPassword", true);
        set("MaxStorageGb", storage == 0 ? Int16.MaxValue : storage);
        set("SelfHost", true);
        set("UsersGetPremium", true);
        set("Version", 9);
        set("Issued", DateTime.UtcNow);
        set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
        set("Expires", DateTime.UtcNow.AddYears(100));
        set("Trial", false);
        set("LicenseType", Enum.Parse(licenseTypeEnum, "Organization"));

        set("Hash", Convert.ToBase64String((Byte[])type.GetMethod("ComputeHash").Invoke(license, new Object[0])));
        set("Signature", Convert.ToBase64String((Byte[])type.GetMethod("Sign").Invoke(license, new Object[] { cert })));

        Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
    }
}
