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
using System.Text;
using pwiz.Common.Chemistry;

namespace pwiz.Topograph.Enrichment
{

    public class ChargedPeptide
    {
        public ChargedPeptide(String sequence, int charge)
        {
            Sequence = sequence;
            int ichDot = sequence.IndexOf(".", StringComparison.Ordinal);
            if (ichDot >= 0)
            {
                Prefix = Sequence.Substring(0, ichDot);
                Sequence = Sequence.Substring(ichDot + 1);
            }
            ichDot = Sequence.IndexOf(".", StringComparison.Ordinal);
            if (ichDot >= 0)
            {
                Suffix = Sequence.Substring(ichDot + 1);
                Sequence = Sequence.Substring(0, ichDot);
            }
            Charge = charge;
        }
        public String Sequence
        { 
            get; set;
        }

        public String Prefix
        {
            get; set;
        }
        public String Suffix
        {
            get; set;
        }
        public int Charge
        {
            get; set;
        }
        public override String ToString()
        {
            StringBuilder result = new StringBuilder();
            if (Prefix != null)
            {
                result.Append(Prefix);
                result.Append(".");
            }
            result.Append(Sequence);
            if (Suffix != null)
            {
                result.Append(".");
                result.Append(Suffix);
            }
            result.Append("+");
            result.Append(Charge);
            return result.ToString();
        }
        public double GetMonoisotopicMass(AminoAcidFormulas aminoAcidFormulas)
        {
            return AminoAcidFormulas.ProtonMass + aminoAcidFormulas.GetMonoisotopicMass(Sequence) / Charge;
        }
        public MassDistribution GetMassDistribution(AminoAcidFormulas aminoAcidFormulas)
        {
            return aminoAcidFormulas.GetMassDistribution(Sequence, Charge);
        }
    }
}