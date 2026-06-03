namespace BitwardenSelfLicensor
{
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.IdentityModel.Tokens;
    using Newtonsoft.Json;
    using SingleFileExtractor.Core;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.IO;
    using System.Runtime.Loader;
    using System.Security.Claims;
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
                            GenerateUserLicense(X509CertificateLoader.LoadPkcs12FromFile(cert.Value(), "test"), GetCoreDllPath(), name, email, storage, guid, null);
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
                            GenerateOrgLicense(X509CertificateLoader.LoadPkcs12FromFile(cert.Value(), "test"), GetCoreDllPath(), name, email, storage, installid, businessname, null);
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

                    GenerateUserLicense(X509CertificateLoader.LoadPkcs12FromFile(cert.Value(), "test"), GetCoreDllPath(), name.Value, email.Value, storageShort, userId, key.Value);

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

                    GenerateOrgLicense(X509CertificateLoader.LoadPkcs12FromFile(cert.Value(), "test"), GetCoreDllPath(), name.Value, email.Value, storageShort, installationId, businessName.Value, key.Value);

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

            var type = core.GetType("Bit.Core.Billing.Models.Business.UserLicense");
            var licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            var licenseKey = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key;

            set("LicenseKey", licenseKey);
            set("Id", userId);
            set("Name", userName);
            set("Email", email);
            set("Premium", true);
            set("MaxStorageGb", storage == 0 ? short.MaxValue : storage);
            set("Version", 1);
            var now = DateTime.UtcNow;
            set("Issued", now);
            set("Refresh", now.AddYears(100).AddMonths(-1));
            set("Expires", now.AddYears(100));
            set("Trial", false);
            set("LicenseType", Enum.Parse(licenseTypeEnum, "User"));

            set("Token", GenerateUserToken(cert, userId, licenseKey, userName, email, storage, now));
            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }

        private static void GenerateOrgLicense(X509Certificate2 cert, string corePath, string userName, string email, short storage, Guid instalId, string businessName, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Billing.Organizations.Models.OrganizationLicense");
            var licenseTypeEnum = core.GetType("Bit.Core.Enums.LicenseType");
            var planTypeEnum = core.GetType("Bit.Core.Billing.Enums.PlanType");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            var licenseKey = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key;
            var businessNameFinal = string.IsNullOrWhiteSpace(businessName) ? "BitBetter" : businessName;

            set("LicenseKey", licenseKey);
            set("InstallationId", instalId);
            set("Id", Guid.NewGuid());
            set("Name", userName);
            set("BillingEmail", email);
            set("BusinessName", businessNameFinal);
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
            set("Version", 16);
            var now = DateTime.UtcNow;
            set("Issued", now);
            set("Refresh", now.AddYears(100).AddMonths(-1));
            set("Expires", now.AddYears(100));
            set("ExpirationWithoutGracePeriod", now.AddYears(100));
            set("Trial", false);
            set("LicenseType", Enum.Parse(licenseTypeEnum, "Organization"));
            set("LimitCollectionCreationDeletion", true);
            set("AllowAdminAccessToAllCollectionItems", true);
            set("UseRiskInsights", true);
            set("UseOrganizationDomains", true);
            set("UseAdminSponsoredFamilies", true);
            set("UseAutomaticUserConfirmation", true);
            set("UsePhishingBlocker", true);
            set("UseDisableSmAdsForUsers", true);
            set("UseMyItems", true);

            var orgId = (Guid)type.GetProperty("Id").GetValue(license);

            set("Token", GenerateOrgToken(cert, orgId, instalId, licenseKey, email, businessNameFinal, userName, storage, planTypeEnum, now));
            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }

        private static string GenerateUserToken(X509Certificate2 cert, Guid userId, string licenseKey, string name, string email, short maxStorageGb, DateTime now)
        {
            var secKey  = new X509SecurityKey(cert);
            var creds   = new SigningCredentials(secKey, SecurityAlgorithms.RsaSha256);
            var expires = now.AddYears(100);

            var claims = new List<Claim>
            {
                new Claim("LicenseType",  "User"),
                new Claim("LicenseKey",   licenseKey),
                new Claim("Id",           userId.ToString()),
                new Claim("Name",         name),
                new Claim("Email",        email),
                new Claim("Premium",      "true"),
                new Claim("MaxStorageGb", (maxStorageGb == 0 ? short.MaxValue : maxStorageGb).ToString()),
                new Claim("Trial",        "false"),
                new Claim("Issued",       now.ToString("o")),
                new Claim("Expires",      expires.ToString("o")),
                new Claim("Refresh",      now.AddYears(100).AddMonths(-1).ToString("o")),
            };

            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(
                issuer:             "bitwarden",
                audience:           $"user:{userId}",
                claims:             claims,
                notBefore:          now,
                expires:            expires,
                signingCredentials: creds);

            return handler.WriteToken(token);
        }

        private static string GenerateOrgToken(X509Certificate2 cert, Guid orgId, Guid installationId, string licenseKey, string billingEmail, string businessName, string name, short maxStorageGb, Type planTypeEnum, DateTime now)
        {
            var secKey  = new X509SecurityKey(cert);
            var creds   = new SigningCredentials(secKey, SecurityAlgorithms.RsaSha256);
            var expires = now.AddYears(100);

            // Resolve the integer value of EnterpriseAnnually from the runtime enum
            var planTypeInt = Convert.ToInt32(Enum.Parse(planTypeEnum, "EnterpriseAnnually"));

            var claims = new List<Claim>
            {
                new Claim("LicenseType",                          "Organization"),
                new Claim("LicenseKey",                           licenseKey),
                new Claim("InstallationId",                       installationId.ToString()),
                new Claim("Id",                                   orgId.ToString()),
                new Claim("Name",                                 name),
                new Claim("BillingEmail",                         billingEmail),
                new Claim("BusinessName",                         businessName),
                new Claim("Enabled",                              "true"),
                new Claim("Plan",                                 "Enterprise (Annually)"),
                new Claim("PlanType",                             planTypeInt.ToString()),
                new Claim("Seats",                                int.MaxValue.ToString()),
                new Claim("MaxCollections",                       short.MaxValue.ToString()),
                new Claim("MaxStorageGb",                         (maxStorageGb == 0 ? short.MaxValue : maxStorageGb).ToString()),
                new Claim("SelfHost",                             "true"),
                new Claim("UsersGetPremium",                      "true"),
                new Claim("UseGroups",                            "true"),
                new Claim("UseDirectory",                         "true"),
                new Claim("UseEvents",                            "true"),
                new Claim("UseTotp",                              "true"),
                new Claim("Use2fa",                               "true"),
                new Claim("UseApi",                               "true"),
                new Claim("UsePolicies",                          "true"),
                new Claim("UseSso",                               "true"),
                new Claim("UseResetPassword",                     "true"),
                new Claim("UseKeyConnector",                      "true"),
                new Claim("UseScim",                              "true"),
                new Claim("UseCustomPermissions",                 "true"),
                new Claim("UsePasswordManager",                   "true"),
                new Claim("UseSecretsManager",                    "true"),
                new Claim("SmSeats",                              int.MaxValue.ToString()),
                new Claim("SmServiceAccounts",                    int.MaxValue.ToString()),
                new Claim("UseRiskInsights",                      "true"),
                new Claim("UseAdminSponsoredFamilies",            "true"),
                new Claim("UseOrganizationDomains",               "true"),
                new Claim("UseAutomaticUserConfirmation",         "true"),
                new Claim("UseDisableSmAdsForUsers",              "true"),
                new Claim("UsePhishingBlocker",                   "true"),
                new Claim("UseMyItems",                           "true"),
                new Claim("LimitCollectionCreationDeletion",      "true"),
                new Claim("AllowAdminAccessToAllCollectionItems", "true"),
                new Claim("Trial",                                "false"),
                new Claim("Issued",                               now.ToString("o")),
                new Claim("Expires",                              expires.ToString("o")),
                new Claim("Refresh",                              now.AddYears(100).AddMonths(-1).ToString("o")),
                new Claim("ExpirationWithoutGracePeriod",         expires.ToString("o")),
            };

            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(
                issuer:             "bitwarden",
                audience:           $"organization:{orgId}",
                claims:             claims,
                notBefore:          now,
                expires:            expires,
                signingCredentials: creds);

            return handler.WriteToken(token);
        }
    }
}
