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
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class EnrichmentDef
    {
        private const double PROTON_MASS = 1.00727649;
        private const string ENRICHED_ELEMENT = "Tracer";
        private Dictionary<double, double> normalMasses;
        private Dictionary<double, double> enrichedMasses;

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

        private ResidueComposition GetUnenrichedResidueComposition()
        {
            var res = Workspace.GetResidueComposition();
            char aminoAcid;
            if (ResidueComposition.LongNames.TryGetValue(TracerSymbol, out aminoAcid))
            {
                res.IsotopeAbundances.addAbundances(TracerSymbol, normalMasses);
                res.ResidueFormulas["" + aminoAcid] = TracerSymbol;
            }
            res.IsotopeAbundances.addAbundances(ENRICHED_ELEMENT, enrichedMasses);
            return res;
        }

        private void ComputeMasses()
        {
            var res = Workspace.GetResidueComposition();
            char aminoAcid;
            if (!ResidueComposition.LongNames.TryGetValue(TracerSymbol, out aminoAcid))
            {
                normalMasses = GetZeroChargeMasses(res, TracerSymbol);
                double enrichedMass = normalMasses.Keys.Min() + DeltaMass;
                enrichedMasses = new Dictionary<double, double> { { enrichedMass, 1 } };
                return;
            }
            Dictionary<double, double> enrichMasses = new Dictionary<double, double>();
            enrichMasses.Add(0, 1 - AtomPercentEnrichment / 100);
            enrichMasses.Add(DeltaMass / AtomCount, AtomPercentEnrichment / 100);
            res.IsotopeAbundances.addAbundances("Enrich", new Dictionary<double, double>
                                                              {
                                                                  {0, 1-AtomPercentEnrichment/100},
                                                                  {DeltaMass/AtomCount,AtomPercentEnrichment/100}
                                                              });
            String formula = res.MolecularFormula(aminoAcid);
            String enrichFormula = formula + "Enrich" + AtomCount;

            normalMasses = GetZeroChargeMasses(res, formula);
            enrichedMasses = GetZeroChargeMasses(res, enrichFormula);
        }

        private static Dictionary<double, double> GetZeroChargeMasses(ResidueComposition residueComposition, String formula)
        {
            Dictionary<double, double> massesPlusOne = residueComposition.GetIsotopeMasses(formula, 1);
            Dictionary<double, double> result = new Dictionary<double, double>();
            foreach (var entry in massesPlusOne)
            {
                result.Add(entry.Key - PROTON_MASS, entry.Value);
            }
            return result;
        }

        public ResidueComposition GetResidueComposition(double ape)
        {
            var res = GetUnenrichedResidueComposition();
            res.IsotopeAbundances.getAbundances()[TracerSymbol] = GetMassAbundances(ape);
            return res;
        }
        private Dictionary<double, double> GetMassAbundances(double ape)
        {
            return Dictionaries.Sum(
                Dictionaries.Scale(normalMasses, 1 - ape / 100),
                Dictionaries.Scale(enrichedMasses, ape / 100)
                );
        }
        public override String ToString()
        {
            return TracerSymbol + (DeltaMass > 0 ? "+" : "") + DeltaMass;
        }

        public List<double> GetMzs(ChargedPeptide chargedPeptide)
        {
            var res = GetUnenrichedResidueComposition();
            double pepBaseMass = res.GetMonoisotopicMz(chargedPeptide);
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
            Dictionary<String, int> formula = res.FormulaToDictionary(res.MolecularFormula(chargedPeptide.Sequence));
            int symbolCount;
            formula.TryGetValue(TracerSymbol, out symbolCount);
            return symbolCount;
        }
        public Dictionary<double, double> GetSpectrum(double ape, ChargedPeptide chargedPeptide)
        {
            return GetResidueComposition(ape).GetIsotopeMasses(chargedPeptide);
        }
        public Dictionary<double, double> GetEnrichedSpectrum(ChargedPeptide chargedPeptide, int enrichmentCount)
        {
            var res = GetUnenrichedResidueComposition();
            Dictionary<String, int> formula = res.FormulaToDictionary(res.MolecularFormula(chargedPeptide.Sequence));
            int symbolCount;
            formula.TryGetValue(TracerSymbol, out symbolCount);
            if (symbolCount < enrichmentCount)
            {
                return null;
            }
            formula[TracerSymbol] = symbolCount - enrichmentCount;
            if (enrichmentCount != 0)
            {
                formula.Add(ENRICHED_ELEMENT, enrichmentCount);
            }

            Dictionary<double, double> dict = res.GetIsotopeMasses(res.DictionaryToFormula(formula),
                                                                   chargedPeptide.Charge);
            dict = Dictionaries.OffsetKeys(dict, res.GetMzDelta(chargedPeptide));
            return dict;
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
