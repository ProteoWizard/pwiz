/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Search
{
    public static class SearchResults
    {
        public static List<SearchResult> ReadDTASelect(Stream stream, Func<int, bool> progressMonitor)
        {
            var reader = new StreamReader(stream);
            List<SearchResult> results = new List<SearchResult>();
            // When we are reading protein rows, these are the proteins that are going to own the following peptides
            String currentProtein = null;
            String currentProteinDescription = null;
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!progressMonitor.Invoke((int) (100 * stream.Position / stream.Length)))
                {
                    return null;
                }
                String[] values = line.Split('\t');
                if (values.Length == 9)
                {
                    try
                    {
                        int sequenceCount = int.Parse(values[1]);
                    }
                    catch (FormatException)
                    {
                        //ignore
                        continue;
                    }
                    currentProtein = values[0];
                    currentProteinDescription = values[8];
                }
                else if (values.Length == 11 || values.Length == 12)
                {
                    int index;
                    String unique = null;
                    if (values.Length == 11)
                    {
                        index = 0;
                    }
                    else
                    {
                        unique = values[0];
                        index = 1;
                    }
                    String strSpectrumLocator = values[index++];
                    double XCorr;
                    try
                    {
                        XCorr = double.Parse(values[index++]);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    double DeltCN = double.Parse(values[index++]);
                    double MplusHplus = double.Parse(values[index++]);
                    double CalcMplusHplus = double.Parse(values[index++]);
                    double TotalIntensity = double.Parse(values[index++]);
                    int SpRank = int.Parse(values[index++]);
                    double SpScore = double.Parse(values[index++]);
                    double IonProportion = double.Parse(values[index++]);
                    int Redundancy = int.Parse(values[index++]);
                    String sequence = values[index++];
                    SpectrumLocator spectrumLocator = new SpectrumLocator(strSpectrumLocator);
                    bool isUnique = unique != null && unique.EndsWith("Y");
                    SearchResult result = new SearchResult(sequence)
                                              {
                                                  Charge = spectrumLocator.Charge,
                                                  Filename = spectrumLocator.Filename,
                                                  Protein = currentProtein,
                                                  ProteinDescription = currentProteinDescription,
                                                  ScanIndex = spectrumLocator.StartScan,
                                                  Unique = isUnique,
                                                  XCorr = XCorr,
                                              };
                    results.Add(result);
                }
            }
            return results;
        }
        public static List<SearchResult> ReadSQT(String filename, FileStream stream, Func<int, bool> progressMonitor)
        {
            var reader = new StreamReader(stream);
            List<SearchResult> results = new List<SearchResult>();
            String line;
            int startScan = 0;
            int endScan = 0;
            int charge = 0;
            while ((line = reader.ReadLine()) != null)
            {
                if (!progressMonitor.Invoke((int) (100 * stream.Position / stream.Length)))
                {
                    return null;
                }
                if (line.StartsWith("S"))
                {
                    var parts = line.Split('\t');
                    startScan = int.Parse(parts[1]);
                    endScan = int.Parse(parts[2]);
                    charge = int.Parse(parts[3]);
                }
                if (line.StartsWith("M") && startScan != 0)
                {
                    var parts = line.Split('\t');
                    SearchResult searchResult = new SearchResult(parts[9])
                                                    {
                                                        Charge = charge,
                                                        Filename = filename,
                                                        Protein = "Unknown",
                                                        ScanIndex = startScan,
                                                        XCorr = double.Parse(parts[5]),
                                                    };
                    results.Add(searchResult);
                    startScan = endScan = 0;
                }
            }
            return results;
        }

        public static List<SearchResult> ReadPepXml(String filename, FileStream stream, Func<int, bool> progressMonitor)
        {
            var results = new List<SearchResult>();
            var xmlReader = XmlReader.Create(stream);
            while (xmlReader.ReadToFollowing("spectrum_query"))
            {
                if (!progressMonitor.Invoke((int)(100 * stream.Position / stream.Length)))
                {
                    return null;
                }
                var startScan = int.Parse(xmlReader.GetAttribute("start_scan"));
                var charge = int.Parse(xmlReader.GetAttribute("assumed_charge"));
                if (!xmlReader.ReadToDescendant("search_hit"))
                {
                    continue;
                }
                var peptide = xmlReader.GetAttribute("peptide");
                var prevAA = xmlReader.GetAttribute("peptide_prev_aa");
                var nextAA = xmlReader.GetAttribute("peptide_next_aa");
                if (prevAA != null)
                {
                    peptide = prevAA + "." + peptide;
                }
                if (nextAA != null)
                {
                    peptide = peptide + "." + nextAA;
                }
                var searchResult = new SearchResult(peptide)
                                       {
                                           Protein = xmlReader.GetAttribute("protein"),
                                           Filename = filename,
                                           Charge = charge,
                                           ScanIndex = startScan,
                                       };
                if (xmlReader.ReadToDescendant("search_score"))
                {
                    do
                    {
                        var name = xmlReader.GetAttribute("name");
                        var value = xmlReader.GetAttribute("value");
                        if (name == "xcorr")
                        {
                            searchResult.XCorr = double.Parse(value);
                        }
                    } while (xmlReader.ReadToNextSibling("search_score"));
                }
                results.Add(searchResult);
            }
            return results;
        }

        public static List<SearchResult> ReadPercolatorOutput(Stream stream, double maxQValue, Func<int, bool> progressMonitor)
        {
            var results = new List<SearchResult>();
            var xmlReader = XmlReader.Create(stream);
            while (true)
            {
                try
                {
                    if (!xmlReader.ReadToFollowing("psm"))
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    ErrorHandler.LogException("Add Search Results", "Error reading XML file", e);
                    break;
                }
                if (!progressMonitor.Invoke((int) (100 * stream.Position / stream.Length)))
                {
                    return null;
                }
                double qValue;
                if (xmlReader.ReadToDescendant("q_value"))
                {
                    qValue = xmlReader.ReadElementContentAsDouble();
                }
                else
                {
                    continue;
                }
                if (qValue > maxQValue)
                {
                    continue;
                }
                string peptide;
                if (xmlReader.ReadToFollowing("peptide_seq"))
                {
                    peptide = xmlReader.GetAttribute("n") + "." + xmlReader.GetAttribute("seq") + "." +
                              xmlReader.GetAttribute("c");
                }
                else
                {
                    continue;
                }
                string protein = null;
                if (xmlReader.ReadToFollowing("protein_id"))
                {
                    protein = xmlReader.ReadElementContentAsString();
                }

                results.Add(new SearchResult(peptide)
                                {
                                    Protein = protein,
                                    QValue = qValue,
                                });
            }
            return results;
        }
        public static List<SearchResult> ReadListOfPeptides(Stream stream, Func<int, bool> progressMonitor)
        {
            var results = new List<SearchResult>();
            var reader = new StreamReader(stream);
            var rePeptide = new Regex(@"([A-Z-]\.)?[A-Z][A-Z][A-Z][A-Z]+(\.[A-Z-])?");
            while (true)
            {
                if (!progressMonitor.Invoke((int)(100 * stream.Position / stream.Length)))
                {
                    return null;
                }
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                var match = rePeptide.Match(line);
                if (!match.Success)
                {
                    continue;
                }
                results.Add(new SearchResult(match.ToString()));
            }
            return results;
        }

        public static List<SearchResult> ReadBlibBuild(String filename, Func<int,bool> progressMonitor)
        {
            var results = new List<SearchResult>();
            string tempFile = null;
            try
            {
                tempFile = Path.GetTempFileName();
                var blibBuild = new BlibBuild(tempFile, new[] {filename});
                var status = new ProgressStatus("");
                var progressMonitorImpl = ProgressMonitorImpl.NewProgressMonitorImpl(status, progressMonitor);
                blibBuild.BuildLibrary(LibraryBuildAction.Create, progressMonitorImpl, ref status);
                using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = tempFile }.ToString()))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText =
                        "SELECT S.peptideSeq, S.precursorCharge, S.SpecIDInFile, F.fileName FROM RefSpectra S INNER JOIN SpectrumSourceFiles F ON S.fileId = F.id";
                    var rs = command.ExecuteReader();
                    while (rs.Read())
                    {
                        string name = Path.GetFileName(rs.GetString(3));
                        int idxDot = name.IndexOf('.');
                        if (idxDot > 0)
                        {
                            name = name.Substring(0, idxDot);
                        }
                        results.Add(new SearchResult(rs.GetString(0))
                                       {
                                           Charge = rs.GetInt32(1),
                                           ScanIndex = Convert.ToInt32(rs.GetValue(2)),
                                           Filename = name, 
                                       });
                    }
                }
            }
            finally
            {
                if (tempFile != null)
                {
                    File.Delete(tempFile);
                }
            }
            return results;
        }
    }



    public class SearchResult
    {
        public SearchResult(String sequenceWithMods)
        {
            Sequence = sequenceWithMods.Replace("*", "");
            TracerCount = sequenceWithMods.Length - Sequence.Length;
        }
        public String Sequence { get; set; }
        public String Protein { get; set; }
        public String ProteinDescription { get; set; }
        public String Filename { get; set; }
        public int ScanIndex { get; set; }
        public int TracerCount { get; set; }
        public int Charge { get; set; }
        public bool Unique { get; set; }
        public double XCorr { get; set; }
        public double QValue { get; set; }
    }
}
