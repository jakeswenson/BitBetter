using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using SingleFileExtractor.Core;

namespace BitwardenSelfLicensor
{
    class Program
    {
        static int Main(string[] args)
        {
            string cerFile   = args.Length >= 1 ? args[0] : "/newLicensing.cer";
            string inputPath = args.Length >= 2 ? args[1] : "/app/Api";

            string coreDllPath;
            string extractDir = Path.GetDirectoryName(Path.GetFullPath(inputPath));

            if (string.Equals(Path.GetExtension(inputPath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                // Input is already a direct Core.dll path — skip bundle extraction
                coreDllPath = inputPath;
            }
            else
            {
                try
                {
                    var reader = new ExecutableReader(inputPath);
                    reader.ExtractToDirectory(extractDir);
                    Console.WriteLine($"Extracted bundle to {extractDir}");
                    coreDllPath = Path.Combine(extractDir, "Core.dll");

                    // The extracted runtimeconfig.json is in self-contained format (no "framework" key).
                    // Running "dotnet App.dll" requires framework-dependent format; without it .NET looks
                    // for libhostpolicy.so in the app dir (which isn't there) and crashes.
                    FixRuntimeConfig(extractDir, Path.GetFileNameWithoutExtension(inputPath));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to extract single-file bundle '{inputPath}': {ex.Message}");
                    return 1;
                }
            }

            Console.WriteLine($"Patching: {coreDllPath}");

            var certBytes = File.ReadAllBytes(cerFile);
            var module    = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(coreDllPath)));

            // Replace embedded certificate resource
            var existingRes = module.Resources
                .OfType<EmbeddedResource>()
                .FirstOrDefault(r => r.Name == "Bit.Core.licensing.cer");

            if (existingRes == null)
            {
                Console.Error.WriteLine("ERROR: Embedded resource 'Bit.Core.licensing.cer' not found in Core.dll");
                return 1;
            }

            Console.WriteLine($"Found resource: {existingRes.Name}");
            module.Resources.Add(new EmbeddedResource("Bit.Core.licensing.cer", existingRes.Attributes, certBytes));
            module.Resources.Remove(existingRes);

            var existingCert = X509CertificateLoader.LoadCertificate(existingRes.GetResourceData());
            var newCert      = X509CertificateLoader.LoadCertificate(certBytes);
            Console.WriteLine($"Old thumbprint: {existingCert.Thumbprint}");
            Console.WriteLine($"New thumbprint: {newCert.Thumbprint}");

            // Find LicensingService by class name (namespace-agnostic to handle renames)
            var type = module.Types.FirstOrDefault(t => t.Name == "LicensingService");
            if (type == null)
            {
                Console.Error.WriteLine("ERROR: LicensingService not found in Core.dll");
                return 1;
            }
            Console.WriteLine($"Found: {type.FullName}");

            var ctor     = type.Resolve().GetConstructors().First();
            var rewriter = ctor.Body.GetILProcessor();

            // Use Contains() to handle the hidden Unicode LRM character (\u200E) that Bitwarden
            // prepends to the production thumbprint string literal in LicensingService.cs
            // Replace ALL occurrences since const fields are inlined at compile time and used in
            // multiple validation checks (both _creationCertificate and _verificationCertificates)
            var instructionsToReplace = ctor.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Where(i => ((string)i.Operand)
                    .Contains(existingCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (instructionsToReplace.Count > 0)
            {
                Console.WriteLine($"Found {instructionsToReplace.Count} thumbprint Ldstr instruction(s) to replace");
                foreach (var inst in instructionsToReplace)
                {
                    Console.WriteLine($"  Replacing: '{inst.Operand}'");
                    rewriter.Replace(inst, Instruction.Create(OpCodes.Ldstr, newCert.Thumbprint));
                }
            }
            else
            {
                Console.WriteLine("WARNING: Thumbprint Ldstr not found — cert resource replaced anyway");
            }

            module.Write(coreDllPath);
            Console.WriteLine("Done.");
            return 0;
        }

        // Converts a self-contained runtimeconfig.json to framework-dependent so the app can be
        // launched with "dotnet App.dll" using the system-installed ASP.NET Core runtime.
        static void FixRuntimeConfig(string dir, string appName)
        {
            var path = Path.Combine(dir, $"{appName}.runtimeconfig.json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"runtimeconfig not found at {path}, skipping");
                return;
            }

            var root = JsonNode.Parse(File.ReadAllText(path))!;
            var opts = root["runtimeOptions"]!.AsObject();

            // Derive framework name/version from the self-contained includedFrameworks before removing it
            string fwName    = "Microsoft.AspNetCore.App";
            string fwVersion = "10.0.0";
            if (opts["includedFrameworks"] is JsonArray included && included.Count > 0)
            {
                var first = included[0]!.AsObject();
                fwName    = first["name"]?.GetValue<string>()    ?? fwName;
                fwVersion = first["version"]?.GetValue<string>() ?? fwVersion;
            }

            // Remove self-contained markers
            opts.Remove("includedFrameworks");

            // Add framework-dependent reference (LatestPatch allows compatibility across patch releases only)
            opts["framework"] = new JsonObject
            {
                ["name"]    = fwName,
                ["version"] = fwVersion
            };
            opts["rollForward"] = "LatestPatch";

            File.WriteAllText(path, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Fixed runtimeconfig: {path}");
        }
    }
}
