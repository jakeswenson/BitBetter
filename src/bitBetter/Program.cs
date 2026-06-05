using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using dnlib.IO;
using SingleFileExtractor.Core;

namespace bitBetter;

internal class Program
{
	private static Int32 Main()
	{
		const String certFile = "/app/cert.cer";

		foreach (String iniFile in Directory.GetFiles("/app/mount/", "*.ini", SearchOption.TopDirectoryOnly))
		{
			Console.WriteLine("Patching: " + iniFile);

			String[] lines = File.ReadAllLines(iniFile);
			for (Int32 i = 0; i < lines.Length; i++)
			{
				String line = lines[i];
				if (!line.StartsWith("command=", StringComparison.Ordinal)) continue;

				String appNameAndPath = line[(line.LastIndexOf('=') + 1)..];
				lines[i] = "command=/usr/bin/dotnet \"" + appNameAndPath + ".dll\" --runtimeconfig \"" + appNameAndPath + ".runtimeconfig.json\"";
				break;
			}
			File.WriteAllText(iniFile, String.Join("\n", lines), new UTF8Encoding(false));
		}

		foreach (String singleFile in Directory.GetFiles("/app/mount/", "*", SearchOption.AllDirectories))
		{
			if (Path.HasExtension(singleFile)) continue;

			Console.WriteLine("Extracting: " + singleFile);

			ExecutableReader reader1 = new(singleFile);
			String currentDirectory = Path.GetDirectoryName(singleFile);
			String newCoreDll = Path.Combine(currentDirectory, "Core.dll");
			reader1.ExtractToDirectory(currentDirectory);
			reader1.Dispose();

			File.Delete(singleFile);

			if (!File.Exists(newCoreDll))
			{
				Console.WriteLine("Could not extract Core.dll for " + singleFile);
				Environment.Exit(-1);
			}

			Console.WriteLine("Extracted: " + newCoreDll);
			ModuleDefMD moduleDefMd = ModuleDefMD.Load(newCoreDll);
			Byte[] cert = File.ReadAllBytes(certFile);

			EmbeddedResource embeddedResourceToRemove = moduleDefMd.Resources.OfType<EmbeddedResource>().First(r => r.Name.Equals("Bit.Core.licensing.cer"));
			EmbeddedResource embeddedResourceToAdd = new("Bit.Core.licensing.cer", cert) { Attributes = embeddedResourceToRemove.Attributes };
			moduleDefMd.Resources.Add(embeddedResourceToAdd);
			moduleDefMd.Resources.Remove(embeddedResourceToRemove);

			DataReader reader = embeddedResourceToRemove.CreateReader();
			
			X509Certificate2 existingCert = X509CertificateLoader.LoadCertificate(reader.ReadRemainingBytes());
			X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(cert);
			Console.WriteLine($"Existing certificate Thumbprint: {existingCert.Thumbprint}");
			Console.WriteLine($"New certificate Thumbprint: {certificate.Thumbprint}");

			// Find LicensingService by class name (namespace-agnostic to handle renames)
			TypeDef type = moduleDefMd.Types.FirstOrDefault(t => String.Equals(t.Name, "LicensingService", StringComparison.OrdinalIgnoreCase));
			if (type == null)
			{
				Console.Error.WriteLine("ERROR: LicensingService class not found");
				return -1;
			}
			Console.WriteLine($"Found: {type.FullName}");

			MethodDef constructor = type.FindConstructors().First();

			if (constructor == null)
			{
				Console.Error.WriteLine("ERROR: Cannot find constructor");
				return -1;
			}

			Instruction[] instructionToPatch = constructor.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Where(i => ((String)i.Operand)
                    .Contains(existingCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                .ToArray();

			if (instructionToPatch.Length > 0)
			{
                Console.WriteLine($"Found {instructionToPatch.Length} thumbprint Ldstr instruction(s) to replace");
                foreach (Instruction inst in instructionToPatch)
                {
                    Console.WriteLine($"  Replacing: '{inst.Operand}'");
                    inst.Operand = certificate.Thumbprint;
                }
            }
			else
			{
				Console.WriteLine("ERROR: Can't find instruction to patch");
				return -1;
			}

			Console.WriteLine("Writing: " + newCoreDll);

            ModuleWriterOptions moduleWriterOptions = new(moduleDefMd);
			moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
			moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
			moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveRids;

			moduleDefMd.Write(newCoreDll + ".new");
			moduleDefMd.Dispose();
			File.Delete(newCoreDll);
			File.Move(newCoreDll + ".new", newCoreDll);
		}

		foreach (String runtimeConfigFile in Directory.GetFiles("/app/mount/", "*.runtimeconfig.json", SearchOption.AllDirectories))
		{
			Console.WriteLine("Patching: " + runtimeConfigFile);

			String[] lines = File.ReadAllLines(runtimeConfigFile);
			for (Int32 i = 0; i < lines.Length; i++)
			{
				String line = lines[i];
				if (!line.Contains("includedFrameworks", StringComparison.Ordinal)) continue;

				lines[i] = lines[i].Replace("includedFrameworks", "frameworks", StringComparison.Ordinal);
				break;
			}
			File.WriteAllText(runtimeConfigFile, String.Join("\n", lines), new UTF8Encoding(false));
		}

		return 0;
	}
}
