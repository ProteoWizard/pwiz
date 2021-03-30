/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SortRESX
{
    /// <summary>
    /// This code was copied from:
    /// https://www.codeproject.com/Articles/37022/Solving-the-resx-Merge-Problem
    ///
    /// It was subsequently modified to use FileSaver and modify .resx files in place
    /// </summary>
    //
    // 0 command line parameters ==> input is from stdin and output is stdout.
    // 1 command line parameter  ==> input is a source .resx file (arg[0]) and output is the same file.
    // The program reads the source and writes a sorted version of it to the output.
    //
    class Program
    {
        static void Main(string[] args)
        {
            XmlReader inputStream = null;
            FileSaver fileSaver = null;
            if (args.Length > 1)
            {
                ShowHelp();
                return;
            }

            if (args.Length == 0) // Input resx is coming from stdin
            {
                try 
                {
                    Stream s = Console.OpenStandardInput();
                    inputStream = XmlReader.Create(s);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine("Error reading from stdin: {0}", ex.Message);
                    return;
                }
            }
            else // Input resx is from file specified by first argument 
            {
                string arg0 = args[0].ToLower();
                if( arg0.StartsWith(@"/h") || arg0.StartsWith(@"/?"))
                {
                    ShowHelp();
                    return;
                }

                string path = Path.GetFullPath(args[0]);
                try
                {
                    inputStream = XmlReader.Create(path);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error opening file '{0}': {1}", path, ex.Message);
                    return;
                }
                fileSaver = new FileSaver(path);
            }
            try
            {
                XDocument sortedDoc = null;
                using (inputStream)
                {
                    XDocument doc = XDocument.Load(inputStream);
                    // Create a sorted version of the XML
                    sortedDoc = SortDataByName(doc);
                    // Save it to the target
                    Console.OutputEncoding = Encoding.UTF8;
                }
                // Create a linq XML document from the source.
                if (fileSaver != null)
                {
                    using (fileSaver)
                    {
                        sortedDoc.Save(fileSaver.SafeName);
                        fileSaver.Commit();
                    }
                }
                else
                {
                    sortedDoc.Save(Console.Out);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            return;
        }

        //
        // Use Linq to sort the elements.  The comment, schema, resheader, assembly, metadata, data appear in that order, 
        // with resheader, assembly, metadata and data elements sorted by name attribute.
        private static XDocument SortDataByName(XDocument resx)
        {
            return new XDocument(
                new XElement(resx.Root.Name,
                    from comment in resx.Root.Nodes() where comment.NodeType == XmlNodeType.Comment select comment,
                    from schema in resx.Root.Elements() where schema.Name.LocalName == "schema" select schema,
                    from resheader in resx.Root.Elements("resheader") orderby (string) resheader.Attribute("name") select resheader,
                    from assembly in resx.Root.Elements("assembly") orderby (string) assembly.Attribute("name") select assembly,
                    from metadata in resx.Root.Elements("metadata") orderby (string)metadata.Attribute("name") select metadata,
                    from data in resx.Root.Elements("data") orderby (string)data.Attribute("name") select data
                )
            );
        }

        //
        // Write invocation instructions to stderr.
        //
        private static void ShowHelp()
        {
            Console.Error.WriteLine(
                "0 arguments ==> Input from STDIN.  Output to STDOUT.\n" +
                "1 argument  ==> Input from specified .resx file.  Output to specified .resx file.\n");
        }
    }
}
