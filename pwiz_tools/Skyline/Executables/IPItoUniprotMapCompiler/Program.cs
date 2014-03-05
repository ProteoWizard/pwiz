/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.IO;
using Ionic.Zip;

namespace IpiToUniprotMapCompiler
{
    /// <summary>
    /// Takes a text file of mappings form IPI to Uniprot,
    ///  We used: ftp://ftp.uniprot.org/pub/databases/uniprot/current_release/knowledgebase/idmapping/last-UniProtKB2IPI.map.gz.
    /// Produces a class containing a hashtable to recover Unimod accession from IPI accession
    /// </summary>
    internal class Program
    {

        private static void Main()
        {
            try
            {
                const string projectPath = @"..\..";
                var inputFilesPath = Path.Combine(projectPath, "InputFiles");

                // Writing the output file.
                var outpath = Path.Combine(projectPath, @"..\..\..\Shared\ProteomeDb\Fasta\IpiToUniprotMap.cs");
                var writer = new StreamWriter(outpath);
                var templateReader = new StreamReader(Path.Combine(projectPath,"IpiToUniprotMapTemplate.cs"));
                using (var zip = new ZipFile(Path.Combine(inputFilesPath, "last-UniProtKB2IPI.zip")))
                {
                    var ms = new MemoryStream();
                    var e = zip["last-UniProtKB2IPI.map"];
                    e.Extract(ms);
                    ms.Position = 0; // rewind
                    var sr = new StreamReader(ms);
                    var line = sr.ReadLine();
                    var pairs = new List<KeyValuePair<int, string>>();
                    while (line != null)
                    {
                        var parts = line.Trim().Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
                        if ((parts.Count() == 2) && (parts[1].StartsWith("IPI") && (parts[1].Length > 3)))
                        {
                            pairs.Add(new KeyValuePair<int, string>(int.Parse(parts[1].Substring(3)), parts[0]));
                        }
                        line = sr.ReadLine();
                    }
                    pairs.Sort((a,b)=> a.Key.CompareTo(b.Key));
                    string templateLine;
                    while ((templateLine = templateReader.ReadLine()) != null)
                    {
                        if (templateLine.Contains("DECLARE_IPI_COUNT"))
                            writer.Write("            const int ipiCount = {0};\n", pairs.Count);
                        else if (templateLine.Contains(@"// ADD MAP."))
                        {
                            var linebreak = 1; // uses 9800 fewer newlines!
                            foreach (var pair in pairs)
                            {
                                    writer.Write("a({0},\"{1}\"); ", pair.Key, pair.Value);
                                    if (linebreak++%8 == 0)
                                        writer.Write("\n");
                            }
                            writer.Write("\n");
                        }
                        else
                            writer.WriteLine(templateLine);
                    }
                    writer.Close();
                }
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("ERROR: " + x.Message);
            }
        }
    }
}
