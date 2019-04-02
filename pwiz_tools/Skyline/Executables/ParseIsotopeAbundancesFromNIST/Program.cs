/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

// Takes the output from https://physics.nist.gov/cgi-bin/Compositions/stand_alone.pl?ele=&all=all&ascii=ascii2&isotype=some (format as of 3/25/2019)
// and generates a code snippet usable in \pwiz_tools\Shared\Common\Chemistry\IsotopeAbundances.cs to construct
//    var defaults = new Dictionary<string, double[]> {...}
// That is, it makes a replacement for the ...

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ParseIsotopeAbundancesFromNIST
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ParseIsotopeAbundancesFromNIST <input_file. <output_file>");
                Console.WriteLine(
                    "Assumes input file contains text produced by https://physics.nist.gov/cgi-bin/Compositions/stand_alone.pl?ele=&all=all&ascii=ascii2&isotype=all ");
                Console.WriteLine("There should be an example checked in to the Inputs folder of this program's source code directory.");
                Console.WriteLine("Output file will contain a code snippet suitable for updating /pwiz_tools/Shared/Common/Chemistry/IsotopeAbundances.cs");
                Environment.Exit(1);
            }

            // Parse the report of entries like:
            //
            // Atomic Number = 1
            // Atomic Symbol = H
            // Mass Number = 1
            // Relative Atomic Mass = 1.00782503223(9)
            // Isotopic Composition = 0.999885(70)
            // Standard Atomic Weight = [1.00784, 1.00811]
            // Notes = m
            // 
            //
            // in order to build strings like:
            // {"H",new []{1.007825035,0.999855,2.014101779,0.000145,}},

            // Notes:
            // Not all entries report Isoptopic Composition, and not all report a Standard Atomic Weight
            // For most elements, at least one Isotopic Composition is reported, for those elements we discard any entries not reporting Isotopic Composition as being uncommon.
            // For elements which report no Isotopic Composition, use the reported Standard Atomic Weight and claim isotopic composition 1.0 per Skyline custom  
            // For elements which report neither Isotopic Composition nor Standard Atomic Weight, use the middle reported mass value and claim isotopic composition 1.0 per Skyline custom  


            var comparer = new CompositionComparer();
            var elementNumbers = new Dictionary<string, SortedList<string, string>>();
            var elementNames = new Dictionary<string, string>();
            var standardAtomicWeights = new Dictionary<string, string>();
            var text = File.ReadAllLines(args[0]).ToList();
            var indices = Enumerable.Range(0,text.Count).Where(t => text[t].StartsWith(@"Atomic Number =")).ToArray();

            foreach (var index in indices)
            {
                var elementNumber = text[index].Split('=')[1].Trim();
                if (!elementNames.ContainsKey(elementNumber)) // May find more than one name per number, as in H, D, T
                {
                    elementNames.Add(elementNumber, text[index+1].Split('=')[1].Trim());
                }                    
                var mass = text[index + 3].Split('=')[1].Trim().Split('(')[0];
                var composition = text[index + 4].Split('=')[1].Trim().Split('(')[0];
                var standardAtomicWeight = text[index + 5].Split('=')[1].Trim();
                if (standardAtomicWeight.StartsWith(@"["))
                {
                    standardAtomicWeight = standardAtomicWeight.Substring(1).Replace(@"]",@".0").Trim();
                    standardAtomicWeights[elementNumber] = standardAtomicWeight;
                }
                if (!elementNumbers.TryGetValue(elementNumber, out _))
                {
                    elementNumbers.Add(elementNumber, new SortedList<string, string>(comparer));
                }
                elementNumbers[elementNumber].Add(mass, composition);
            }

            var output = new List<string>();
            output.Add("                // " + text[0]); // Hopefuly it says something about source and date, like "These values obtained 3/25/2019 from https://physics.nist.gov/cgi-bin/Compositions/stand_alone.pl?ele=&all=all&ascii=ascii2&isotype=some"

            foreach (var elementNumber in elementNumbers)
            {
                var line = "                {\"" + elementNames[elementNumber.Key] + "\", new[]{";
                int nIsotopes = 0;
                foreach (var pair in elementNumber.Value)
                {
                    var composition = pair.Value;
                    if (!string.IsNullOrEmpty(composition) && double.Parse(composition, CultureInfo.InvariantCulture) > 0)
                    {
                        if (Equals("1", composition))
                        {
                            composition = "1.0";
                        }

                        line += pair.Key + "," + composition + ",";
                        nIsotopes++;
                    }
                }

                if (nIsotopes == 0)
                {
                    // Nothing declared as stable, take the declared standard atomic weightm or failing that use the middle value in the list
                    var mass = standardAtomicWeights.TryGetValue(elementNumber.Key, out var stdWeight) ? stdWeight :  elementNumber.Value.Keys.ToArray()[elementNumber.Value.Count / 2];
                    line += mass + "," + "1.0" + ",";
                }
                line += "}},";
                Console.WriteLine(line);
                output.Add(line);
            }
            File.WriteAllLines(args[1], output);
            Console.WriteLine("code snippet written to " + args[1]);
        }

        private class CompositionComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                // ReSharper disable AssignNullToNotNullAttribute
                return Double.Parse(x).CompareTo(Double.Parse(y));
                // ReSharper restore AssignNullToNotNullAttribute
            }
        }
    }
}
