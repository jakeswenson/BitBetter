using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace bitwardenSelfLicensor
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new Microsoft.Extensions.CommandLineUtils.CommandLineApplication();
            var cert = app.Option("--cert", "cert file", CommandOptionType.SingleValue);
            var coreDll = app.Option("--core", "path to core dll", CommandOptionType.SingleValue);

            bool certExists()
            {
                return File.Exists(cert.Value());
            }

            bool coreExists()
            {
                return File.Exists(coreDll.Value());
            }

            bool verifyTopOptions()
            {
                return !string.IsNullOrWhiteSpace(cert.Value()) &&
                       !string.IsNullOrWhiteSpace(coreDll.Value()) &&
                       certExists() && coreExists();
            }

            app.Command("interactive", config =>
            {
                string buff="", licensetype="", name="", email="", businessname="";

                bool valid_guid = false, valid_installid = false;
                Guid guid = new Guid(), installid = new Guid();

                config.OnExecute(() =>
                {
                    if (!verifyTopOptions())
                    {
                        if (!coreExists()) config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        if (!certExists()) config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");

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
                                else if (checkBusinessName(buff))   businessname = buff;
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
                        if ( checkUsername(buff) )   name = buff;
                    }

                    while (email == "")
                    {
                        WriteLineOver("Please provide the email address for the user " + name + ". [email]");
                        buff = Console.ReadLine();
                        if ( checkEmail(buff) )   email = buff;
                    }

                    if (licensetype == "user")
                    {
                        WriteLineOver("Confirm creation of \"user\" license for username: \"" + name + "\", email: \"" + email + "\", User-GUID: \"" + guid + "\"? Y/n");
                        buff = Console.ReadLine();
                        if ( buff == "" || buff == "y" || buff == "Y" )
                        {
                            GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name, email, guid, null);
                        }
                        else
                        {
                            WriteLineOver("Exiting...");
                            return 0;
                        }
                    }
                    else if (licensetype == "org")
                    {
                        WriteLineOver("Confirm creation of \"organization\" license for business name: \"" + businessname + "\", username: \"" + name + "\", email: \"" + email + "\", Install-ID: \"" + installid + "\"? Y/n");
                        buff = Console.ReadLine();
                        if ( buff == "" || buff == "y" || buff == "Y" )
                        {
                            GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name, email, installid, businessname, null);
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
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!verifyTopOptions())
                    {
                        if (!coreExists())
                        {
                            config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        }
                        if (!certExists())
                        {
                            config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                        }

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

                    GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, userId, key.Value);

                    return 0;
                });
            });
            app.Command("org", config =>
            {
                var name = config.Argument("Name", "your name");
                var email = config.Argument("Email", "your email");
                var installId = config.Argument("InstallId", "your installation id (GUID)");
                var businessName = config.Argument("BusinessName", "name For the organization (optional)");
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!verifyTopOptions())
                    {
                        if (!coreExists())
                        {
                            config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        }
                        if (!certExists())
                        {
                            config.Error.WriteLine($"Cant find certificate at: {cert.Value()}");
                        }

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

                    GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, installationId, businessName.Value, key.Value);

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
        static bool checkUsername(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The username provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // checkBusinessName Checks that the Business Name is a valid username
        static bool checkBusinessName(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The Business Name provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // checkEmail Checks that the email address is a valid email address
        static bool checkEmail(string s)
        {
            if ( string.IsNullOrWhiteSpace(s) ) {
                WriteLineOver("The email provided doesn't appear to be valid.\n");
                return false;
            }
            return true;    // TODO: Actually validate
        }

        // WriteLineOver Writes a new line to console over last line.
        static void WriteLineOver(string s)
        {
            Console.SetCursorPosition(0, Console.CursorTop -1);
            Console.WriteLine(s);
        }

        // WriteLine This wrapper is just here so that console writes all look similar.
        static void WriteLine(string s)
        {
            Console.WriteLine(s);
        }

        static void GenerateUserLicense(X509Certificate2 cert, string corePath,
            string userName, string email, Guid userId, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Models.Business.UserLicense");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            set("LicenseKey", string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
            set("Id", userId);
            set("Name", userName);
            set("Email", email);
            set("MaxStorageGb", short.MaxValue);
            set("Premium", true);
            set("Version", 1);
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(100));
            set("Trial", false);

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }

        static void GenerateOrgLicense(X509Certificate2 cert, string corePath,
            string userName, string email, Guid instalId, string businessName, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Models.Business.OrganizationLicense");

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
            set("Plan", "Custom");
            set("PlanType", (byte)6);
            set("Seats", (short)32767);
            set("MaxCollections", short.MaxValue);
            set("UsePolicies", true);
            set("UseSso", true);
            set("UseGroups", true);
            set("UseEvents", true);
            set("UseDirectory", true);
            set("UseTotp", true);
            set("Use2fa", true);
            set("MaxStorageGb", short.MaxValue);
            set("SelfHost", true);
            set("UsersGetPremium", true);
            set("Version", 6);
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(100));
            set("Trial", false);
            set("UseApi", true);

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }
    }
}
