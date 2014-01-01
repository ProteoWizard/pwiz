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
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Enrichment
{
    public class TracerDef
    {
        public String Name { get; private set; }
        public String TraceeSymbol { get; private set; }
        public double DeltaMass { get; private set; }
        public int AtomCount { get; private set; }
        public double AtomPercentEnrichment { get; private set; }
        public double InitialApe { get; private set; }
        public double FinalApe { get; private set; }
        public bool IsotopesEluteEarlier { get; private set; }
        public bool IsotopesEluteLater { get; private set; }
        public char? AminoAcidSymbol { get; private set;}
        public MassDistribution TracerMasses { get; private set; }
        public MassDistribution TraceeMasses { get; private set; }

        public bool TracerIsAminoAcid 
        {
            get
            {
                return AminoAcidSymbol.HasValue;
            }
        }
        public bool TracerIsElement 
        { 
            get
            {
                return !AminoAcidSymbol.HasValue;
            }
        }

        public TracerDef(Workspace workspace, TracerDefData tracerDefData)
        {
            Workspace = workspace;
            Name = tracerDefData.Name;
            TraceeSymbol = tracerDefData.TracerSymbol;
            char aminoAcidSymbol;
            if (AminoAcidFormulas.LongNames.TryGetValue(TraceeSymbol, out aminoAcidSymbol))
            {
                AminoAcidSymbol = aminoAcidSymbol;
            }
            DeltaMass = tracerDefData.DeltaMass;
            AtomCount = tracerDefData.AtomCount;
            AtomPercentEnrichment = tracerDefData.AtomPercentEnrichment;
            InitialApe = tracerDefData.InitialEnrichment;
            FinalApe = tracerDefData.FinalEnrichment;
            IsotopesEluteEarlier = tracerDefData.IsotopesEluteEarlier;
            IsotopesEluteLater = tracerDefData.IsotopesEluteLater;
            
            ComputeMasses();
        }

        public Workspace Workspace { get; private set; }

        public AminoAcidFormulas AddTracerToAminoAcidFormulas(AminoAcidFormulas aminoAcidFormulas)
        {
            if (AminoAcidSymbol.HasValue)
            {
                aminoAcidFormulas = aminoAcidFormulas.SetIsotopeAbundances(
                    aminoAcidFormulas.IsotopeAbundances.SetAbundances(TraceeSymbol, TraceeMasses));
                aminoAcidFormulas = aminoAcidFormulas.SetFormula(AminoAcidSymbol.Value, TraceeSymbol);
            }
            aminoAcidFormulas = aminoAcidFormulas.SetIsotopeAbundances(
                aminoAcidFormulas.IsotopeAbundances.SetAbundances(Name, TracerMasses));
            return aminoAcidFormulas;
        }

        /// <summary>
        /// Computes the isotope distribution of the tracer
        /// </summary>
        private void ComputeMasses()
        {
            var res = Workspace.GetAminoAcidFormulas();
            if (!AminoAcidSymbol.HasValue)
            {
                // If the tracer is a single element, then its mass is just the normal mass
                // of the element plus the mass shift.
                TraceeMasses = res.GetMassDistribution(Molecule.Parse(TraceeSymbol), 0);
                double tracerMass = TraceeMasses.MostAbundanceMass + DeltaMass;
                TracerMasses = MassDistribution.NewInstance(new Dictionary<double, double> {{tracerMass, 1.0}}, 0, 0);
                return;
            }
            // If the tracer is an amino acid, get the masses of the amino acid, and
            // add in atoms with the correct mass shift.
            var heavyMasses = new Dictionary<double, double>();
            heavyMasses.Add(0, 1 - AtomPercentEnrichment / 100);
            heavyMasses.Add(DeltaMass / AtomCount, AtomPercentEnrichment / 100);
            const string tempName = "Temp";
            var isotopeAbundances = res.IsotopeAbundances.SetAbundances(
                tempName, MassDistribution.NewInstance(heavyMasses, 0, 0));
            res = res.SetIsotopeAbundances(isotopeAbundances);
            var formula = Molecule.Parse(res.Formulas[AminoAcidSymbol.Value]);
            var tracerFormula = Molecule.Parse(formula + tempName + AtomCount);
            TraceeMasses = res.GetMassDistribution(formula, 0);
            TracerMasses = res.GetMassDistribution(tracerFormula, 0);
        }

        public override String ToString()
        {
            return Name;
        }

        public int GetMaximumTracerCount(ChargedPeptide chargedPeptide)
        {
            return GetMaximumTracerCount(chargedPeptide.Sequence);
        }
        public int GetMaximumTracerCount(String peptideSequence)
        {
            var res = AddTracerToAminoAcidFormulas(Workspace.GetAminoAcidFormulas());
            var formula = res.GetFormula(peptideSequence);
            return formula.GetElementCount(TraceeSymbol);
        }
        public static DbTracerDef GetN15Enrichment()
        {
            return new DbTracerDef
                       {
                           AtomCount = 1,
                           AtomPercentEnrichment = 100,
                           DeltaMass = 0.9970356,
                           TracerSymbol = "N",
                           IsotopesEluteEarlier = false,
                           IsotopesEluteLater = false,
                       };
        }
        public static DbTracerDef GetD3LeuEnrichment()
        {
            return new DbTracerDef
                       {
                           AtomCount = 3,
                           AtomPercentEnrichment = 99.5,
                           DeltaMass = 3.0188325,
                           TracerSymbol = "Leu",
                           IsotopesEluteEarlier = true,
                           IsotopesEluteLater = false,
                       };
        }
    }
}
