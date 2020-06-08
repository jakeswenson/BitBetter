using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace bitwardenSelfLicensor
{
    class Program
    {
        static int Main(string[] args)
        {
            string cerFile;
            string corePath;

            if(args.Length >= 2) {
                cerFile = args[0];
                corePath = args[1];
            } else if (args.Length == 1) {
                cerFile = args[0];
                corePath = "/app/Core.dll";
            }
            else {
                cerFile = "/newLicensing.cer";
                corePath = "/app/Core.dll";
            }


            var module =  ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(corePath)));
            var cert = File.ReadAllBytes(cerFile);

            var x = module.Resources.OfType<EmbeddedResource>()
                                    .Where(r => r.Name.Equals("Bit.Core.licensing.cer"))
                                    .First();

            Console.WriteLine(x.Name);

            var e = new EmbeddedResource("Bit.Core.licensing.cer", x.Attributes, cert);

            module.Resources.Add(e);
            module.Resources.Remove(x);

            var services = module.Types.Where(t => t.Namespace == "Bit.Core.Services");
            

            var type = services.First(t => t.Name == "LicensingService");

            var licensingType =  type.Resolve();

            var existingCert = new X509Certificate2(x.GetResourceData());

            Console.WriteLine($"Existing Cert Thumbprint: {existingCert.Thumbprint}");
            X509Certificate2 certificate = new X509Certificate2(cert);

            Console.WriteLine($"New Cert Thumbprint: {certificate.Thumbprint}");

            var ctor = licensingType.GetConstructors().Single();


            var rewriter = ctor.Body.GetILProcessor();

            var instToReplace = 
                ctor.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr
                    && string.Equals((string)i.Operand, existingCert.Thumbprint, StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault();

            if(instToReplace != null) {
                rewriter.Replace(instToReplace, Instruction.Create(OpCodes.Ldstr, certificate.Thumbprint));
            }
            else {
                Console.WriteLine("Cant find inst");
            }

            // foreach (var inst in ctor.Body.Instructions)
            // {
            //     Console.Write(inst.OpCode.Name + " " + inst.Operand?.GetType() + " = ");
            //     if(inst.OpCode.FlowControl == FlowControl.Call) {
            //         Console.WriteLine(inst.Operand);
            //     }
            //     else if(inst.OpCode == OpCodes.Ldstr) {
            //         Console.WriteLine(inst.Operand);
            //     }
            //     else {Console.WriteLine();}
            // }

            module.Write("modified.dll");

            return 0;
        }
    }
}
