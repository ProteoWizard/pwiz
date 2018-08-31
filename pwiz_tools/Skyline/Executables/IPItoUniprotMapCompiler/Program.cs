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
using System.Text;
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
                var inputFilesPath = Path.Combine(projectPath, @"InputFiles");
                const int segmentCount = 100; // we'll split the data tables up to stay off the Large Object Heap

                // Writing the output file.
                // ReSharper disable once LocalizableElement
                var outpathCode = Path.Combine(projectPath, @"..\..\..\Shared\ProteomeDb\Fasta\IpiToUniprotMap.cs");
                // ReSharper disable once LocalizableElement
                var outpathData = Path.Combine(projectPath, @"..\..\..\Shared\ProteomeDb\Fasta\IpiToUniprotMap.zip");
                var writer = new StreamWriter(outpathCode);
                var templateReader = new StreamReader(Path.Combine(projectPath, @"IpiToUniprotMapTemplate.cs"));
                using (var zip = new ZipFile(Path.Combine(inputFilesPath, @"last-UniProtKB2IPI.zip")))
                {
                    var ms = new MemoryStream();
                    var e = zip[@"last-UniProtKB2IPI.map"];
                    e.Extract(ms);
                    ms.Position = 0; // rewind
                    var sr = new StreamReader(ms);
                    var line = sr.ReadLine();
                    var pairs = new List<KeyValuePair<int, string>>();
                    while (line != null)
                    {
                        var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if ((parts.Count() == 2) && (parts[1].StartsWith("IPI") && (parts[1].Length > 3)))
                        {
                            pairs.Add(new KeyValuePair<int, string>(int.Parse(parts[1].Substring(3)), parts[0]));
                        }
                        line = sr.ReadLine();
                    }
                    pairs.Sort((a, b) => a.Key.CompareTo(b.Key));

                    // Now write the sorted data to a resource file - it's
                    // that or generate a lot of code,  which can cause
                    // alarming spikes in our automated test memory tracking
                    using (var resfile = new ZipFile())
                    {
                        var sb = new StringBuilder();
                        foreach (var pair in pairs)
                        {
                            // ReSharper disable once LocalizableElement
                            sb.AppendFormat("{0} {1}\r\n", pair.Key, pair.Value);
                        }
                        resfile.AddEntry("MapUniprotIPI.txt", sb.ToString());
                        resfile.Save(outpathData);
                    }

                    int segmentSize = (pairs.Count / segmentCount);
                    if ((segmentSize * segmentCount) < pairs.Count)
                    {
                        segmentSize++;
                    }
                    string templateLine;
                    while ((templateLine = templateReader.ReadLine()) != null)
                    {
                        if (templateLine.Contains(@"DECLARE_IPI_COUNT"))
                        {
                            // ReSharper disable once LocalizableElement
                            writer.Write("        // private const int IPI_COUNT = {0};\n", pairs.Count);
                            // ReSharper disable once LocalizableElement
                            writer.Write("        private const int SEGMENT_COUNT = {0}; // We'll split our tables into segements to stay out of the LOH\n", segmentCount);
                            writer.Write("        private const int SEGMENT_SIZE = {0};\n", segmentSize);
                        }
                        // ReSharper disable once LocalizableElement
                        else if (templateLine.Contains(@"// ADD ALLOC."))
                        {
                            // ReSharper disable once LocalizableElement
                            writer.Write("            _ipi = new List<int[]>(SEGMENT_COUNT);\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("            _accession = new List<String[]>(SEGMENT_COUNT);\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("            for (var iseg = SEGMENT_COUNT; iseg-- > 0;)\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("            {\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("                _ipi.Add(new int[SEGMENT_SIZE]);\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("                _accession.Add(new String[SEGMENT_SIZE]);\n");
                            // ReSharper disable once LocalizableElement
                            writer.Write("            }\n");
                        }
                        else
                            writer.WriteLine(templateLine);
                    }
                    writer.Close();
                }
            }
            catch (Exception x)
            {
                Console.Error.WriteLine(@"ERROR: " + x.Message);
            }
        }
    }
}
