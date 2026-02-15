/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Text;

namespace BindingRedirectGenerator
{
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: BindingRedirectGenerator.exe <path-to-created-app.config> <path-to-dll1> <path-to-dll2> ...");
                Console.WriteLine("Creates the .config file with bindings redirects (if needed) based on the passed assemblies.");
                Console.WriteLine();
                Console.WriteLine("Usage: BindingRedirectGenerator.exe --test <path-to-dll1> <path-to-dll2> ...");
                Console.WriteLine("Returns 0 if no redirects are necessary, else returns the number of redirects.");
                return 1;
            }

            var loadedAssemblies = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);
            var referencedVersions = new Dictionary<string, List<AssemblyName>>(StringComparer.OrdinalIgnoreCase);

            foreach (var dllPath in args.Skip(1))
            {
                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"File not found: {dllPath}");
                    continue;
                }

                try
                {
                    var assembly = Assembly.ReflectionOnlyLoadFrom(dllPath);
                    var name = assembly.GetName();
                    loadedAssemblies[name.Name] = name;

                    foreach (var reference in assembly.GetReferencedAssemblies())
                    {
                        if (!referencedVersions.ContainsKey(reference.Name))
                            referencedVersions[reference.Name] = new List<AssemblyName>();

                        referencedVersions[reference.Name].Add(reference);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load {dllPath}: {ex.Message}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<configuration>");
            sb.AppendLine("  <runtime>");
            sb.AppendLine("    <assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\">");

            int redirects = 0;
            foreach (var kvp in referencedVersions)
            {
                if (loadedAssemblies.TryGetValue(kvp.Key, out var actualAssembly))
                {
                    var referencedVersionsList = kvp.Value
                        .Where(r => r.Version < actualAssembly.Version)
                        .Select(r => r.Version)
                        .Distinct()
                        .OrderBy(v => v)
                        .ToList();

                    if (referencedVersionsList.Count > 0)
                    {
                        ++redirects;
                        var publicKeyToken = BitConverter.ToString(actualAssembly.GetPublicKeyToken()).Replace("-", string.Empty).ToLowerInvariant();
                        sb.AppendLine("      <dependentAssembly>");
                        sb.AppendLine($"        <assemblyIdentity name=\"{actualAssembly.Name}\" publicKeyToken=\"{publicKeyToken}\" culture=\"neutral\" />");
                        sb.AppendLine($"        <bindingRedirect oldVersion=\"0.0.0.0-{actualAssembly.Version}\" newVersion=\"{actualAssembly.Version}\" />");
                        sb.AppendLine("      </dependentAssembly>");
                    }
                }
            }

            sb.AppendLine("    </assemblyBinding>");
            sb.AppendLine("  </runtime>");
            sb.AppendLine("</configuration>");

            if (args[0] == "--test")
                return redirects;

            if (redirects == 0)
            {
                Console.WriteLine($"No binding redirects needed for {Path.GetFileName(args[0])}.");
            }
            else
            {
                var exeConfigDir = Path.GetDirectoryName(args[0]);
                if (!string.IsNullOrWhiteSpace(exeConfigDir))
                    Directory.CreateDirectory(exeConfigDir);
                File.WriteAllText(args[0], sb.ToString());
                Console.WriteLine($"Generated {Path.GetFileName(args[0])} with {redirects} binding redirects.");
            }

            return 0;
        }
    }
}