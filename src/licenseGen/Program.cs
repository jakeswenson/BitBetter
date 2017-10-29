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

            bool certExists() {
                return File.Exists(cert.Value());
            }

            bool coreExists() {
                return File.Exists(coreDll.Value());
            }

            bool verifyTopOptions()
            {
                return !string.IsNullOrWhiteSpace(cert.Value()) &&
                       !string.IsNullOrWhiteSpace(coreDll.Value()) &&
                       certExists() && coreExists();
            }

            app.Command("user", config =>
            {
                var name = config.Argument("Name", "your name");
                var email = config.Argument("Email", "your email");
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!verifyTopOptions())
                    {
                        if(!coreExists())
                        {
                            config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        }
                        if (!certExists()) {
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

                    GenerateUserLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, key.Value);

                    return 0;
                });
            });
            app.Command("org", config =>
            {
                var name = config.Argument("Name", "your name");
                var email = config.Argument("Email", "your email");
                var installId = config.Argument("InstallId", "your installation id (GUID)");
                var key = config.Argument("Key", "your key id (optional)");
                var help = config.HelpOption("--help | -h | -?");

                config.OnExecute(() =>
                {
                    if (!verifyTopOptions())
                    {
                        if(!coreExists())
                        {
                            config.Error.WriteLine($"Cant find core dll at: {coreDll.Value()}");
                        }
                        if (!certExists()) {
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

                    GenerateOrgLicense(new X509Certificate2(cert.Value(), "test"), coreDll.Value(), name.Value, email.Value, installationId, key.Value);

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

        static void GenerateUserLicense(X509Certificate2 cert, string corePath,
            string userName, string email, string key)
        {
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);

            var type = core.GetType("Bit.Core.Models.Business.UserLicense");

            var license = Activator.CreateInstance(type);

            void set(string name, object value)
            {
                type.GetProperty(name).SetValue(license, value);
            }

            set("LicenseKey", string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
            set("Id", Guid.NewGuid());
            set("Name", userName);
            set("Email", email);
            set("MaxStorageGb", short.MaxValue);
            set("Premium", true);
            set("Version", 1);
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(1).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(1));
            set("Trial", false);

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }

        static void GenerateOrgLicense(X509Certificate2 cert, string corePath,
            string userName, string email, Guid instalId, string key)
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
            set("BusinessName", "BitBetter");
            set("Enabled", true);
            set("Seats", (short)5);
            set("MaxCollections", short.MaxValue);
            set("MaxStorageGb", short.MaxValue);
            set("SelfHost", true);
            set("UseGroups", true);
            set("UseDirectory", true);
            set("UseTotp", true);
            set("PlanType", (byte)6);
            set("Plan", "Custom");
            set("Version", 1);
            set("Issued", DateTime.UtcNow);
            set("Refresh", DateTime.UtcNow.AddYears(1).AddMonths(-1));
            set("Expires", DateTime.UtcNow.AddYears(1));
            set("Trial", false);

            set("Hash", Convert.ToBase64String((byte[])type.GetMethod("ComputeHash").Invoke(license, new object[0])));
            set("Signature", Convert.ToBase64String((byte[])type.GetMethod("Sign").Invoke(license, new object[] { cert })));

            Console.WriteLine(JsonConvert.SerializeObject(license, Formatting.Indented));
        }
    }
}
