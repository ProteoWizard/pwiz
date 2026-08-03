/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Skyline runs inside the SkylineCmd.exe process, so SkylineCmd.exe.config supplies its
    /// binding policy. It used to be maintained by hand, went stale, and .parquet export
    /// worked in the UI while failing from the command-line. The CopyConfigToSkylineCmd
    /// target in Skyline.csproj now writes it. This checks the copy happened, and that the
    /// policy covers every dependency some shipping assembly wants at a version other than
    /// the one deployed, which is the check that would have caught the original bug.
    /// </summary>
    [TestClass]
    public class SkylineCmdConfigTest : AbstractUnitTest
    {
        private const string SKYLINE_CMD_CONFIG = "SkylineCmd.exe.config";
        private static readonly XNamespace ASM_NS = @"urn:schemas-microsoft-com:asm.v1";

        // Skyline builds as Skyline-daily.exe unless MSBuildAssemblyName overrides it
        private static readonly string[] SKYLINE_CONFIGS = { @"Skyline.exe.config", @"Skyline-daily.exe.config" };

        /// <summary>
        /// The reflection-only loader cannot read every file, and a silent skip would stop
        /// this test checking anything. Each of these is a referrer that needs a redirect.
        /// </summary>
        private static readonly string[] MUST_READ =
        {
            @"ParquetNet.dll", @"Dapper.dll", @"IdentityModel.dll", @"System.Memory.dll"
        };

        [TestMethod]
        public void TestSkylineCmdBindingPolicy()
        {
            // Test.dll is deployed to the same folder as Skyline.exe and SkylineCmd.exe
            string binDir = Path.GetDirectoryName(GetType().Assembly.Location);
            Assert.IsNotNull(binDir);
            string configPath = Path.Combine(binDir, SKYLINE_CMD_CONFIG);
            AssertEx.IsTrue(File.Exists(configPath),
                string.Format(
                    @"Expected {0} in the build output folder {1}. It is produced by the CopyConfigToSkylineCmd target in Skyline.csproj.",
                    SKYLINE_CMD_CONFIG, binDir));

            VerifyCopiedFromSkylineConfig(binDir, configPath);

            var onDisk = new Dictionary<string, Version>();
            var references = new List<AssemblyDependency>();
            ScanBuildOutput(binDir, onDisk, references);

            var dependentAssemblies = XDocument.Load(configPath)
                .Descendants(ASM_NS + @"dependentAssembly").ToArray();
            VerifyCodeBases(binDir, dependentAssemblies, onDisk);
            VerifyBindingRedirects(dependentAssemblies, onDisk, references);
        }

        /// <summary>
        /// The command-line must get the same binding policy as the UI, so the two files
        /// have to be identical.
        /// </summary>
        private void VerifyCopiedFromSkylineConfig(string binDir, string configPath)
        {
            string skylineConfig = SKYLINE_CONFIGS
                .Select(name => Path.Combine(binDir, name))
                .FirstOrDefault(File.Exists);
            AssertEx.IsTrue(skylineConfig != null,
                string.Format(@"Found none of {0} in {1}.", string.Join(@", ", SKYLINE_CONFIGS), binDir));

            AssertEx.AreEqual(File.ReadAllText(skylineConfig), File.ReadAllText(configPath),
                string.Format(
                    @"{0} is not a copy of {1}. It is supposed to be written by the CopyConfigToSkylineCmd target in Skyline.csproj, not edited by hand.",
                    SKYLINE_CMD_CONFIG, Path.GetFileName(skylineConfig)));
        }

        /// <summary>
        /// A codeBase is how an assembly whose file name differs from its simple name gets
        /// found, so the file it names must exist and must be that assembly.
        /// </summary>
        private void VerifyCodeBases(string binDir, IEnumerable<XElement> dependentAssemblies,
            IDictionary<string, Version> onDisk)
        {
            foreach (var dependentAssembly in dependentAssemblies)
            {
                var codeBase = dependentAssembly.Element(ASM_NS + @"codeBase");
                if (codeBase == null)
                {
                    continue;
                }

                string name = GetAssemblyIdentityName(dependentAssembly);
                string href = (string) codeBase.Attribute(@"href") ?? string.Empty;
                AssertEx.IsTrue(File.Exists(Path.Combine(binDir, href)),
                    string.Format(@"The codeBase for {0} points at {1}, which is not in {2}.",
                        name, href, binDir));
                AssertEx.IsTrue(onDisk.ContainsKey(name),
                    string.Format(@"The codeBase for {0} points at {1}, which is not the assembly {0}.",
                        name, href));
            }
        }

        /// <summary>
        /// Requires a bindingRedirect for every dependency some assembly in the build output
        /// wants at a version other than the one shipping. MSBuild generates these from its
        /// own view of the reference graph, so this checks the deployed result.
        /// </summary>
        private void VerifyBindingRedirects(IEnumerable<XElement> dependentAssemblies,
            IDictionary<string, Version> onDisk, IEnumerable<AssemblyDependency> references)
        {
            var mismatched = references
                .Where(r => onDisk.ContainsKey(r.Name) && !Equals(r.Version, onDisk[r.Name]))
                .ToLookup(r => r.Name);

            var redirected = new Dictionary<string, XElement>();
            foreach (var dependentAssembly in dependentAssemblies)
            {
                var redirect = dependentAssembly.Element(ASM_NS + @"bindingRedirect");
                if (redirect != null)
                {
                    redirected[GetAssemblyIdentityName(dependentAssembly)] = redirect;
                }
            }

            foreach (var dependency in mismatched)
            {
                string wantedBy = string.Join(@", ", dependency
                    .Select(r => string.Format(@"{0} wants {1}", r.Referrer, r.Version)).Distinct());
                AssertEx.IsTrue(redirected.ContainsKey(dependency.Key),
                    string.Format(
                        @"The binding policy has no bindingRedirect for {0} to {1}. Without it these loads fail at run time: {2}.",
                        dependency.Key, onDisk[dependency.Key], wantedBy));
                AssertEx.AreEqual(onDisk[dependency.Key].ToString(),
                    (string) redirected[dependency.Key].Attribute(@"newVersion"),
                    string.Format(
                        @"The bindingRedirect for {0} does not point at the version shipping in the build output.",
                        dependency.Key));
            }
        }

        private void ScanBuildOutput(string binDir, IDictionary<string, Version> onDisk,
            ICollection<AssemblyDependency> references)
        {
            var unread = new List<string>();
            foreach (string path in Directory.EnumerateFiles(binDir, @"*.dll")
                         .Concat(Directory.EnumerateFiles(binDir, @"*.exe")))
            {
                AssemblyName identity;
                AssemblyName[] referenced;
                try
                {
                    identity = AssemblyName.GetAssemblyName(path);
                    referenced = Assembly.ReflectionOnlyLoadFrom(path).GetReferencedAssemblies();
                }
                catch (Exception)
                {
                    // Native DLLs, and anything else the reflection-only loader rejects
                    unread.Add(Path.GetFileName(path));
                    continue;
                }

                onDisk[identity.Name] = identity.Version;
                string referrer = Path.GetFileName(path);
                foreach (var reference in referenced)
                {
                    references.Add(new AssemblyDependency(referrer, reference.Name, reference.Version));
                }
            }

            var missing = MUST_READ.Where(unread.Contains).ToArray();
            AssertEx.IsFalse(missing.Any(),
                string.Format(
                    @"Could not read the assembly references of {0}, so this test cannot tell which redirects are needed.",
                    string.Join(@", ", missing)));
        }

        private static string GetAssemblyIdentityName(XElement dependentAssembly)
        {
            var identity = dependentAssembly.Element(ASM_NS + @"assemblyIdentity");
            return identity == null ? string.Empty : (string) identity.Attribute(@"name");
        }

        private class AssemblyDependency
        {
            public AssemblyDependency(string referrer, string name, Version version)
            {
                Referrer = referrer;
                Name = name;
                Version = version;
            }

            public string Referrer { get; }
            public string Name { get; }
            public Version Version { get; }
        }
    }
}
