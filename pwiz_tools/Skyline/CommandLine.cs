/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    class CommandLine : IDisposable
    {
        private readonly TextWriter _out;

        public CommandLine(TextWriter output)
        {
            _out = output;
        }

        public void Run(string[] args)
        {
            string skyFile = "";
            string dataFile = "";
            string replicate = "";
            string outFile = "";

            bool saving = false;

            SrmDocument doc = null;

            foreach(string s in args)
            {
                if(s.StartsWith("--in="))
                {
                    skyFile = s.Split('=')[1];
                }

                if(s.StartsWith("--import="))
                {
                    dataFile = s.Split('=')[1];
                }

                if(s.StartsWith("--replicate="))
                {
                    replicate = s.Split('=')[1];
                }

                if(s.StartsWith("--out="))
                {
                    outFile = s.Split('=')[1];
                }

                if(s.StartsWith("--save"))
                {
                    saving = true;
                }
            }

            if (!String.IsNullOrEmpty(skyFile))
            {
                using (var stream = new FileStream(skyFile, FileMode.Open))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    try
                    {
                        doc = (SrmDocument)xmlSerializer.Deserialize(stream);
                    }
                    catch (Exception)
                    {
                        _out.WriteLine("Error opening file: " + skyFile);
                        return;
                    }
                }
            }

            if(!String.IsNullOrEmpty(dataFile))
            {
                if(doc == null)
                {
                    _out.WriteLine("If you provide a replicate file, you must specify an input file");
                    _out.WriteLine("with --in=[file]");
                    return;
                }

                if (String.IsNullOrEmpty(replicate))
                    replicate = Path.GetFileNameWithoutExtension(dataFile);

                if(String.IsNullOrEmpty(outFile) && !saving)
                {
                    _out.WriteLine("If you provide a replicate file, you must use");
                    _out.Write("either the --out=[file] flag or the --save flag.");

                    return;
                }

                SrmDocument newDoc = AttachReplicate(doc, skyFile, replicate, dataFile);

                if(ReferenceEquals(doc, newDoc))
                {
                    _out.WriteLine("There was an error adding the replicate.");
                    return;
                }

                doc = newDoc;

                //the replicate was added successfully
            }

            if (saving && !String.IsNullOrEmpty(outFile))
            {
                _out.WriteLine("Error: please use either --save or --out=[file] but not both.");
                return;
            }

            if(saving)
            {
                outFile = skyFile;
            }

            if(!String.IsNullOrEmpty(outFile))
            {
                WriteFiles(doc, outFile);
            }
        }


        public static void WriteFiles(SrmDocument doc, string outFile)
        {
            using (var writer = new XmlTextWriter(outFile, Encoding.UTF8) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, doc);

                writer.Flush();
                writer.Close();

                var settings = doc.Settings;
                if (settings.HasResults)
                {
                    if (settings.MeasuredResults.IsLoaded)
                    {
                        FileStreamManager fsm = FileStreamManager.Default;
                        settings.MeasuredResults.OptimizeCache(outFile, fsm);

                        //don't worry about updating the document with the results of optimization
                        //as is done in SkylineFiles
                    }
                }
                else
                {
                    string cachePath = ChromatogramCache.FinalPathForName(outFile, null);
                    if (File.Exists(cachePath))
                        File.Delete(cachePath);
                }
            }
        }


        public static SrmDocument AttachReplicate(SrmDocument doc, string docPath, string replicate, string dataFile)
        {
            var docContainer = new ResultsMemoryDocumentContainer(doc, docPath);

            var listChromatograms = new List<ChromatogramSet>();

            if (doc.Settings.HasResults)
                listChromatograms.AddRange(doc.Settings.MeasuredResults.Chromatograms);
            
            listChromatograms.Add(new ChromatogramSet(replicate, new[] {dataFile}));
            
            var results = doc.Settings.HasResults
                ? doc.Settings.MeasuredResults.ChangeChromatograms(listChromatograms)
                : new MeasuredResults(listChromatograms);
            
            var docAdded = doc.ChangeMeasuredResults(results);

            docContainer.SetDocument(docAdded, doc, true);

            return docContainer.Document;
        }

        public void Dispose()
        {
            _out.Close();
        }
    }
}
