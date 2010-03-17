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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Fasta
{
    public class FastaUpdater
    {
        public FastaUpdater()
        {
            Proteins = new Dictionary<string, Protein>();
        }
        public void ReadFasta(TextReader reader)
        {
            var importer = new FastaImporter();
            foreach (var dbProtein in importer.Import(reader))
            {
                Protein protein;
                if (!Proteins.TryGetValue(dbProtein.Sequence, out protein))
                {
                    protein = new Protein(dbProtein.Sequence);
                    Proteins.Add(dbProtein.Sequence, protein);
                }
                foreach (var dbName in dbProtein.Names)
                {
                    protein.Names[dbName.Name] = dbName.Description;
                }
            }
        }
        public void Update(Workspace workspace, UpdateProgress updateProgress)
        {
            var peptides = workspace.Peptides.ListChildren();
            for (int i = 0; i < peptides.Count(); i ++)
            {
                if (!updateProgress("Peptide " + i + "/" + peptides.Count, 100 * i / peptides.Count))
                {
                    return;
                }
                var peptide = peptides[i];
                var proteins = FindProteinsWithSequence(peptide.Sequence);
                if (proteins.Count == 0)
                {
                    continue;
                }
                var names = new SortedDictionary<String, String>();
                foreach (var protein in proteins)
                {
                    foreach (var nameEntry in protein.Names)
                    {
                        names.Add(nameEntry.Key, nameEntry.Value);
                    }
                }
                var name = new StringBuilder();
                var description = new StringBuilder();
                var space = "";
                foreach (var entry in names)
                {
                    name.Append(space);
                    description.Append(space);
                    space = " ";
                    name.Append(entry.Key);
                    description.Append(entry.Value);
                }
                peptide.UpdateProtein(name.ToString(), description.ToString());
            }
        }
        public Dictionary<String, Protein> Proteins { get; private set; }
        public List<Protein> FindProteinsWithSequence(String sequence)
        {
            var regex = new Regex(Regex.Escape(sequence));
            var result = new List<Protein>();
            foreach (var protein in Proteins.Values)
            {
                if (regex.Match(protein.Sequence).Success)
                {
                    result.Add(protein);
                }
            }
            return result;
        }

        public delegate bool UpdateProgress(String status, int progress);
    }
    public class Protein
    {
        public Protein(String sequence)
        {
            Names = new Dictionary<string, string>();
            Sequence = sequence;
        }
        public Dictionary<String, String> Names { get; private set; }
        public String Sequence { get; private set; }
    }
}
