using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using McMaster.Extensions.CommandLineUtils;

namespace licenseGen;

internal class Program
{
	private static readonly CommandLineApplication App = new();
	private static readonly CommandOption Cert = App.Option("--cert", "Certificate file", CommandOptionType.SingleValue);
	private static readonly CommandOption CoreDll = App.Option("--core", "Path to Core.dll", CommandOptionType.SingleValue);

	private static Int32 Main(String[] args)
	{
		App.Command("interactive", config =>
		{
			String buff, licenseType = "", name = "", email = "", businessName="";
			Int16 storage = 0;
			Boolean validGuid = false, validInstallid = false;
			Guid guid = Guid.Empty, installid = Guid.Empty;

			config.OnExecute(() =>
			{
				Check();
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
				Check();

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
				Check();

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
			App.HelpOption("-? | -h | --help");
			return App.Execute(args);
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine("Oops: {0}", exception);
			return 100;
		}
	}

	private static void Check()
	{
		if (Cert == null || String.IsNullOrWhiteSpace(Cert.Value()))
		{
			App.Error.WriteLine("No certificate specified");
			App.ShowHelp();
			Environment.Exit(1);
		}
		else if (CoreDll == null || String.IsNullOrWhiteSpace(CoreDll.Value()))
		{
			App.Error.WriteLine("No core dll specified");
			App.ShowHelp();
			Environment.Exit(1);
		}
		else if (!File.Exists(Cert.Value()))
		{
			App.Error.WriteLine($"Can't find certificate at: {Cert.Value()}");
			App.ShowHelp();
			Environment.Exit(1);
		}
		else if (!File.Exists(CoreDll.Value()))
		{
			App.Error.WriteLine($"Can't find core dll at: {CoreDll.Value()}");
			App.ShowHelp();
			Environment.Exit(1);
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

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private static void GenerateUserLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid userId, String key)
	{
		Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(corePath));

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

		Set(type, license, "LicenseKey", String.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
		Set(type, license, "Id", userId);
		Set(type, license, "Name", userName);
		Set(type, license, "Email", email);
		Set(type, license, "Premium", true);
		Set(type, license, "MaxStorageGb", storage == 0 ? Int16.MaxValue : storage);
		Set(type, license, "Version", 1);
		Set(type, license, "Issued", DateTime.UtcNow);
		Set(type, license, "Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
		Set(type, license, "Expires", DateTime.UtcNow.AddYears(100));
		Set(type, license, "Trial", false);
		Set(type, license, "LicenseType", Enum.Parse(licenseTypeEnum, "User"));
		Set(type, license, "Hash", Convert.ToBase64String(((Byte[])computeHash.Invoke(license, []))!));
		Set(type, license, "Signature", Convert.ToBase64String((Byte[])sign.Invoke(license, [cert])!));

		Console.WriteLine(JsonSerializer.Serialize(license, JsonOptions));
	}
	private static void GenerateOrgLicense(X509Certificate2 cert, String corePath, String userName, String email, Int16 storage, Guid instalId, String businessName, String key)
	{
		Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(corePath));
		Type type = core.GetType("Bit.Core.Billing.Organizations.Models.OrganizationLicense");
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

		Set(type, license, "LicenseKey", String.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("n") : key);
		Set(type, license, "InstallationId", instalId);
		Set(type, license, "Id", Guid.NewGuid());
		Set(type, license, "Name", userName);
		Set(type, license, "BillingEmail", email);
		Set(type, license, "BusinessName", String.IsNullOrWhiteSpace(businessName) ? "BitBetter" : businessName);
		Set(type, license, "Enabled", true);
		Set(type, license, "Plan", "Enterprise (Annually)");
		Set(type, license, "PlanType", Enum.Parse(planTypeEnum, "EnterpriseAnnually"));
		Set(type, license, "Seats", Int32.MaxValue);
		Set(type, license, "MaxCollections", Int16.MaxValue);
		Set(type, license, "UsePolicies", true);
		Set(type, license, "UseSso", true);
		Set(type, license, "UseKeyConnector", true);
		Set(type, license, "UseScim", true);
		Set(type, license, "UseGroups", true);
		Set(type, license, "UseEvents", true);
		Set(type, license, "UseDirectory", true);
		Set(type, license, "UseTotp", true);
		Set(type, license, "Use2fa", true);
		Set(type, license, "UseApi", true);
		Set(type, license, "UseResetPassword", true);
		Set(type, license, "MaxStorageGb", storage == 0 ? Int16.MaxValue : storage);
		Set(type, license, "SelfHost", true);
		Set(type, license, "UsersGetPremium", true);		
		Set(type, license, "UseCustomPermissions", true);
		Set(type, license, "Version", 16);
		Set(type, license, "Issued", DateTime.UtcNow);
		Set(type, license, "Refresh", DateTime.UtcNow.AddYears(100).AddMonths(-1));
		Set(type, license, "Expires", DateTime.UtcNow.AddYears(100));
		Set(type, license, "ExpirationWithoutGracePeriod", DateTime.UtcNow.AddYears(100));
		Set(type, license, "UsePasswordManager", true);
		Set(type, license, "UseSecretsManager", true);
		Set(type, license, "SmSeats", Int32.MaxValue);
		Set(type, license, "SmServiceAccounts", Int32.MaxValue);
		Set(type, license, "UseRiskInsights", true);
		Set(type, license, "Trial", false);
		Set(type, license, "LicenseType", Enum.Parse(licenseTypeEnum, "Organization"));
		Set(type, license, "UseOrganizationDomains", true);
		Set(type, license, "UseAdminSponsoredFamilies", true);
		Set(type, license, "UsePhishingBlocker", true);
		Set(type, license, "Hash", Convert.ToBase64String((Byte[])computeHash.Invoke(license, [])!));
		Set(type, license, "Signature", Convert.ToBase64String((Byte[])sign.Invoke(license, [cert])!));

		Console.WriteLine(JsonSerializer.Serialize(license, JsonOptions));
	}
	private static void Set(Type type, Object license, String name, Object value)
	{
		type.GetProperty(name)?.SetValue(license, value);
	}
}