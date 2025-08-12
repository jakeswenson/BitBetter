using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using dnlib.IO;

namespace bitBetter;

internal class Program
{
    private static Int32 Main()
    {
        const String certFile = "/app/cert.cert";
        String[] files = Directory.GetFiles("/app/mount", "Core.dll", SearchOption.AllDirectories);

        foreach (String file in files)
        {
            Console.WriteLine(file);
            ModuleDefMD moduleDefMd = ModuleDefMD.Load(file);
            Byte[] cert = File.ReadAllBytes(certFile);

            EmbeddedResource embeddedResourceToRemove = moduleDefMd.Resources.OfType<EmbeddedResource>().First(r => r.Name.Equals("Bit.Core.licensing.cer"));
            EmbeddedResource embeddedResourceToAdd = new("Bit.Core.licensing.cer", cert) { Attributes = embeddedResourceToRemove.Attributes };
            moduleDefMd.Resources.Add(embeddedResourceToAdd);
            moduleDefMd.Resources.Remove(embeddedResourceToRemove);

            DataReader reader = embeddedResourceToRemove.CreateReader();
            X509Certificate2 existingCert = new(reader.ReadRemainingBytes());
            
            Console.WriteLine($"Existing Cert Thumbprint: {existingCert.Thumbprint}");
            X509Certificate2 certificate = new(cert);

            Console.WriteLine($"New Cert Thumbprint: {certificate.Thumbprint}");

            IEnumerable<TypeDef> services = moduleDefMd.Types.Where(t => t.Namespace == "Bit.Core.Billing.Services");
            TypeDef type = services.First(t => t.Name == "LicensingService");
            MethodDef constructor = type.FindConstructors().First();
            
            Instruction instructionToPatch = constructor.Body.Instructions.FirstOrDefault(i => i.OpCode == OpCodes.Ldstr && String.Equals((String)i.Operand, existingCert.Thumbprint, StringComparison.InvariantCultureIgnoreCase));
            
            if (instructionToPatch != null)
            {
                instructionToPatch.Operand = certificate.Thumbprint;
            }
            else
            {
                Console.WriteLine("Can't find constructor to patch");
            }

            ModuleWriterOptions moduleWriterOptions = new(moduleDefMd);
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveRids;

            moduleDefMd.Write(file + ".new");
            moduleDefMd.Dispose();
            File.Delete(file);
            File.Move(file + ".new", file);
        }

        return 0;
    }
}