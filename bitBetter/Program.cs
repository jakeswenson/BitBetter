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
    private static Int32 Main(String[] args)
    {
        const String certFile = "/app/cert.cert";
        String[] files = Directory.GetFiles("/app/mount", "Core.dll", SearchOption.AllDirectories);

        foreach (String file in files)
        {
            Console.WriteLine(file);
            ModuleDefMD moduleDefMd = ModuleDefMD.Load(file);
            Byte[] cert = File.ReadAllBytes(certFile);

            EmbeddedResource embeddedResourceToRemove = moduleDefMd.Resources
                .OfType<EmbeddedResource>()
                .First(r => r.Name.Equals("Bit.Core.licensing.cer"));

            Console.WriteLine(embeddedResourceToRemove.Name);

            EmbeddedResource embeddedResourceToAdd = new("Bit.Core.licensing.cer", cert) {Attributes = embeddedResourceToRemove.Attributes };
            moduleDefMd.Resources.Add(embeddedResourceToAdd);
            moduleDefMd.Resources.Remove(embeddedResourceToRemove);

            DataReader reader = embeddedResourceToRemove.CreateReader();
            X509Certificate2 existingCert = new(reader.ReadRemainingBytes());
            
            Console.WriteLine($"Existing Cert Thumbprint: {existingCert.Thumbprint}");
            X509Certificate2 certificate = new(cert);

            Console.WriteLine($"New Cert Thumbprint: {certificate.Thumbprint}");

            IEnumerable<TypeDef> services = moduleDefMd.Types.Where(t => t.Namespace == "Bit.Core.Services");
            TypeDef type = services.First(t => t.Name == "LicensingService");
            MethodDef constructor = type.FindConstructors().First();
            
            Instruction instToReplace =
                constructor.Body.Instructions
                    .FirstOrDefault(i => i.OpCode == OpCodes.Ldstr
                                         && String.Equals((String)i.Operand, existingCert.Thumbprint, StringComparison.InvariantCultureIgnoreCase));
            
            if (instToReplace != null)
            {
                Int32 index = constructor.Body.Instructions.IndexOf(instToReplace);
                constructor.Body.Instructions.Remove(instToReplace);
                constructor.Body.Instructions.Insert(index, Instruction.Create(OpCodes.Ldstr, certificate.Thumbprint));
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