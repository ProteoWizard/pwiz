/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.ProteomeDatabase.DataModel;

namespace pwiz.ProteomeDatabase.Fasta
{
    public class FastaImporter
    {
        private DbProtein _curProtein;
        private StringBuilder _curSequence;

        public IEnumerable<DbProtein> Import(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(">"))
                {
                    DbProtein protein = EndProtein();
                    if (protein != null)
                    {
                        yield return protein;
                    }
                    _curProtein = ParseProteinLine(line);
                    _curSequence = new StringBuilder();
                }
                else if (_curSequence == null)
                {
                    // break;
                }
                else
                {
                    _curSequence.Append(ParseSequenceLine(line));
                }
            }
            DbProtein lastProtein = EndProtein();
            if (lastProtein != null)
            {
                yield return lastProtein;
            }
        }
        private DbProtein EndProtein()
        {
            if (_curProtein == null)
            {
                return null;
            }
            _curProtein.Sequence = _curSequence.ToString();
            _curSequence = new StringBuilder();
            DbProtein result = _curProtein;
            _curProtein = null;
            return result;
        }

        private static DbProtein ParseProteinLine(String line)
        {
            String[] alternatives = line.Substring(1).Split((char) 1);
            String name, description;
            ParseNameDescription(alternatives[0], out name, out description);
            DbProtein protein = new DbProtein();
            protein.Names.Add(new DbProteinName {Name = name, Description = description, IsPrimary = true});
            for (int i = 1; i < alternatives.Length; i++)
            {
                ParseNameDescription(alternatives[i], out name, out description);
                DbProteinName altName = new DbProteinName
                                            {
                                                Protein = protein,
                                                Name = name,
                                                Description = description,
                                            };
                protein.Names.Add(altName);
            }
            return protein;
        }

        private static void ParseNameDescription(String line, out String name, out String description)
        {
            int ichSeparator = line.IndexOfAny(new[] {' ', '\t'});
            if (ichSeparator < 0)
            {
                name = line;
                description = null;
            }
            else
            {
                name = line.Substring(0, ichSeparator);
                description = line.Substring(ichSeparator + 1);
            }
        }

        private static String ParseSequenceLine(String line)
        {
            line = line.Replace(" ", "").Trim();
            if (line.EndsWith("*"))
            {
                line = line.Substring(0, line.Length - 1);
            }
            return line;
        }
    }
}
