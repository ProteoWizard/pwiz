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
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class EnrichmentDef
    {
        private const double PROTON_MASS = 1.00727649;
        private const string ENRICHED_ELEMENT = "Tracer";
        private MassDistribution normalMasses;
        private MassDistribution enrichedMasses;

        public String TracerSymbol { get; private set; }
        public double DeltaMass { get; private set; }
        public int AtomCount { get; private set; }
        public double AtomPercentEnrichment { get; private set; }
        public double BaseApe { get; private set; }
        public double InitialApe { get; private set; }
        public double FinalApe { get; private set; }
        public bool IsotopesEluteEarlier { get; private set; }
        public bool IsotopesEluteLater { get; private set; }

        public EnrichmentDef(Workspace workspace, DbEnrichment dbEnrichment)
        {
            Workspace = workspace;
            TracerSymbol = dbEnrichment.TracerSymbol;
            DeltaMass = dbEnrichment.DeltaMass;
            AtomCount = dbEnrichment.AtomCount;
            AtomPercentEnrichment = dbEnrichment.AtomPercentEnrichment;
            BaseApe = dbEnrichment.InitialEnrichment;
            InitialApe = dbEnrichment.InitialEnrichment;
            FinalApe = dbEnrichment.FinalEnrichment;
            IsotopesEluteEarlier = dbEnrichment.IsotopesEluteEarlier;
            IsotopesEluteLater = dbEnrichment.IsotopesEluteLater;
            
            ComputeMasses();
        }

        public void Update(DbEnrichment dbEnrichment)
        {
            dbEnrichment.AtomCount = AtomCount;
            dbEnrichment.AtomPercentEnrichment = AtomPercentEnrichment;
            dbEnrichment.DeltaMass = DeltaMass;
            dbEnrichment.FinalEnrichment = FinalApe;
            dbEnrichment.InitialEnrichment = InitialApe;
            dbEnrichment.IsotopesEluteEarlier = IsotopesEluteEarlier;
            dbEnrichment.IsotopesEluteLater = IsotopesEluteLater;
            dbEnrichment.TracerSymbol = TracerSymbol;
        }

        public Workspace Workspace { get; private set; }

        private AminoAcidFormulas GetUnenrichedResidueComposition()
        {
            var res = Workspace.GetResidueComposition();
            char aminoAcid;
            if (AminoAcidFormulas.LongNames.TryGetValue(TracerSymbol, out aminoAcid))
            {
                res = res.SetIsotopeAbundances(res.IsotopeAbundances.SetAbundances(TracerSymbol, normalMasses));
                res = res.SetFormula(aminoAcid, TracerSymbol);
            }
            res = res.SetIsotopeAbundances(res.IsotopeAbundances.SetAbundances(ENRICHED_ELEMENT, enrichedMasses));
            return res;
        }

        private void ComputeMasses()
        {
            var res = Workspace.GetResidueComposition();
            char aminoAcid;
            if (!AminoAcidFormulas.LongNames.TryGetValue(TracerSymbol, out aminoAcid))
            {
                normalMasses = res.GetMassDistribution(Molecule.Parse(TracerSymbol), 0);
                double enrichedMass = normalMasses.Keys.Min() + DeltaMass;
                enrichedMasses = MassDistribution.NewInstance(new Dictionary<double, double> {{enrichedMass, 1.0}}, 0, 0);
                return;
            }
            var enrichMasses = new Dictionary<double, double>();
            enrichMasses.Add(0, 1 - AtomPercentEnrichment / 100);
            enrichMasses.Add(DeltaMass / AtomCount, AtomPercentEnrichment / 100);
            var isotopeAbundances = res.IsotopeAbundances.SetAbundances("Enrich",
                                                                        MassDistribution.NewInstance(
                                                                            new Dictionary<double, double>
                                                                                {
                                                                                    {0, 1 - AtomPercentEnrichment/100},
                                                                                    {
                                                                                        DeltaMass/AtomCount,
                                                                                        AtomPercentEnrichment/100
                                                                                        }
                                                                                }, 0, 0));
            res = res.SetIsotopeAbundances(isotopeAbundances);
            var formula = Molecule.Parse(res.Formulas[aminoAcid]);
            var enrichFormula = Molecule.Parse(formula + "Enrich" + AtomCount);
            normalMasses = res.GetMassDistribution(formula, 0);
            enrichedMasses = res.GetMassDistribution(enrichFormula, 0);
        }

        public AminoAcidFormulas GetResidueComposition(double ape)
        {
            var res = GetUnenrichedResidueComposition();
            return res.SetIsotopeAbundances(res.IsotopeAbundances.SetAbundances(TracerSymbol, GetMassAbundances(ape)));
        }
        private MassDistribution GetMassAbundances(double ape)
        {
            return MassDistribution.NewInstance(Dictionaries.Sum(
                Dictionaries.Scale(normalMasses, 1 - ape / 100),
                Dictionaries.Scale(enrichedMasses, ape / 100)
                ), 0, 0);
        }
        public override String ToString()
        {
            return TracerSymbol + (DeltaMass > 0 ? "+" : "") + DeltaMass;
        }

        public List<double> GetMzs(ChargedPeptide chargedPeptide)
        {
            var res = GetUnenrichedResidueComposition();
            double pepBaseMass = chargedPeptide.GetMonoisotopicMass(res);
            List<double> result = new List<double>();
            int symbolCount = GetMaximumTracerCount(chargedPeptide);
            for (int i = 0; i <= symbolCount; i++)
            {
                double mass = pepBaseMass + DeltaMass * i / chargedPeptide.Charge;
                result.Add(mass);
                int extraCount = (int) Math.Round(DeltaMass);
                if (i == symbolCount)
                {
                    extraCount = Math.Max(extraCount, 6);
                }
                // Also add masses which are 1 higher for 13C.
                for (double extraMass = 1; extraMass < extraCount; extraMass++)
                {
                    result.Add(mass + extraMass / chargedPeptide.Charge);
                }
            }
            return result;
        }

        public int GetMassCount(ChargedPeptide chargedPeptide)
        {
            var deltaMassInt = (int) Math.Round(DeltaMass);
            return GetMaximumTracerCount(chargedPeptide) * deltaMassInt + Math.Max(6, deltaMassInt);
        }

        public int GetMaximumTracerCount(ChargedPeptide chargedPeptide)
        {
            var res = GetUnenrichedResidueComposition();
            var formula = res.GetFormula(chargedPeptide.Sequence);
            int symbolCount;
            formula.TryGetValue(TracerSymbol, out symbolCount);
            return symbolCount;
        }
        public MassDistribution GetSpectrum(double ape, ChargedPeptide chargedPeptide)
        {
            return chargedPeptide.GetMassDistribution(GetResidueComposition(ape));
        }
        public MassDistribution GetEnrichedSpectrum(ChargedPeptide chargedPeptide, int enrichmentCount)
        {
            var res = GetUnenrichedResidueComposition();
            var formula = res.GetFormula(chargedPeptide.Sequence);
            int symbolCount;
            formula.TryGetValue(TracerSymbol, out symbolCount);
            if (symbolCount < enrichmentCount)
            {
                return null;
            }
            if (enrichmentCount != 0)
            {
                formula = formula.SetAtomCount(TracerSymbol, symbolCount - enrichmentCount);
                formula = formula.SetAtomCount(ENRICHED_ELEMENT, enrichmentCount);
            }

            var result = res.GetMassDistribution(formula, chargedPeptide.Charge);
            return result.OffsetAndDivide(res.GetMassShift(chargedPeptide.Sequence), 1);
        }

        public static DbEnrichment GetN15Enrichment()
        {
            return new DbEnrichment
                       {
                           AtomCount = 1,
                           AtomPercentEnrichment = 100,
                           DeltaMass = 0.9970356,
                           TracerSymbol = "N",
                           IsotopesEluteEarlier = false,
                           IsotopesEluteLater = false,
                       };
        }
        public static DbEnrichment GetD3LeuEnrichment()
        {
            return new DbEnrichment
                       {
                           AtomCount = 3,
                           AtomPercentEnrichment = 98,
                           DeltaMass = 3.018,
                           TracerSymbol = "Leu",
                           IsotopesEluteEarlier = true,
                           IsotopesEluteLater = false,
                       };
        }
    }
}
