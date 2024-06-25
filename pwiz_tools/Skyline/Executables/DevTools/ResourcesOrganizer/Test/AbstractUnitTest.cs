using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public abstract class AbstractUnitTest
    {
        public TestContext TestContext { get; set; }

        protected void SaveManifestResources(Type type, string destination)
        {
            var assembly = type.Assembly;
            const string suffix = ".testfile";
            string prefix = type.FullName + ".";
            foreach (var manifestResourceName in assembly.GetManifestResourceNames())
            {
                if (manifestResourceName.StartsWith(prefix) && manifestResourceName.EndsWith(suffix))
                {
                    var target = manifestResourceName.Substring(prefix.Length, manifestResourceName.Length - prefix.Length - suffix.Length);
                    using var stream = assembly.GetManifestResourceStream(manifestResourceName);
                    using var dest = File.OpenWrite(Path.Combine(destination, target));
                    stream!.CopyTo(dest);
                }
            }
        }
}
}
