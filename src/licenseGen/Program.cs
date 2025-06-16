namespace BitwardenSelfLicensor
{
    using Microsoft.Extensions.CommandLineUtils;
    using Newtonsoft.Json;
    using SingleFileExtractor.Core;
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;

    public static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            var cert = app.Option("--cert", "cert file", CommandOptionType.SingleValue);
            var coreDll = app.Option("--core", "path to core dll", CommandOptionType.SingleValue);
            var exec = app.Option("--executable", "path to Bitwarden single file executable", CommandOptionType.SingleValue);

            bool ExecExists() => File.Exists(exec.Value());
            bool CertExists() => File.Exists(cert.Value());
            bool CoreExists() => File.Exists(coreDll.Value());
            bool VerifyTopOptions() => 
                !string.IsNullOrWhiteSpace(cert.Value()) &&
                (!string.IsNullOrWhiteSpace(coreDll.Value()) || !string.IsNullOrWhiteSpace(exec.Value())) &&
                CertExists() &&
                (CoreExists() || ExecExists());
            string GetExtractedDll()
            {
                var coreDllPath = Path.Combine("extract", "Core.dll");
                var reader = new ExecutableReader(exec.Value());
                reader.ExtractToDirectory("extract");
                var fileInfo = new FileInfo(coreDllPath);
                return fileInfo.FullName;
            }
            string GetCoreDllPath() => CoreExists() ? coreDll.Value() : GetExtractedDll();
            
            app.Command("interactive", config =>
            {
                string buff="", licensetype="", name="", email="", businessname="";
                short storage = 0;

                bool valid_guid = false, valid_installid = false;
                Guid guid = new Guid(), installid = new Guid();

                config.OnExecute(() =>
                {
                    if (!VerifyTopOptions())
                    {
                        if (!ExecExists() && !string.IsNullOrWhiteSpace(exec.Value())) config.Error.WriteLine($"Cant find single file executable at: {exec.Value()}");
                        if (!CoreExists() && !string.IsNullOrWhiteSpace(coreDll.Value())) config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
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

                            while (valid_guid == false)
                            {
                                WriteLine("Please provide the user's guid — refer to the Readme for details on how to retrieve this. [GUID]:");
                                buff = Console.ReadLine();

                                if (Guid.TryParse(buff, out guid))valid_guid = true;
                                else WriteLineOver("The user-guid provided does not appear to be valid.");
                            }
                        }
                        else if (buff == "o")
                        {
                            licensetype = "org";
                            WriteLineOver("Okay, we will generate an organization license.");

                            while (valid_installid == false)
                            {
                                WriteLine("Please provide your Bitwarden Install-ID — refer to the Readme for details on how to retrieve this. [Install-ID]:");
                                buff = Console.ReadLine();

                                if (Guid.TryParse(buff, out installid)) valid_installid = true;
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
                        WriteLineOver("Extra storage space for the user " + name + ". (max.: " + short.MaxValue + "). Defaults to maximum value. [storage]");
                        buff = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(buff))
                        {
                            storage = short.MaxValue;
                        }
                        else
                        {
                            if (CheckStorage(buff)) storage = short.Parse(buff);
                        }
                    }

                    if (licensetype == "user")
                    {
                        WriteLineOver("Confirm creation of \"user\" license for username: \"" + name + "\", email: \"" + email + "\", Storage: \"" + storage + " GB\", User-GUID: \"" + guid + "\"? Y/n");
                        buff = Console.ReadLine();
                        if ( buff == "" || buff == "y" || buff == "Y" )
                        {
                            GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), GetCoreDllPath(), name, email, storage, guid, null);
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
                            GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), GetCoreDllPath(), name, email, storage, installid, businessname, null);
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
                var name = config.Argument("Name", "your name");
                var email = config.Argument("Email", "your email");
                var userIdArg = config.Argument("User ID", "your user id");
                var storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + short.MaxValue + " (optional, default = max)");
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!VerifyTopOptions())
                    {
                        if (!ExecExists() && !string.IsNullOrWhiteSpace(exec.Value())) config.Error.WriteLine($"Cant find single file executable at: {exec.Value()}");
                        if (!CoreExists() && !string.IsNullOrWhiteSpace(coreDll.Value())) config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        if (!CertExists()) config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                        config.ShowHelp();
                        return 1;
                    }
                    else if (string.IsNullOrWhiteSpace(name.Value) || string.IsNullOrWhiteSpace(email.Value))
                    {
                        config.Error.WriteLine($"Some arguments are missing: Name='{name.Value}' Email='{email.Value}'");
                        config.ShowHelp("user");
                        return 1;
                    }

                    if (string.IsNullOrWhiteSpace(userIdArg.Value) || !Guid.TryParse(userIdArg.Value, out Guid userId))
                    {
                        config.Error.WriteLine($"User ID not provided");
                        config.ShowHelp("user");
                        return 1;
                    }

                    short storageShort = 0;
                    if (!string.IsNullOrWhiteSpace(storage.Value))
                    {
                        var parsedStorage = double.Parse(storage.Value);
                        if (parsedStorage > short.MaxValue || parsedStorage < 0)
                        {
                            config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + short.MaxValue + "]");
                            config.ShowHelp("org");
                            return 1;
                        }
                        storageShort = (short) parsedStorage;
                    }

                    GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), GetCoreDllPath(), name.Value, email.Value, storageShort, userId, key.Value);

                    return 0;
                });
            });
            app.Command("org", config =>
            {
                var name = config.Argument("Name", "your name");
                var email = config.Argument("Email", "your email");
                var installId = config.Argument("InstallId", "your installation id (GUID)");
                var storage = config.Argument("Storage", "extra storage space in GB. Maximum is " + short.MaxValue + " (optional, default = max)");
                var businessName = config.Argument("BusinessName", "name for the organization (optional)");
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!VerifyTopOptions())
                    {
                        if (!ExecExists() && !string.IsNullOrWhiteSpace(exec.Value())) config.Error.WriteLine($"Cant find single file executable at: {exec.Value()}");
                        if (!CoreExists() && !string.IsNullOrWhiteSpace(coreDll.Value())) config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        if (!CertExists()) config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                        config.ShowHelp();
                        return 1;
                    }
                    else if (string.IsNullOrWhiteSpace(name.Value) ||
                            string.IsNullOrWhiteSpace(email.Value) ||
                            string.IsNullOrWhiteSpace(installId.Value))
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

                    short storageShort = 0;
                    if (!string.IsNullOrWhiteSpace(storage.Value))
                    {
                        var parsedStorage = double.Parse(storage.Value);
                        if (parsedStorage > short.MaxValue || parsedStorage < 0)
                        {
                            config.Error.WriteLine("The storage value provided is outside the accepted range of [0-" + short.MaxValue + "]");
                            config.ShowHelp("org");
                            return 1;
                        }
                        storageShort = (short) parsedStorage;
                    }

                    GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), GetCoreDllPath(), name.Value, email.Value, storageShort, installationId, businessName.Value, key.Value);

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
        private static bool CheckUsername(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The username provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // checkBusinessName Checks that the Business Name is a valid username
        private static bool CheckBusinessName(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The Business Name provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // checkEmail Checks that the email address is a valid email address
        private static bool CheckEmail(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The email provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // checkStorage Checks that the storage is in a valid range
        private static bool CheckStorage(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                WriteLineOver("The storage provided doesn't appear to be valid.\n");
                return false;
            }
            if (double.Parse(s) > short.MaxValue || double.Parse(s) < 0)
            {
                WriteLineOver("The storage value provided is outside the accepted range of [0-" + short.MaxValue + "].\n");
                return false;
            }
            return true;
        }

        // WriteLineOver Writes a new line to console over last line.
        private static void WriteLineOver(string s)
        {
            Console.SetCursorPosition(0, Console.CursorTop -1);
            Console.WriteLine(s);
        }

        // WriteLine This wrapper is just here so that console writes all look similar.
        private static void WriteLine(string s) => Console.WriteLine(s);

        private static void GenerateUserLicense(X509Certificate2 cert, string corePath, string userName, string email, short storage, Guid userId, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Models.Business.UserLicense");
            var licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            set("LicenseKey", string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
            set("Id", userId);
            set("Name", userName);
            set("Email", email);
            set("Premium", true);
            set("MaxStorageGb", storage == 0 ? short.MaxValue : storage);
            set("Version", 1);
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(100));
            set("Trial", false);
            set("LicenseType", Enum.Parse(licenseTypeEnum, "User"));

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }

        private static void GenerateOrgLicense(X509Certificate2 cert, string corePath, string userName, string email, short storage, Guid instalId, string businessName, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Models.Business.OrganizationLicense");
            var licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");
            var planTypeEnum = core.GetType("Bit.Core.Billing.Enums.PlanType");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            set("LicenseKey", string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
            set("InstallationId", instalId);
            set("Id", Guid.NewGuid());
            set("Name", userName);
            set("BillingEmail", email);
            set("BusinessName", string.IsNullOrWhiteSpace(businessName) ? "BitBetter" : businessName);
            set("Enabled", true);
            set("Plan", "Enterprise (Annually)");
            set("PlanType", Enum.Parse(planTypeEnum, "EnterpriseAnnually"));
            set("Seats", int.MaxValue);
            set("MaxCollections", short.MaxValue);
            set("UsePolicies", true);
            set("UseSso", true);
            set("UseKeyConnector", true);
            set("UseScim", true);
            set("UseGroups", true);
            set("UseEvents", true);
            set("UseDirectory", true);
            set("UseTotp", true);
            set("Use2fa", true);
            set("UseApi", true);
            set("UseResetPassword", true);
            set("UseCustomPermissions", true);
            set("MaxStorageGb", storage == 0 ? short.MaxValue : storage);
            set("SelfHost", true);
            set("UsersGetPremium", true);
            set("UsePasswordManager", true);
            set("UseSecretsManager", true);
            set("SmSeats", int.MaxValue);
            set("SmServiceAccounts", int.MaxValue);
            set("Version", 15); //This is set to 15 to use AllowAdminAccessToAllCollectionItems can be changed to 13 to just use Secrets Manager
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(100));
            set("Trial", false);
            set("LicenseType", Enum.Parse(licenseTypeEnum, "Organization"));
            set("LimitCollectionCreationDeletion", true); //This will be used in the new version of BitWarden but can be applied now
            set("AllowAdminAccessToAllCollectionItems", true);

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }
    }
}
