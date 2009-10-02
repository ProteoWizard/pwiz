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
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class IsotopeAbundances
    {
        readonly Dictionary<String, Dictionary<double, double>> abundances 
            = new Dictionary<string, Dictionary<double, double>>();

        public static readonly Dictionary<String,Dictionary<double, double>> NATURAL_ABUNDANCES 
            = parseLines(new StreamReader(typeof(IsotopeAbundances).Assembly.GetManifestResourceStream("pwiz.Topograph.Enrichment.ISOTOPE.DAT")));
        public IsotopeAbundances(Dictionary<String, Dictionary<double, double>> initialAbundances)
        {
            foreach (var entry in initialAbundances)
            {
                abundances[entry.Key] = new Dictionary<double, double>(entry.Value);
            }
        }
        public IsotopeAbundances() : this(NATURAL_ABUNDANCES)
        {
        }
        static Dictionary<String, Dictionary<double, double>> parseLines(StreamReader reader)
        {
            Dictionary<String,Dictionary<double, double>> result 
                = new Dictionary<string, Dictionary<double, double>>();
            String element = null;
            Dictionary<double, double> isotopes = null;
            int isotopeCount = 0;
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                String[] parts = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                if (isotopeCount > 0)
                {
                    isotopes.Add(double.Parse(parts[0]), double.Parse(parts[1]));
                    if (--isotopeCount == 0)
                    {
                        result.Add(element, isotopes);
                        isotopes = null;
                        element = null;
                    }
                } 
                else
                {
                    element = parts[0];
                    isotopeCount = int.Parse(parts[1]);
                    isotopes = new Dictionary<double, double>();
                }
            }
            return result;
        }
        public Dictionary<String, Dictionary<double, double>> getAbundances()
        {
            return abundances;
        }
        public void addAbundances(String element, Dictionary<double, double> abundances)
        {
            this.abundances.Add(element, new Dictionary<double, double>(abundances));
        }
        public void addAllAbundances(Dictionary<String, Dictionary<double, double>> abundances)
        {
            foreach(KeyValuePair<String, Dictionary<double, double>> keyValuePair in abundances.AsEnumerable())
            {
                this.abundances.Add(keyValuePair.Key, keyValuePair.Value);
            }
        }

        public void Enrich(String symbol, double molWt, double enrichment)
        {
            Dictionary<double, double> isotopeAbundances = abundances[symbol];
            double totalAbundance = isotopeAbundances.Values.Sum();
            double currentAbundance;
            if (isotopeAbundances.ContainsKey(molWt))
            {
                currentAbundance = isotopeAbundances[molWt];   
            }
            else
            {
                currentAbundance = 0;
            }
            if (enrichment >= totalAbundance - currentAbundance)
            {
                abundances.Remove(symbol);
                abundances.Add(symbol, new Dictionary<double, double> {{molWt, 1}});
                return;
            }
            double newAbundance = currentAbundance +
                                  enrichment*totalAbundance*totalAbundance/
                                  (totalAbundance - currentAbundance - enrichment);
            isotopeAbundances[molWt] = newAbundance;
            abundances.Remove(symbol);
            abundances.Add(symbol, Dictionaries.Normalize(isotopeAbundances, 1));
        }
    }
}