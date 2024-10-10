/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0I
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{

    /// <summary>
    /// This collection class serves to replace the many places in code where we formerly 
    /// indexed items by (inherently positive integer) charge state.
    /// </summary>
    public class AdductMap<T>
    {
        private readonly Dictionary<Adduct, T> _dict;

        public AdductMap()
        {
            _dict = new Dictionary<Adduct, T>();
        }

        public T this[Adduct a]
        {
            get
            {
                T item;
                if (!_dict.TryGetValue(a, out item))
                {
                    item = default(T);
                    _dict.Add(a, item);
                }
                return item;
            }
            set
            {
                _dict[a] = value;
            }            
        }

        public IEnumerable<Adduct> Keys { get { return _dict.Keys; } }
    }

    public class Adduct : Immutable, IComparable, IEquatable<Adduct>, IAuditLogObject
    {
        private Molecule Composition { get; set; } // The chemical makeup of the adduct - the "2H" part in 4M3Cl37+2H
        private string Description { get; set; } // The text description (will be empty for protonation, we just use charge)
        public TypedMass AverageMassAdduct { get; private set; } // Average mass of the adduct itself - the "2H" in 4M3Cl37+2H
        public TypedMass MonoMassAdduct { get; private set; } // Monoisotopic mass of the adduct itself - the "2H" in 4M3Cl37+2H
        private int MassMultiplier { get; set; } // Returns, for example, the 2 in "[2M+Na]", which means the ion is two molecules + the adduct mass. 
        public TypedMass IsotopesIncrementalAverageMass { get; private set; } // The incremental average mass due to (4*3) (Cl37 - Cl) in 4M3Cl37+2H 
        public TypedMass IsotopesIncrementalMonoMass { get; private set; } // The incremental mono mass due to (4*3) (Cl37 - Cl) in 4M3Cl37+2H

        private int _hashCode; // We want comparisons to be on the same order as comparing ints, as when we used to just use integer charge instead of proper adducts

        // We tend to see the same strings again and again, save some parsing time by maintaining a threadsafe lookup for each ADDUCT_TYPE
        private static ConcurrentDictionary<string, Adduct>[] _knownAdducts = new ConcurrentDictionary<string, Adduct>[]
        {
            new ConcurrentDictionary<string, Adduct>(),
            new ConcurrentDictionary<string, Adduct>(),
            new ConcurrentDictionary<string, Adduct>()
        };

        //
        // Note constructors are private - use FromCharge and FromString instead, which allow reuse
        //

        public enum ADDUCT_TYPE
        {
            proteomic, // parsing "2" results in an adduct w/ formula H2, z=2, displays as "2" or "++"
            non_proteomic, // parsing "2" results in an adduct w/ formula H2, z=2, displays as "[M+2H]"
            charge_only // parsing "2" results in an adduct w/ no formula, z=2, displays as "[M+2]" or "++"
        }

        private Adduct(int charge, bool protonated)
        {
            InitializeAsCharge(charge, protonated ? ADDUCT_TYPE.proteomic : ADDUCT_TYPE.charge_only);
            SetHashCode(); // For fast GetHashCode()
        }

        private Adduct(string description, ADDUCT_TYPE integerMode, int? explicitCharge = null, bool strict = false) // Description should have form similar to M+H, 2M+2NA-H etc, or it may be a text representation of a protonated charge a la "2", "-3" etc
        {
            var input = (description ?? string.Empty).Trim();
            int chargeFromText;
            if (input.Length == 0)
            {
                // No text description
                InitializeAsCharge(explicitCharge ?? 0, integerMode);
            }
            else if (int.TryParse(input, out chargeFromText))
            {
                // Described purely as a charge
                InitializeAsCharge(chargeFromText, integerMode);
            }
            else
            {
                if (!input.StartsWith(@"[") && !input.Contains(@"["))
                {
                    // Accept a bare "M+Na", but put it in canonical form "[M+Na]"
                    input = @"[" + input + @"]";
                }

                // Watch for strange construction from Agilent MP system e.g. (M+H)+ and (M+H)+[-H2O]
                if (input.StartsWith(@"(") && input.Contains(@")") && input.Contains(@"M"))
                {
                    var parts = input.Split('['); // Break off water loss etc, if any
                    if (parts.Length == 1 || input.IndexOf(')') < input.IndexOf('['))
                    {
                        var constructed = parts[0].Replace(@"(", @"[").Replace(@")", @"]");
                        if (parts.Length > 1) // Deal with water loss etc
                        {
                            // Rearrange (M+H)+[-H2O] as [M-H2O+H]+
                            var mod = parts[1].Split(']')[0]; // Trim end
                            var mPos = input.IndexOf('M');
                            constructed = constructed.Substring(0, mPos+1) + mod + constructed.Substring(mPos + 1);
                        }
                        if (TryParse(constructed, out _))
                        {
                            input = constructed; // Constructed string is parseable
                        }
                    }
                }


                // Check for implied positive ion mode - we see "MH", "MH+", "MNH4+" etc in the wild
                // Also watch for for label-only like  "[M2Cl37]"
                var posNext = input.IndexOf('M') + 1;
                if (posNext > 0 && posNext < input.Length)
                {
                    var posClose = input.IndexOf(']');
                    if (posClose >= 0 && posClose < posNext)
                    {
                        // This isn't an adduct description, probably actually examining a modified peptide e.g. K[1Ac]IDGFGPMK
                        throw new InvalidDataException(
                            string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__, input));
                    }
                    if (input[posNext] != '+' && input[posNext] != '-') 
                    {
                        // No leading + or - : is it because description starts with a label, or because + mode is implied?
                        var labelEnd = FindLabelDescriptionEnd(input);
                        if (labelEnd.HasValue)
                        {
                            if (input.LastIndexOfAny(new []{'+','-'}) < labelEnd.Value)
                            {
                                // Pure labeling - add a trailing + for parseability
                                input = input.Replace(@"]", @"+0]");
                            }
                        }
                        else if (input[posNext] != ']')  // Leave @"[M]" or @"[2M]" alone
                        {
                            // Implied positive mode
                            input = input.Replace(@"M", @"M+");
                        }
                    }
                }

                if (strict && !Equals(input, description))
                {
                    // Caller wanted no tidying, initialize as an empty adduct
                    InitializeAsCharge(0, ADDUCT_TYPE.charge_only);
                    SetHashCode(); 
                    return;
                }
                ParseDescription(Description = input);
                InitializeMasses();
            }

            if (explicitCharge.HasValue)
            {
                if (AdductCharge != 0) // Does claimed charge agree with obviously calcuable charge?
                {
                    Assume.IsTrue(AdductCharge == explicitCharge, @"Conflicting charge values in adduct description "+input );
                }
                AdductCharge = explicitCharge.Value;
            }
            SetHashCode(); // For fast GetHashCode()
        }

        private static int? FindLabelDescriptionEnd(string input)
        {
            var posNext = input.IndexOf('M') + 1;
            if (posNext > 0)
            {
                if (input[posNext] == '(')
                {
                    var close = input.LastIndexOf(')');
                    if (close > posNext)
                    {
                        return close+1;
                    }
                }
                else if (input[posNext] != '+' && input[posNext] != '-')
                {
                    // No leading + or - : is it because description starts with a label, or because + mode is implied?
                    var limit = input.IndexOfAny(new[] { '+', '-', ']' });
                    if (limit < posNext)
                    {
                        return null;
                    }

                    if (double.TryParse(input.Substring(posNext, limit - posNext),
                            NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out _))
                    {
                        return limit;  // Started with a mass label
                    }
                    while (posNext < limit)
                    {
                        if (char.IsDigit(input[posNext]))
                        {
                            posNext++;
                        }
                        else
                        {
                            var remain = input.Substring(posNext);
                            if (DICT_ADDUCT_ISOTOPE_NICKNAMES.Keys.Any(k => remain.StartsWith(k)))
                            {
                                // It's at least trying to be an isotopic label
                                return limit;
                            }
                            break;
                        }
                    }
                }
            }
            return null;
        }

        private static readonly Regex ADDUCT_OUTER_REGEX =
            new Regex(
                @"\[?(?<multM>.*?)M(?<label>(\(.*\)|[^\+\-]*))?(?<adduct>[\+\-][^\]]*)(\](?<declaredChargeCount>\d*)(?<declaredChargeSign>[+-]*)?)?$",
                RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ADDUCT_INNER_REGEX = new Regex(@"(?<oper>\+|\-)(?<multM>\d+)?\(?(?<ion>[^-+\)]*)\)?",
            RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ADDUCT_ION_REGEX = new Regex(@"(?<multM>\d+)?(?<ion>[A-Z][a-z]?['\""]?)",
            RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ADDUCT_NMER_ONLY_REGEX = new Regex(@"\[(?<multM>\d*)M\]$",
            RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private int? ParseChargeDeclaration(string adductOperations)
        {
            int parsedCharge;
            int? result = null;
            if (adductOperations.StartsWith(@"+") && adductOperations.Distinct().Count() == 1) // @"[M+]" is legit, @"[M+++]" etc is presumably also legit
            {
                result = adductOperations.Length;
            }
            else if (adductOperations.StartsWith(@"-") && adductOperations.Distinct().Count() == 1) // @"[M-]", @"[M---]" etc are presumably also legit
            {
                result = -adductOperations.Length;
            }
            else if (int.TryParse(adductOperations, out parsedCharge)) // "[M+2]", "[M-3]" etc
            {
                result = parsedCharge;
            }
            return result;
        }

        /// <summary>
        /// Examine a string to see if it contains an adduct description at the end
        /// </summary>
        /// <param name="formulaWithAdduct">possibly a string ending with an adduct description</param>
        /// <param name="result">resulting adduct object, if any</param>
        /// <returns>input string trimmed of adduct string</returns>
        public static string FindInFormula(string formulaWithAdduct, out Adduct result)
        {
            if (!string.IsNullOrEmpty(formulaWithAdduct))
            {
                var possibleAdductStart = formulaWithAdduct.LastIndexOf('[');
                if (possibleAdductStart >= 0) // Has '['
                {
                    var possible = formulaWithAdduct.Substring(possibleAdductStart);
                    if (possible.Count(c => c == ']') == 1 && // Has closing ']'
                        possible[1] != '+' && possible[1] != '-') // Isn't a mass modification
                    {
                        if (Adduct.TryParse(possible, out result))
                        {
                            return formulaWithAdduct.Substring(0,possibleAdductStart).Trim();
                        }
                    }
                }
            }
            result = EMPTY;
            return formulaWithAdduct; // Not found
        }

        private void ParseDescription(string input)
        {
            int? declaredCharge = null;
            int? calculatedCharge = null;
            IsotopeLabels = ParsedMolecule.EMPTY;
            var match = ADDUCT_OUTER_REGEX.Match(input.Trim());
            var success = match.Success && (match.Groups.Count == 6);

            string adductOperations; 
            success &= !string.IsNullOrEmpty(adductOperations = match.Groups[@"adduct"].Value);

            // Check for sane bracketing (none, or single+balanced+anchored)
            if (success)
            {
                var brackets = input.Count(c => c == '[');
                success &= brackets == input.Count(c => c == ']');
                if (brackets != 0)
                {
                    success &= (brackets == 1) && input.StartsWith(@"[");
                }
            }
            var composition = new Dictionary<string, int>();
            if (success)
            {
                // Read the mass multiplier if any - the "2" in "[2M+..." if any such thing is there
                var massMultiplier = 1;
                var massMultiplierStr = match.Groups[@"multM"].Value;
                if (!string.IsNullOrEmpty(massMultiplierStr) && !massMultiplierStr.StartsWith(@"("))
                {
                    success = int.TryParse(massMultiplierStr, out massMultiplier);
                    if (!success || massMultiplier <= 0)
                    {
                        throw new InvalidDataException(
                            string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__, input));
                    }
                }
                MassMultiplier = massMultiplier;

                // Read any isotope declarations

                // Read the "4Cl37" in "[2M4Cl37+..." if any such thing is there
                // Also deal with more complex labels, eg 6C132N15 -> 6C'2N'
                var label = match.Groups[@"label"].Value.Split(']')[0].Trim('(', ')'); // In case adduct had form like M(-1.2345)+H or [2M2Cl37]+3
                var hasIsotopeLabels = !string.IsNullOrEmpty(label);
                if (hasIsotopeLabels)
                {
                    var isotopeLabels = new Dictionary<string, int>();
                    double labelMass = 0;

                    if (double.TryParse(label, NumberStyles.Float, CultureInfo.InvariantCulture, out var tryLabelMass))
                    {
                        // Sometimes all we're given is a mass offset eg M1.002+2H
                        labelMass = tryLabelMass;
                    }
                    else
                    {
                        // Verify that everything in the label can be understood as isotope counts
                        // ReSharper disable LocalizableElement
                        var test = DICT_ADDUCT_ISOTOPE_NICKNAMES.Aggregate(label, (current, nickname) => current.Replace(nickname.Key, "\0")); //  2Cl373H2 -> "2\03\0"
                        // ReSharper restore LocalizableElement
                        if (test.Any(t => !char.IsDigit(t) && t != '\0') || test[test.Length - 1] != '\0') // This will catch 2Cl373H -> "2\03H" or 2Cl373H23 -> "2\03\03"
                        {
                            var errmsg = string.Format(UtilResources.Adduct_ParseDescription_isotope_error,
                                    match.Groups[@"label"].Value.Split(']')[0], input, string.Join(@" ", DICT_ADDUCT_ISOTOPE_NICKNAMES.Keys));
                            throw new InvalidDataException(errmsg);
                        }

                        label = DICT_ADDUCT_ISOTOPE_NICKNAMES.Aggregate(label, (current, nickname) => current.Replace(nickname.Key, nickname.Value)); // eg Cl37 -> Cl'
                        // Problem: normal chemical formula for "6C132N15H" -> "6C'2NH'" would be "C'6N'15H"
                        var ionMatches = ADDUCT_ION_REGEX.Matches(label);
                        foreach (Match m in ionMatches)
                        {
                            if (m.Groups.Count < 1)
                            {
                                success = false;
                                break;
                            }
                            var multiplierM = 1;
                            var multMstr = m.Groups[@"multM"].Value; // Read the @"2" in @"+2H" if any such thing is there
                            if (!string.IsNullOrEmpty(multMstr))
                            {
                                success = int.TryParse(multMstr, out multiplierM);
                            }

                            var isotope = m.Groups[@"ion"].Value;
 
                            isotopeLabels.Add(isotope, multiplierM);
                        }
                    }
                    IsotopeLabels = ParsedMolecule.Create(Molecule.FromDict(isotopeLabels), 
                       new TypedMass(labelMass, MassType.MonoisotopicHeavy), 
                       new TypedMass(labelMass, MassType.AverageHeavy));
                }

                var declaredChargeCountStr = match.Groups[@"declaredChargeCount"].Value;
                if (!string.IsNullOrEmpty(declaredChargeCountStr)) // Read the "2" in "[M+H+Na]2+" if any such thing is there
                {
                    if (!string.IsNullOrEmpty(declaredChargeCountStr))
                    {
                        int z;
                        success = int.TryParse(declaredChargeCountStr, out z);
                        declaredCharge = z;
                    }
                }
                var declaredChargeSignStr = match.Groups[@"declaredChargeSign"].Value;
                if (!string.IsNullOrEmpty(declaredChargeSignStr))
                    // Read the "++" in "[M+2H]++" or "+" in "]2+" if any such thing is there
                {
                    declaredCharge = (declaredCharge ?? 1)*
                                     (declaredChargeSignStr.Count(c => c == '+') - declaredChargeSignStr.Count(c => c == '-'));
                }

                // Check for M+, M--, M+3 etc
                var parsedCharge = ParseChargeDeclaration(adductOperations);
                if (parsedCharge.HasValue)
                {
                    calculatedCharge = parsedCharge;
                }
                else
                {
                    // If trailing part of declaration is of form +2, -3, -- etc, treat it as an explicit charge as in "[M+H+]" or "[M+H+1]"
                    var lastSign = Math.Max(adductOperations.LastIndexOf(@"-", StringComparison.Ordinal), adductOperations.LastIndexOf(@"+", StringComparison.Ordinal));
                    if (lastSign > -1 && (lastSign == adductOperations.Length-1 || adductOperations.Substring(lastSign+1).All(char.IsDigit)))
                    {
                        while (lastSign > 0 && adductOperations[lastSign - 1] == adductOperations[lastSign])
                        {
                            lastSign--;
                        }
                        parsedCharge = ParseChargeDeclaration(adductOperations.Substring(lastSign));
                        if (parsedCharge.HasValue)
                        {
                            declaredCharge = parsedCharge;
                            adductOperations = adductOperations.Substring(0, lastSign);
                        }
                    }

                    // Now parse each part of the "+Na-2H" in "[M+Na-2H]" if any such thing is there
                    var matches = ADDUCT_INNER_REGEX.Matches(adductOperations);
                    int remaining = matches.Count;
                    foreach (Match m in matches)
                    {
                        remaining--;
                        if (m.Groups.Count < 4)
                        {
                            success = false;
                            break;
                        }
                        var multiplierM = 1;
                        var multMstr = m.Groups[@"multM"].Value; // Read the @"2" in @"+2H" if any such thing is there
                        if (!string.IsNullOrEmpty(multMstr))
                        {
                            success = int.TryParse(multMstr, out multiplierM);
                        }
                        if (m.Groups[@"oper"].Value.Contains(@"-"))
                        {
                            multiplierM *= -1;
                        }
                        var ion = m.Groups[@"ion"].Value;
                        int ionCharge;
                        if (DICT_ADDUCT_ION_CHARGES.TryGetValue(ion, out ionCharge))
                        {
                            calculatedCharge = (calculatedCharge ?? 0) + ionCharge*multiplierM;
                        }

                        // Swap common nicknames like "DMSO" for "C2H6OS", or "N15" for N'
                        string realname;
                        if (DICT_ADDUCT_NICKNAMES.TryGetValue(ion, out realname) || DICT_ADDUCT_ISOTOPE_NICKNAMES.TryGetValue(ion, out realname))
                        {
                            ion = realname;
                        }

                        try
                        {
                            var ionMolecule = ParsedMolecule.Create(ion);
                            if (ionMolecule.IsEmpty)
                            {
                                success = multiplierM == 1 && remaining != 0; // Allow pointless + in "M+-H2O+H" but not trailing +in "M-H2O+H+"
                            }
                            foreach (var pair in ionMolecule)
                            {
                                int count;
                                if (composition.TryGetValue(pair.Key, out count))
                                {
                                    count += pair.Value * multiplierM;
                                    if (count == 0)
                                    {
                                        composition.Remove(pair.Key);
                                    }
                                    else
                                    {
                                        composition[pair.Key] = count;
                                    }
                                }
                                else if (pair.Value != 0)
                                {
                                    composition.Add(pair.Key, pair.Value * multiplierM);
                                }
                            }
                        }
                        catch (ArgumentException)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__,
                                    ion, input));
                        }
                    }
                }
            }
            AdductCharge = declaredCharge ?? calculatedCharge ?? 0; // If user supplied a charge (e.g. [M+2CH3+Cl]+) believe them even if our calculation/guess disagrees
            if (!composition.Keys.All(k => BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)))
            {
                throw new InvalidDataException(
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__,
                        composition.Keys.First(k => !BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)), input));
            }
            Composition = Molecule.FromDict(composition);
            if (!success)
            {
                // Allow charge free neutral like [M] or nmer like [3M]
                match = ADDUCT_NMER_ONLY_REGEX.Match(input);
                if (match.Success && match.Groups.Count == 2)
                {
                    success = true;
                    var massMultiplier = 1;
                    var massMultiplierStr = match.Groups[@"multM"].Value;
                    if (!string.IsNullOrEmpty(massMultiplierStr))
                    {
                        success = int.TryParse(massMultiplierStr, out massMultiplier);
                    }
                    MassMultiplier = massMultiplier;
                }
                if (!success)
                {
                    throw new InvalidDataException(
                        string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__, input));
                }
            }
            if (declaredCharge.HasValue && calculatedCharge.HasValue && declaredCharge != calculatedCharge)
            {
                // Only complain if this is an obvious error, like "[M+H]-"
                foreach (var adducts in new[] { COMMON_PROTONATED_ADDUCTS, COMMON_SMALL_MOL_ADDUCTS})
                {
                    foreach (var adduct in adducts)
                    {
                        if (Equals(Composition, adduct.Composition))
                        {
                            throw new InvalidDataException(
                                string.Format(
                                    UtilResources
                                        .BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0____declared_charge__1__does_not_agree_with_calculated_charge__2_,
                                    input, declaredCharge.Value, calculatedCharge));
                        }
                    }
                }
            }
        }
        public Adduct Unlabeled { get; private set; } // Version of this adduct without any isotope labels

        public ParsedMolecule IsotopeLabels { get; private set; } // Isotope information - the "3Cl37" in  4M3Cl37+2H or "1.23" in 2M(1.23)+Na

        // N.B. "AdductCharge" and "AdductFormula" seem like weirdly redundant names, until you consider that 
        // they can show up in reports, at which point "Charge" and "Formula" are a bit overloaded.

        public int AdductCharge { get; private set; }  // The charge that the adduct gives to a molecule

        public string AdductFormula // Return adduct description - will produce [M+H] format for protonation
        {
            get
            {
                if (IsEmpty)
                {
                    return string.Empty;
                }
                if (IsProteomic) // We don't carry description for peptide protonation, generate one here
                {
                    switch (AdductCharge)
                    {
                        case 1:
                            return @"[M+H]";
                        case -1:
                            return @"[M-H]";
                        default:
                            return string.Format(@"[M{0:+#;-#}H]", AdductCharge);
                    }
                }
                return !string.IsNullOrEmpty(Description) ? Description : string.Format(@"[M{0:+#;-#}]", AdductCharge);
            }
        }

        public bool IsProtonated { get; private set; } // When true, we use a slightly different mz calc for backward compatibility

        public bool IsChargeOnly { get { return Composition.Count == 0 && !HasIsotopeLabels; } }

        public bool IsProteomic { get; private set; } //  For peptide use

        public bool IsEmpty { get { return ReferenceEquals(this, EMPTY); } }

        public static bool IsNullOrEmpty(Adduct adduct)
        {
            return adduct == null || adduct.IsEmpty;
        }
        public bool HasIsotopeLabels => IsotopeLabels.HasMassOffsets || IsotopeLabels.HasChemicalFormula; // Does the adduct description include isotopes, like "6Cl37" in "M6Cl37+2H"


        // Helper function for UI - does this string look like it's on its way to being an adduct?
        public static bool PossibleAdductDescriptionStart(string possibleDescription)
        {
            if (string.IsNullOrEmpty(possibleDescription))
                return false;
            Adduct val;
            if (TryParse(possibleDescription, out val) && !val.IsEmpty)
            {
                return true; // An actual adduct description
            }
            return possibleDescription.StartsWith(@"[") || possibleDescription.StartsWith(@"M");
        }

        /// <summary>
        /// Construct an adduct based on a string (probably serialized XML) of form "2" or "-3" or "[M+Na]" etc.
        /// Minimizes memory thrash by reusing the more common adducts. 
        /// Assumes protonated adduct when dealing with charge only, 
        /// so "2" gives adduct z=2 formula=H2 ToString="++".
        /// </summary>
        public static Adduct FromStringAssumeProtonated(string value)
        {
            return FromString(value, ADDUCT_TYPE.proteomic, null);
        }
        /// Same as above, but assumes charge-only adduct when dealing with integer charge only,
        /// so "2" gives adduct z=2 formula=<none></none> ToString="[M+2]".
        public static Adduct FromStringAssumeChargeOnly(string value)
        {
            return FromString(value, ADDUCT_TYPE.charge_only, null);
        }

        /// Same as above, but assumes charge-only adduct when dealing with integer charge only,
        /// so "2" gives adduct z=2 formula=H2 ToString="[M+2H]".
        public static Adduct FromStringAssumeProtonatedNonProteomic(string value)
        {
            return FromString(value, ADDUCT_TYPE.non_proteomic, null);
        }

        /// <summary>
        /// Construct an adduct based on a string (probably serialized XML) of form "2" or "-3" or "[M+Na]" etc.
        /// Minimizes memory thrash by reusing the more common adducts.
        /// In strict mode, won't attempt any syntax correction
        ///
        /// </summary>
        public static Adduct FromString(string value, ADDUCT_TYPE parserMode, int? explicitCharge, bool strict=false)
        {
            if (value == null)
                return EMPTY;

            // Quick check to see if we've encountered this description before
            var dict = _knownAdducts[(int)parserMode];
            if (dict.TryGetValue(value, out var knownAdduct))
            {
                return knownAdduct;
            }

            if (int.TryParse(value, out var z))
            {
                var result = FromCharge(z, parserMode);
                dict[value] = result; // Cache this on the likely chance that we'll see this representation again
                return result;
            }

            // Reuse the more common non-proteomic adducts
            var testValue = value.StartsWith(@"M") ? @"[" + value + @"]" : value;
            var testAdduct = new Adduct(testValue, parserMode, explicitCharge, strict);
            if (!testValue.EndsWith(@"]"))
            {
                // Can we trim any trailing charge info to arrive at a standard form (ie use [M+H] instead of [M+H]+)?
                try
                {
                    var stripped = testValue.Substring(0, testValue.IndexOf(']')+1);
                    var testB = new Adduct(stripped, parserMode, explicitCharge);
                    if (testAdduct.SameEffect(testB))
                        testAdduct = testB; // Go with the simpler canonical form
                }
                catch
                {
                    // ignored
                }
            }
            // Re-use the standard pre-allocated adducts when possible
            foreach (var adduct in (parserMode == ADDUCT_TYPE.proteomic) ? COMMON_PROTONATED_ADDUCTS : COMMON_SMALL_MOL_ADDUCTS) 
            {
                if (testAdduct.SameEffect(adduct))
                {
                    dict[value] = adduct;  // Cache this on the likely chance that we'll see this representation again
                    return adduct;
                }
            }
            dict[value] = testAdduct;  // Cache this on the likely chance that we'll see this representation again
            if (strict && !Equals(testAdduct.Description, value))
            {
                return EMPTY; // Caller wants no attempts at tidying up
            }
            return testAdduct;
        }

        /// <summary>
        /// Given, for example, charge=-3, return an Adduct with z=-3, empty formula, displays as "[M-3]"
        /// </summary>
        public static Adduct FromChargeNoMass(int charge)
        {
            switch (charge)
            {
                case 0:
                    return EMPTY;
                case 1:
                    return M_PLUS; // [M+]
                case -1:
                    return M_MINUS; // [M-]
                case 2:
                    return M_PLUS_2; // [M+2]
                case -2:
                    return M_MINUS_2; // [M-2]
                case 3:
                    return M_PLUS_3; // [M+3]
                case -3:
                    return M_MINUS_3; // [M-3]
                default:
                    return new Adduct(string.Format(@"[M{0:+#;-#}]", charge), ADDUCT_TYPE.charge_only, charge);
            }
        }

        /// <summary>
        /// Given, for example, charge=3, return an Adduct with z=3, formula = H3, displays as "[M+3H]"
        /// </summary>
        public static Adduct NonProteomicProtonatedFromCharge(int charge)
        {
            if (charge == 0)
                return EMPTY;
            var adductTmp = FromChargeProtonated(charge);
            return new Adduct(adductTmp.AdductFormula, ADDUCT_TYPE.non_proteomic, charge);  // Create an adduct that shows a formula in ToString()
        }

        /// <summary>
        /// Same as NonProteomicProtonatedFromCharge(int charge), but also accepts isotope information
        /// </summary>
        public static Adduct NonProteomicProtonatedFromCharge(int charge, IDictionary<string, int> dictIsotopeCounts)
        {
            var adductTmp = NonProteomicProtonatedFromCharge(charge);
            if (dictIsotopeCounts != null && dictIsotopeCounts.Count > 0)
            {
               // Convert from our chemical formula syntax to that used by adducts
                var adductIons = dictIsotopeCounts.Aggregate(@"[M", (current, pair) => current + string.Format(CultureInfo.InvariantCulture, @"{0}{1}",
                    (pair.Value>1) ? pair.Value.ToString() : string.Empty, 
                    (DICT_ADDUCT_NICKNAMES.FirstOrDefault(x => x.Value == pair.Key).Key ?? DICT_ADDUCT_ISOTOPE_NICKNAMES.FirstOrDefault(x => x.Value == pair.Key).Key) ??pair.Key));
                var adductTextClose = (charge == 0) ? @"]" : adductTmp.AdductFormula.Substring(2);
                return new Adduct(adductIons + adductTextClose, ADDUCT_TYPE.non_proteomic, charge);
            }
            if (charge == 0)
            {
                return EMPTY;
            }
            return new Adduct(adductTmp.AdductFormula, ADDUCT_TYPE.non_proteomic, charge);  // Create an adduct that shows a formula in ToString()
        }

        public static Adduct FromFormulaDiff(string left, string right, int charge)
        {
            // Take adduct as the difference between two chemical formulas
            var l = ParsedMolecule.Create(left.Trim());
            var r = ParsedMolecule.Create(right.Trim());
            return FromFormulaDiff(l, r, charge);
        }

        public static Adduct FromFormulaDiff(ParsedMolecule left, ParsedMolecule right, int charge)
        {
            var adductFormula = left.Difference(right).ToString();
            if (string.IsNullOrEmpty(adductFormula))
            {
                return FromChargeNoMass(charge);
            }
            var sign = adductFormula.StartsWith(@"-") ? string.Empty : @"+";
            var signZ = charge < 0 ? @"-" : @"+";
            // Emit something like [M-C4H]2-"
            return new Adduct(string.Format(@"[M{0}{1}]{2}{3}", sign, adductFormula, Math.Abs(charge), signZ), ADDUCT_TYPE.non_proteomic) { AdductCharge = charge };
        }

        public static Adduct ProtonatedFromFormulaDiff(string left, string right, int charge)
        {
            // Take adduct as the difference between two chemical formulas, assuming that H is for protonation
            var l = ParsedMolecule.Create(left.Trim());
            var r = ParsedMolecule.Create(right.Trim());
            return ProtonatedFromFormulaDiff(l, r, charge);
        }

        public static Adduct ProtonatedFromFormulaDiff(ParsedMolecule left, ParsedMolecule right, int charge)
        {
            // Take adduct as the difference between two chemical formulas, assuming that H is for protonation
            var d = left.Difference(right).Molecule;
            if (d.Values.Any(count => count < 0) || d.Values.All(count => count == 0))
            {
                return NonProteomicProtonatedFromCharge(charge); // No difference in formulas, try straight protonation
            }

            // Any difference in H can be used as explanation for charge
            int nH;
            if (d.TryGetValue(BioMassCalc.H, out nH) && nH != 0)
            {
                d = d.SetElementCount(BioMassCalc.H, Math.Max(0, nH - charge));
            }

            var adductFormula = d.ToString();

            if (string.IsNullOrEmpty(adductFormula))
            {
                return NonProteomicProtonatedFromCharge(charge); // The entire formula difference was protonation
            }
            var sign = adductFormula.StartsWith(@"-") ? string.Empty : @"+";
            // Emit something like [M-C4+H3] or [M+Cl-H]
            if (Math.Abs(charge) > 1)
            {
                return new Adduct(string.Format(@"[M{0}{1}{2:+#;-#}H]", sign, adductFormula, charge), ADDUCT_TYPE.non_proteomic) { AdductCharge = charge };
            }
            return new Adduct(string.Format(@"[M{0}{1}{2}H]", sign, adductFormula, charge>0?@"+":@"-"), ADDUCT_TYPE.non_proteomic) { AdductCharge = charge };
        }

        /// <summary>
        /// Splits a string which might be a formula and adduct (e.g. C12H5[M+H] returns "C12H5" and sets adduct to Adduct.M_PLUS_H)
        /// </summary>
        public static string SplitFormulaAndTrailingAdduct(string input, ADDUCT_TYPE adductType, out Adduct adduct)
        {
            var formulaAndAdductText = (input??string.Empty).Trim();
            if (string.IsNullOrEmpty(formulaAndAdductText))
            {
                adduct = EMPTY;
                return string.Empty;
            }
            var parts = Regex.Split(formulaAndAdductText, @"\[(?![+-])");  // Split on [ but not on [+ or [-
            if (!Adduct.TryParse(formulaAndAdductText.Substring(parts[0].Length), out adduct, adductType))
            {
                adduct = EMPTY;
            }
            return parts[0];
        }
        
        /// <summary>
        /// Replace, for example, the "2" in "[2M+H]"
        /// </summary>
        public Adduct ChangeMassMultiplier(int value)
        {
            if (value == MassMultiplier)
                return this; // No change
            var indexM = AdductFormula.IndexOf('M');
            if (indexM < 1)
                return this;
            var newFormula = (value > 1 ? string.Format(@"[{0}", value) : @"[") + AdductFormula.Substring(indexM);
            return Equals(AdductFormula, newFormula) ? this : new Adduct(newFormula, ADDUCT_TYPE.non_proteomic, AdductCharge);
        }

        private Adduct ChangeIsotopeLabels(string isotopes)
        {
            if (string.IsNullOrEmpty(isotopes) && !HasIsotopeLabels)
            {
                return this;
            }
            var indexM = AdductFormula.IndexOf('M');
            if (indexM < 1)
            {
                return this;
            }
            var signIndex = FindSignIndex(AdductFormula);
            if (signIndex < 0)
            {
                return EMPTY; // Error
            }
            var newFormula = AdductFormula.Substring(0, indexM + 1) + isotopes + AdductFormula.Substring(signIndex);
            return Equals(AdductFormula, newFormula) ? this : new Adduct(newFormula, ADDUCT_TYPE.non_proteomic, AdductCharge); // No reason isotopes should change charge
        }


        /// <summary>
        /// Replace, for example, the "6C13" in "[M6C13+Na]"
        /// Accepts a dictionary of isotope,count where isotope is either in Skyline vernacular Cl', or adduct-speak Cl37
        /// </summary>
        public Adduct ChangeIsotopeLabels(IDictionary<string, int> isotopes)
        {
            if ((isotopes==null || isotopes.Count==0) && !HasIsotopeLabels)
            {
                return this;
            } 
            return ChangeIsotopeLabels(
                isotopes == null || isotopes.Count == 0 ? string.Empty : isotopes.Aggregate(string.Empty,
                    (current, pair) => current +
                                       ((pair.Value == 0) ? string.Empty : // We see things like H'0 (that's a zero) in the wild
                                           (string.Format(CultureInfo.InvariantCulture, @"{0}{1}",
                                               (pair.Value > 1) ? pair.Value.ToString() : string.Empty,
                                               // If label was described (for example) as Cl' in dict, look up Cl37 and use that
                                               DICT_ADDUCT_ISOTOPE_NICKNAMES.FirstOrDefault(x => x.Value == pair.Key).Key ?? pair.Key))))); 
        }

        // Sometimes all we know is that two analytes have same name but different masses - describe isotope label as a mass
        public Adduct ChangeIsotopeLabels(double value, int? precision = null)
        {
            var format = @".0########".Substring(0, Math.Min(1 + (precision ?? 5), 10));
            var valStr =  value.ToString(format, CultureInfo.InvariantCulture);
            if (valStr.Equals(@".0"))
            {
                value = 0;
            }
            if (value < 0)
            {
                return ChangeIsotopeLabels(string.Format(@"({0})", valStr));
            }
            return ChangeIsotopeLabels(value==0 ? string.Empty :  valStr);
        }

        // Change the charge multiplier if possible
        // ie for charge 2, [M+Na] -> [M+2Na] but [M+3Na-H] goes to [M+2H] because it's ambiguous
        public Adduct ChangeCharge(int newCharge)
        {
            if (Equals(newCharge, AdductCharge))
                return this;
            // If it is proteomic, simply create a new proteomic adduct with the new charge.
            if (IsProteomic)
                return FromChargeProtonated(newCharge);

            if (AdductCharge == 0)
            {
                // Adduct doesn't have any cue for charge state, so append one: eg [M+S] => [M+S]+
                var adductFormula = AdductFormula;
                string sign;
                if (Math.Abs(newCharge) < 3)
                {
                    // Use ++ or -- type notation
                    sign = (newCharge > 0) ? plusses.Substring(0, newCharge) : minuses.Substring(0, -newCharge);
                }
                else
                {
                    // Use +4, -5 type notation
                    sign = newCharge.ToString(@"+#;-#");
                }
                return FromStringAssumeChargeOnly(adductFormula+sign);
            }

            var formula = AdductFormula;
            var signIndex = FindSignIndex(formula); // Skip over any isotope description - might contain "-"
            if (signIndex > 0)
            {
                if (formula.Substring(signIndex).Count(c => c == '+' || c == '-') == 1) // Reject multipart adducts - don't know which parts to change
                {
                    var oldcount = formula.Substring(signIndex, 1) + new string(formula.Substring(signIndex + 1).TakeWhile(char.IsDigit).ToArray()); // Find the +2 in [M+2Na] or the + in [M+H]
                    var newcount = (newCharge < 0 ? @"-" : @"+") + (Math.Abs(newCharge) > 1 ? Math.Abs(newCharge).ToString(CultureInfo.InvariantCulture) : string.Empty);
                    formula = formula.Substring(0,signIndex) + formula.Substring(signIndex).Replace(oldcount, newcount);
                    Adduct result;
                    if (TryParse(formula, out result))
                    {
                        if (result.AdductCharge == newCharge)
                        {
                            return result; // Revised charge looks good
                        }
                        if (result.AdductCharge == -newCharge)
                        {
                            // Revised charge is opposite of what we expected - adduct has opposite charge value of what we expected?
                            formula = formula.Substring(0, signIndex) + formula.Substring(signIndex).Replace(newCharge < 0 ? @"-" : @"+", newCharge < 0 ? @"+" : @"-");
                            if (TryParse(formula, out result) && result.AdductCharge == newCharge)
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            throw new InvalidDataException(string.Format(@"Unable to adjust adduct formula {0} to achieve charge state {1}", AdductFormula, newCharge));
        }

        /// <summary>
        /// Replace, for example, the "+Na" in "[M+Na]"
        /// </summary>
        public Adduct ChangeIonFormula(string val)
        {
            var end = AdductFormula.IndexOf(']');
            if (end < 0)
                return this;
            var formula = AdductFormula.Substring(0, end);
            var signIndex = FindSignIndex(formula);
            if (signIndex < 0) 
                return EMPTY;
            if (string.IsNullOrEmpty(val))
            {
                signIndex++; // Include a charge sense for parsability
            }
            var newFormula = formula.Substring(0, signIndex) + (val??string.Empty) + @"]";
            return Equals(AdductFormula, newFormula) ? this : new Adduct(newFormula, ADDUCT_TYPE.non_proteomic);
        }

        private int FindSignIndex(string formula)
        {
            formula = formula.Split(']')[0]; // Ignore the "++" in "[M2Cl37]++"
            var closeNumericIsotopeDescription = formula.IndexOf(')');
            if (closeNumericIsotopeDescription > 0)
            {
                // Skip over the (-1.2345) in "M(-1.2345)+2H"
                formula = formula.Substring(closeNumericIsotopeDescription);
            }
            else
            {
                closeNumericIsotopeDescription = 0;
            }
            var firstPlus = formula.IndexOf('+');
            var firstMinus = formula.IndexOf('-');
            if (firstPlus < 0)
                firstPlus = firstMinus;
            if (firstMinus < 0)
                firstMinus = firstPlus;
            var signIndex = Math.Min(firstPlus, firstMinus);
            return signIndex >= 0 ? signIndex + closeNumericIsotopeDescription : signIndex;
        }

        public static Adduct FromChargeProtonated(int? charge)
        {
            return charge.HasValue ? FromChargeProtonated(charge.Value) : EMPTY;
        }

        public static Adduct FromChargeProtonated(int charge)
        {
            return FromCharge(charge, ADDUCT_TYPE.proteomic);
        }

        public static Adduct FromCharge(int charge, ADDUCT_TYPE type)
        {
            var assumeProteomic = false;
            if (type == ADDUCT_TYPE.proteomic)
            {
                assumeProteomic = true;
                switch (charge)
                {
                    case 0:
                        return EMPTY;
                    case 1:
                        return SINGLY_PROTONATED;
                    case 2:
                        return DOUBLY_PROTONATED;
                    case 3:
                        return TRIPLY_PROTONATED;
                    case 4:
                        return QUADRUPLY_PROTONATED;
                    case 5:
                        return QUINTUPLY_PROTONATED;
                }
            }
            else if (type == ADDUCT_TYPE.non_proteomic)
            {
                switch (charge)
                {
                    case 0:
                        return EMPTY;
                    case 1:
                        return M_PLUS_H;
                    case 2:
                        return M_PLUS_2H;
                    case 3:
                        return M_PLUS_3H;
                    case -1:
                        return M_MINUS_H;
                    case -2:
                        return M_MINUS_2H;
                    case -3:
                        return M_MINUS_3H;
                }
            }
            else
            {
                switch (charge)
                {
                    case 0:
                        return EMPTY;
                    case 1:
                        return M_PLUS;
                    case 2:
                        return M_PLUS_2;
                    case 3:
                        return M_PLUS_3;
                    case -1:
                        return M_MINUS;
                    case -2:
                        return M_MINUS_2;
                    case -3:
                        return M_MINUS_3;
                }
            }
            return new Adduct(charge, assumeProteomic);
        }

        public static Adduct[] ProtonatedFromCharges(params int[] list)
        {
            return list.Select(FromChargeProtonated).ToArray();
        }

        public static bool TryParse(string s, out Adduct result, ADDUCT_TYPE assumeAdductType = ADDUCT_TYPE.non_proteomic, bool strict = false)
        {
            result = EMPTY;
            try
            {
                result = FromString(s, assumeAdductType, null, strict);
                return result.AdductCharge != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Some internals made public for test purposes
        /// </summary>
        public int GetMassMultiplier() { return MassMultiplier; }
        public Molecule GetComposition() { return Composition; }

        // Common terms for small molecule adducts per http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/ESI-MS-adducts.xls
        // See also (An interesting list of pseudoelements is at http://winter.group.shef.ac.uk/chemputer/pseudo-elements.html for a longer list we may wish to implement later
        public static readonly IDictionary<string, string> DICT_ADDUCT_NICKNAMES =
            new Dictionary<string, string>
            {
                // ReSharper disable LocalizableElement
                {"ACN", "C2H3N"}, // Acetonitrile
                {"DMSO", "C2H6OS"}, // Dimethylsulfoxide
                {"FA", "CH2O2"}, // Formic acid
                {"Hac", "CH3COOH"}, // Acetic acid
                {"TFA", "C2HF3O2"}, // Trifluoroacetic acid
                {"IsoProp", "C3H8O"}, // Isopropanol
                {"MeOH", "CH3OH"}, // CH3OH. methanol
                {"MeOX", "CH3N"}, // Methoxamine 
                {"TMS", "C3H8Si"}, // MSTFA(N-methyl-N-trimethylsilytrifluoroacetamide)
            };

        public static readonly IDictionary<string, string> DICT_ADDUCT_ISOTOPE_NICKNAMES =
            new Dictionary<string, string>
            {
                {"Cl37", BioMassCalc.Cl37},
                {"Br81", BioMassCalc.Br81},
                {"P32", BioMassCalc.P32},
                {"S33", BioMassCalc.S33},
                {"S34", BioMassCalc.S34},
                {"H2", BioMassCalc.H2},
                {"H3", BioMassCalc.H3},
                {"D", BioMassCalc.H2},
                {"T", BioMassCalc.H3},
                {"C13", BioMassCalc.C13},
                {"C14", BioMassCalc.C14},
                {"N15", BioMassCalc.N15},
                {"O17", BioMassCalc.O17},
                {"O18", BioMassCalc.O18},
                {"Cu65", BioMassCalc.Cu65},
                // ReSharper restore LocalizableElement
            };

        // Ion charges seen in XCMS public and ESI-MS-adducts.xls
        public static readonly IDictionary<string, int> DICT_ADDUCT_ION_CHARGES =
            new Dictionary<string, int>
            {
                {BioMassCalc.H, 1},
                {BioMassCalc.K, 1},
                {BioMassCalc.Na, 1},
                {BioMassCalc.Li, 1},
                {BioMassCalc.Br,-1},
                {BioMassCalc.Cl,-1},
                {BioMassCalc.F, -1},
                {@"CH3COO", -1}, // Deprotonated Hac
                {@"HCOO", -1}, // Formate (deprotonated FA)  
                {@"NH4", 1},
                {@"CH3", 1} // Methyl
            };

        // Popular adducts (declared way down here because it has to follow some other statics)
        public static readonly Adduct EMPTY = new Adduct(0, false);
        public static readonly Adduct SINGLY_PROTONATED = new Adduct(1, true); // For use with proteomic molecules where user expects to see "z=1" instead  of "M+H" as the description
        public static readonly Adduct DOUBLY_PROTONATED = new Adduct(2, true);
        public static readonly Adduct TRIPLY_PROTONATED = new Adduct(3, true);
        public static readonly Adduct QUADRUPLY_PROTONATED = new Adduct(4, true);
        public static readonly Adduct QUINTUPLY_PROTONATED = new Adduct(5, true);

        public static readonly Adduct[] COMMON_PROTONATED_ADDUCTS =
        {
            SINGLY_PROTONATED,
            DOUBLY_PROTONATED,
            TRIPLY_PROTONATED,
            QUADRUPLY_PROTONATED,
            QUINTUPLY_PROTONATED
        };

        // Common small molecule adducts
        // ReSharper disable LocalizableElement
        public static readonly Adduct M_PLUS_H = new Adduct("[M+H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS_Na = new Adduct("[M+Na]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS_2H = new Adduct("[M+2H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS_3H = new Adduct("[M+3H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS = new Adduct("[M+]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS_2 = new Adduct("[M+2]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_PLUS_3 = new Adduct("[M+3]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS_H = new Adduct("[M-H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS_2H = new Adduct("[M-2H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS_3H = new Adduct("[M-3H]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS = new Adduct("[M-]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS_2 = new Adduct("[M-2]", ADDUCT_TYPE.non_proteomic);
        public static readonly Adduct M_MINUS_3 = new Adduct("[M-3]", ADDUCT_TYPE.non_proteomic);

        public static readonly Adduct[] COMMON_SMALL_MOL_ADDUCTS =
        {
            M_PLUS_H,
            M_MINUS_H,
            M_PLUS_Na,
            M_PLUS_2H,
            M_PLUS_3H,
            M_PLUS,
            M_PLUS_2,
            M_PLUS_3,
            M_MINUS_2H,
            M_MINUS_3H,
            M_MINUS,
            M_MINUS_2,
            M_MINUS_3
        };

        public static readonly string[] COMMON_CHARGEONLY_ADDUCTS =
        {
            "[M+]",     
            "[M+2]",    
            "[M+3]",    
            "[M-]",     
            "[M-2]",    
            "[M-3]"     
        };

        // All the adducts from http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator
        // And a few more from XCMS public
        public static readonly string[] DEFACTO_STANDARD_ADDUCTS =
        {
            "[M+3H]",       
            "[M+2H+Na]",    
            "[M+H+2Na]",    
            "[M+3Na]",      
            "[M+2H]",       
            "[M+H+NH4]",    
            "[M+H+Na]",     
            "[M+H+K]",      
            "[M+ACN+2H]",   
            "[M+2Na]",      
            "[M+2ACN+2H]",  
            "[M+3ACN+2H]",  
            "[M+H]",        
            "[M+NH4]",      
            "[M+Na]",       
            "[M+CH3OH+H]",  
            "[M+K]",        
            "[M+ACN+H]",    
            "[M+2Na-H]",    
            "[M+IsoProp+H]",
            "[M+ACN+Na]",   
            "[M+2K-H]",     
            "[M+DMSO+H]",   
            "[M+2ACN+H]",   
            "[M+IsoProp+Na+H]",
            "[2M+H]",          
            "[2M+NH4]",        
            "[2M+Na]",         
            "[2M+K]",          
            "[2M+ACN+H]",      
            "[2M+ACN+Na]",     
            "[M-3H]",       
            "[M-2H]",       
            "[M-H2O-H]",    
            "[M-H]",        
            "[M+Na-2H]",    
            "[M+Cl]",       
            "[M+K-2H]",     
            "[M+FA-H]",     
            "[M+HCOO]", // Formate (synonym for deprotonated FA)  
            "[M+Hac-H]",    
            "[M+CH3COO]", // Synonym for deprotonated Hac
            "[M+Br]",       
            "[M+TFA-H]",    
            "[2M-H]",       
            "[2M+FA-H]",    
            "[2M+Hac-H]",   
            "[3M-H]"        
        };

        /// <summary>
        /// Generate a tooltip string that look something like this:
        ///
        ///   Formula may contain an adduct description (e.g. "C47H51NO14[M+IsoProp+H]").
        ///
        ///   Multipliers (e.g. "[2M+K]") and labels (e.g. "[M2Cl37+H]") are supported.
        ///   
        ///   Recognized adduct components include normal chemical symbols and:
        ///   ACN (C2H3N)
        ///   DMSO (C2H6OS)
        ///   FA (CH2O2)
        ///   Hac (CH3COOH)
        ///   TFA (C2HF3O2)
        ///   IsoProp (C3H8O)
        ///   MeOH (CH3OH)
        ///   Cl37 (Cl')
        ///   Br81 (Br')
        ///   C13 (C')
        ///   N15 (N')
        ///   O17 (O")
        ///   O18 (O').
        ///   
        ///   Charge states are inferred from the presence of these adduct components:
        ///   H (+1)
        ///   K (+1)
        ///   Na (+1)
        ///   Li (+1)
        ///   Br (-1)
        ///   Cl (-1)
        ///   F (-1)
        ///   CH3COO (-1)
        ///   NH4 (+1)
        /// 
        /// </summary>
        public static string Tips
        {
            get
            {
                var components = DICT_ADDUCT_NICKNAMES.Aggregate<KeyValuePair<string, string>, string>(null, (current, c) => current + (String.IsNullOrEmpty(current) ? "\r\n" : ", ") + String.Format("{0} ({1})", c.Key, c.Value));
                components += DICT_ADDUCT_ISOTOPE_NICKNAMES.Aggregate<KeyValuePair<string, string>, string>(null, (current, c) => current + ", " + String.Format("{0} ({1})", c.Key, c.Value));
                var chargers = DICT_ADDUCT_ION_CHARGES.Aggregate<KeyValuePair<string, int>, string>(null, (current, c) => current + (String.IsNullOrEmpty(current) ? "\r\n" : ", ") + String.Format("{0} ({1:+#;-#;+0})", c.Key, c.Value));
                return string.Format(UtilResources.IonInfo_AdductTips_, components, chargers);
            }
        }
        // ReSharper restore LocalizableElement

        // Convert an ordered list of adducts to a list of their unique absolute 
        // charge values, ordered by first appearance 
        public static IList<int> OrderedAbsoluteChargeValues(IEnumerable<Adduct> adducts)
        {
            var charges = new List<int>();
            foreach (var charge in adducts.Select(a => Math.Abs(a.AdductCharge)))
            {
                if (!charges.Contains(charge)) // We're looking at abs charge, not adduct per se
                {
                    charges.Add(charge);
                }
            }
            return charges;
        }

        public ParsedMolecule ApplyToMolecule(Molecule molecule)
        {
            return ApplyToMolecule(ParsedMolecule.Create(molecule));
        }

        /// <summary>
        /// Handle the "2" and "4Cl37" in "[2M4Cl37+H]", and add the H
        /// </summary>
        public ParsedMolecule ApplyToMolecule(ParsedMolecule molecule)
        {
            if (HasIsotopeLabels && molecule.HasIsotopes())
            {
                // Molecule is already labeled, use the unlabeled version of this adduct 
                return Unlabeled.ApplyToMolecule(molecule);
            }

            var resultDict = new Dictionary<string, int>(molecule.Molecule);

            // Deal with any mass multiplier (the 2 in "[2M+Na]")
            if (MassMultiplier != 1)
            {
                foreach (var element in resultDict.Keys.ToArray())
                {
                    resultDict[element] *= MassMultiplier;
                }
            }

            // Add in the "Na" of [M+Na] (or remove the 4H in [M-4H])
            foreach (var pair in Composition)
            {
                if (resultDict.TryGetValue(pair.Key, out var count))
                {
                    count += pair.Value;
                    if (count == 0)
                    {
                        resultDict.Remove(pair.Key);
                    }
                    else
                    {
                        resultDict[pair.Key] = count;
                    }
                }
                else if (pair.Value != 0)
                {
                    resultDict.Add(pair.Key, pair.Value);
                    count = pair.Value;
                }
                if (!molecule.IsMassOnly &&
                    count < 0 && !Equals(pair.Key, BioMassCalc.H)) // Treat H loss as a general proton loss
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Adduct_ApplyToMolecule_Adduct___0___calls_for_removing_more__1__atoms_than_are_found_in_the_molecule__2_,
                            this, pair.Key, molecule.ToString()));
                }
            }

            return ApplyIsotopeValues(molecule, MassMultiplier, resultDict);
        }

        public ParsedMolecule ApplyToMolecule(MoleculeMassOffset molecule) => ApplyToMolecule(ParsedMolecule.Create(molecule));


        private ParsedMolecule ApplyIsotopeValues(ParsedMolecule molecule, int massMultiplier, IDictionary<string, int> resultDict)
        {
            TypedMass massModMono;
            TypedMass massModAvg;

            if (!HasIsotopeLabels)
            {
                // Caller has already multiplied chemical formula, handle mass offsets here
                massModMono = molecule.MonoMassOffset * massMultiplier;
                massModAvg = molecule.AverageMassOffset * massMultiplier;
            }
            else if (molecule.IsMassOnly)
            {
                // Just add the incremental mass of the declared isotopes
                massModMono = (molecule.MonoMassOffset + IsotopesIncrementalMonoMass) * massMultiplier;
                massModAvg = (molecule.AverageMassOffset + IsotopesIncrementalAverageMass) * massMultiplier;
            }
            else
            {
                // Deal with mass-only labeling (the "(-1.23)" in [2M(-1.23)+Na]")
                // N.B. in "[2M2.45+2H]" we'd add 4.90 rather than 2.45, that is, "[+2.45]" with a count of 2
                // Same idea as handling "4Cl37" in the example below
                massModMono =
                    (molecule.MonoMassOffset + IsotopeLabels.MonoMassOffset) * massMultiplier;
                massModAvg =
                    (molecule.AverageMassOffset + IsotopeLabels.AverageMassOffset) * massMultiplier;
                massModMono = massModMono.ChangeIsHeavy(true);
                massModAvg = massModAvg.ChangeIsHeavy(true);

                // Deal with labeling (the "4Cl37" in "[M4Cl37+2H]")
                // N.B. in "[2M4Cl37+2H]" we'd replace 8 Cl rather than 4
                foreach (var isotopeSymbolAndCount in IsotopeLabels.Molecule)
                {
                    var unlabeledSymbol = BioMassCalc.DICT_HEAVYSYMBOL_TO_MONOSYMBOL[isotopeSymbolAndCount.Key];
                    resultDict.TryGetValue(unlabeledSymbol, out var unlabeledCount);
                    // If label is "2Cl37" and molecule is CH4Cl5 then result is CH4Cl3Cl'2
                    var isotopeCount = massMultiplier * isotopeSymbolAndCount.Value;
                    unlabeledCount -= isotopeCount;
                    if (unlabeledCount > 0)
                    {
                        resultDict[unlabeledSymbol] = unlabeledCount; // Number of remaining non-label atoms
                    }
                    else if (unlabeledCount == 0)
                    {
                        resultDict.Remove(unlabeledSymbol); // Number of remaining non-label atoms)
                    }
                    else // Can't remove that which is not there
                    {
                        throw new InvalidDataException(
                            string.Format(
                                UtilResources
                                    .Adduct_ApplyToMolecule_Adduct___0___calls_for_labeling_more__1__atoms_than_are_found_in_the_molecule__2_,
                                this, unlabeledSymbol, molecule.ToString()));
                    }

                    var isotopeSymbol = isotopeSymbolAndCount.Key;
                    if (resultDict.TryGetValue(isotopeSymbol, out var exist))
                    {
                        exist += isotopeCount;
                        if (exist == 0)
                        {
                            resultDict.Remove(isotopeSymbol);
                        }
                        else
                        {
                            resultDict[isotopeSymbol] = exist;
                        }
                    }
                    else if (isotopeCount != 0)
                    {
                        resultDict.Add(isotopeSymbol, isotopeCount);
                    }
                }
            }

            return molecule.Change(Molecule.FromDict(resultDict), massModMono, massModAvg);
        }

        public ParsedMolecule ApplyToFormula(string formula)
        {
            return ApplyToMolecule(ParsedMolecule.Create(formula));
        }

        public ParsedMolecule ApplyIsotopeLabelsToFormula(string formula)
        {
            var molecule = ParsedMolecule.Create(formula);
            return ApplyIsotopeLabelsToMolecule(molecule);
        }

        // Apply just the isotope effect, e.g "2Cl37" in 2MCl37+Na
        public ParsedMolecule ApplyIsotopeLabelsToMolecule(ParsedMolecule molecule)
        {
            if (!HasIsotopeLabels)
            {
                return molecule;
            }
            var resultDict = new Dictionary<string, int>(molecule.Molecule);

            return ApplyIsotopeValues(molecule, 1, resultDict);
        }

        public double ApplyIsotopeLabelsToMass(TypedMass mass)
        {
            // Account for the added mass of any labels declared in the adduct, e.g. for [2M4Cl37+H] add 2x4x the difference in mass between CL37 and Cl
            if (mass.IsHeavy())
            {
                return mass; // Mass already has isotope masses factored in
            }
            if (!HasIsotopeLabels)
            {
                return mass;
            }
            return (mass.IsMonoIsotopic() ? IsotopesIncrementalMonoMass : IsotopesIncrementalAverageMass) + mass; 
        }

        /// <summary>
        /// Returns the effect of the adduct on the input mass,
        /// including the mass multiplier and any isotope labels if the mass isn't marked heavy (ie already has labels accounted for)
        /// </summary>
        public TypedMass ApplyToMass(TypedMass neutralMass)
        {
            var adductMass = neutralMass.IsHeavy()
                ? neutralMass // Mass already takes isotopes into account
                : neutralMass.MassType.IsAverage()
                    ? IsotopesIncrementalAverageMass + AverageMassAdduct
                    : IsotopesIncrementalMonoMass + MonoMassAdduct; // Mass of the Na and 2*3(mass C' - mass C) in [2M3C13+Na]
            Assume.IsTrue(adductMass.IsHeavy() == IsotopesIncrementalAverageMass.IsHeavy());
            return adductMass + neutralMass * MassMultiplier;
        }

        /// <summary>
        /// Get the mz when the adduct formula (including any mass multiplier and isotope labels) is applied to a neutral mass
        /// </summary>
        /// <param name="neutralMass">mass of a neutral molecule, and its mass tyoe </param>
        public double MzFromNeutralMass(TypedMass neutralMass)
        {
            return MzFromNeutralMass(neutralMass.Value, neutralMass.MassType);
        }

        /// <summary>
        /// Get the mz when the adduct formula (including any mass multiplier) is applied to a neutral mass
        /// </summary>
        /// <param name="neutralMass">mass of a neutral molecule</param>
        /// <param name="t">determines use of Average mass or Mono mass</param>
        public double MzFromNeutralMass(double neutralMass, MassType t)
        {
            if (neutralMass != 0 && t.IsMassH())
            {
                Assume.IsTrue(IsProtonated); // Expect massH to be a peptide thing only
                var iMass = t.IsHeavy() ? 
                    0.0 :  // Don't reapply isotope label mass
                    (t.IsAverage() ? IsotopesIncrementalAverageMass : IsotopesIncrementalMonoMass); // For example, mass of the 2*3*(cl37-Cl)in 2M3Cl37+2H
                return (iMass + neutralMass * MassMultiplier + (AdductCharge-1) * BioMassCalc.MassProton) / Math.Abs(AdductCharge);
            }
            // Treat protonation as a special case, so the numbers agree with how we traditionally deal with peptide charges
            if (IsProtonated)
            {
                var isotopeIncrementalMass = t.IsHeavy() ? 
                    0.0 : // Don't reapply isotope label mass
                    t.IsAverage() ? IsotopesIncrementalAverageMass : IsotopesIncrementalMonoMass; // For example, mass of the 2*3*(cl37-Cl)in 2M3Cl37+2H
                return (isotopeIncrementalMass + neutralMass * MassMultiplier + AdductCharge * BioMassCalc.MassProton) / Math.Abs(AdductCharge);
            }
            double adductMass = t.IsHeavy() ? // Don't reapply isotope label mass
                (t.IsAverage() ? AverageMassAdduct : MonoMassAdduct) : // For example, mass of the 2H in 2M3Cl37+2H
                (t.IsAverage() ? AverageMassAdduct + IsotopesIncrementalAverageMass : MonoMassAdduct + IsotopesIncrementalMonoMass); // For example, mass of the 2H and 2*3*(cl37-Cl)in 2M3Cl37+2H
            return (neutralMass * MassMultiplier + adductMass - AdductCharge * BioMassCalc.MassElectron) / Math.Abs(AdductCharge);  
        }

        /// <summary>
        /// Work back from mz to mass of molecule without adduct (but with isotopes if any), accounting for electron loss or gain, 
        /// and adduct multiplier 
        /// </summary>
        /// <param name="mz">mz of ion (molecule+adduct)</param>
        /// <param name="t">determines use of Average mass or Mono mass</param>
        /// <returns></returns>
        public TypedMass MassFromMz(double mz, MassType t)
        {
            if (IsProtonated)
            {
                // Treat this as a special case, so the numbers agree with how we deal with peptide charges
                return new TypedMass((mz * Math.Abs(AdductCharge) - AdductCharge * BioMassCalc.MassProton) / MassMultiplier, t);
            }
            var adductMass = t.IsAverage() ? AverageMassAdduct : MonoMassAdduct;
            return new TypedMass((mz * Math.Abs(AdductCharge) + AdductCharge * BioMassCalc.MassElectron - adductMass) / MassMultiplier, t);
        }

        private void InitializeAsCharge(int charge, ADDUCT_TYPE mode)
        {
            Description = null;
            AdductCharge = charge;
            MassMultiplier = 1;
            if ((mode != ADDUCT_TYPE.charge_only) && (AdductCharge != 0))
            {
                var absCharge = Math.Abs(AdductCharge);
                Composition = Molecule.Parse($@"{((AdductCharge < 0) ? @"-" : string.Empty)}H{((absCharge==0)?string.Empty:absCharge.ToString(CultureInfo.InvariantCulture))}");
            }
            else
            {
                Composition = Molecule.Empty;
            }
            IsotopeLabels = ParsedMolecule.EMPTY;
            InitializeMasses();
        }

        private void InitializeMasses()
        {
            AverageMassAdduct = BioMassCalc.AVERAGE.CalculateMass(Composition); // The average mass of the +2Na in [2M4Cl37+2Na]
            MonoMassAdduct = BioMassCalc.MONOISOTOPIC.CalculateMass(Composition); // The mono mass of the +2Na in [2M4Cl37+2Na]
            double isotopeLabelsAverageMassOffset =  IsotopeLabels.AverageMassOffset;
            double isotopeLabelsMonoMassOffset = IsotopeLabels.MonoMassOffset;
            foreach (var isotope in IsotopeLabels.Molecule)
            {
                // Account for the added mass of any labels declared in the adduct, e.g. for [2M4Cl37+H] add 2x4x the difference in mass between CL37 and Cl
                var unlabled = BioMassCalc.GetMonoisotopicSymbol(isotope.Key);
                var label = isotope.Key;
                var labelCount = isotope.Value;
                isotopeLabelsAverageMassOffset += labelCount*(BioMassCalc.AVERAGE.GetMass(label) - BioMassCalc.AVERAGE.GetMass(unlabled));
                isotopeLabelsMonoMassOffset += labelCount*(BioMassCalc.MONOISOTOPIC.GetMass(label) - BioMassCalc.MONOISOTOPIC.GetMass(unlabled));
            }
            IsotopesIncrementalAverageMass = new TypedMass(MassMultiplier * isotopeLabelsAverageMassOffset,
                isotopeLabelsAverageMassOffset == 0 ? MassType.Average : MassType.AverageHeavy);
            IsotopesIncrementalMonoMass = new TypedMass(MassMultiplier * isotopeLabelsMonoMassOffset,
                isotopeLabelsMonoMassOffset == 0 ? MassType.Monoisotopic : MassType.MonoisotopicHeavy);
            Unlabeled = ChangeIsotopeLabels(string.Empty); // Useful for dealing with labels and mass-only small molecule declarations
            IsProtonated = Composition.Any() && Composition.All(pair => // All H, H', H", D or T
                BioMassCalc.GetMonoisotopicSymbol(pair.Key) == BioMassCalc.H);
            IsProteomic = IsProtonated && string.IsNullOrEmpty(Description); 
        }

        // Used for checking that different descriptions (ie "[M+H]" vs "[M+H]+") have same ion effect
        public bool SameEffect(Adduct obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (this.Equals(obj)) return true;

            if (!Equals(obj.AdductCharge, AdductCharge) || 
                !Equals(obj.Composition, Composition) || 
                !Equals(obj.MassMultiplier, MassMultiplier) ||
                !Equals(obj.IsotopeLabels, IsotopeLabels))
                return false;
            return true;
        }

        // We want comparisons to be on the same order as comparing ints, as when we used to just use integer charge instead of proper adducts
        private void SetHashCode()
        {
            _hashCode = (Description != null ? Description.GetHashCode() : 0);
            _hashCode = (_hashCode * 397) ^ AdductCharge.GetHashCode();
            _hashCode = (_hashCode * 397) ^ IsotopeLabels.GetHashCode();
            _hashCode = (_hashCode * 397) ^ Composition.GetHashCode();
        }


        #region object overrides

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public bool Equals(Adduct obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (_hashCode != obj._hashCode) return false;
            var equal = CompareTo(obj) == 0;
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Adduct)) return false;
            return Equals((Adduct)obj);
        }

        public static int Compare(Adduct left, Adduct right)
        {
            if (left == null)
            {
                return right == null ? 0 : -1;
            }
            return left.CompareTo(right);
        }

        // Lots of operator overrides, so we don't have to change masses of Skyline code
        // where we formerly used charge as a proxy for protonation

        public static bool operator ==(Adduct left, Adduct right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Adduct left, Adduct right)
        {
            return !Equals(left, right);
        }

        public static bool operator <(Adduct left, Adduct right)
        {
            return Compare(left, right) < 0;
        }

        public static bool operator <=(Adduct left, Adduct right)
        {
            return Compare(left, right) <= 0;
        }

        public static bool operator >=(Adduct left, Adduct right)
        {
            return Compare(left, right) >= 0;
        }

        public static bool operator >(Adduct left, Adduct right)
        {
            return Compare(left, right) > 0;
        }

        public int CompareTo(object obj)
        {
            return ReferenceEquals(null, obj) ? 1 : CompareTo(obj as Adduct);
        }

        // Sort by polarity (- then +), then by abs(charge), then by resulting m/z when applied to an arbitrary mass, then by text description
        public int CompareTo(Adduct that)
        {
            if (ReferenceEquals(null, that))
            {
                return 1;
            }
            if (ReferenceEquals(this, that))
            {
                return 0;
            }
            // Negatives then Positives
            var comp = Math.Sign(AdductCharge).CompareTo(Math.Sign(that.AdductCharge));
            if (comp != 0)
            {
                return comp;
            }
            // Now charge low to high e.g -1,-2,-3,1,2,3
            comp = Math.Abs(AdductCharge).CompareTo(Math.Abs(that.AdductCharge));
            if (comp != 0)
            {
                return comp;
            }
            // Now by mz including the mass multiplier and any isotope labels
            const double mass = 2000;
            comp = MzFromNeutralMass(mass, MassType.Monoisotopic).CompareTo(that.MzFromNeutralMass(mass, MassType.Monoisotopic));
            if (comp != 0)
            {
                return comp;
            }
            // Last ditch, an alpha sort on the whole thing as text
            comp = string.Compare(Description, that.Description, StringComparison.Ordinal);
            if (comp != 0)
            {
                return comp;
            }
            return 0;
        }

        // Return the full "[M+H]" style declaration even if marked as proteomic (that is, even if we aren't carrying a text description)
        public string AsFormula()
        {
            if (!string.IsNullOrEmpty(Description))
            {
                return Description;
            }
            if (IsChargeOnly)
            {
                return string.Format(@"[M{0:+#;-#}]", AdductCharge); 
            }
            Assume.IsFalse(HasIsotopeLabels); // For peptides we don't normally handle isotopes in the adduct
            return Composition.Aggregate(@"[M", (current, atom) => current + (atom.Value==1 ? @"+" : atom.Value==-1 ? @"-" : string.Format(@"{0:+#;-#;#}", atom.Value)) + atom.Key)+@"]";
        }

        // For protonation, return something like "+2" or "-3", for others the full "[M+Na]" style declaration
        public string AsFormulaOrSignedInt()
        {
            return Description ?? string.Format(@"{0:+#;-#;#}",AdductCharge); // Formatter for pos;neg;zero
        }

        // For protonation, return something like "2" or "-3", for others the full "[M+Na]" style declaration
        public string AsFormulaOrInt(CultureInfo culture = null)
        {
            return Description ?? AdductCharge.ToString(culture ?? CultureInfo.InvariantCulture); 
        }

        // For protonation, return something like "++" or "---", for others the full "[M+Na]" style declaration
        private const string plusses = "++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++";
        private const string minuses = "------------------------------------------------------------------------------------------";

        public string AsFormulaOrSigns()
        {
            if (!String.IsNullOrEmpty(Description))
            {
                // For charge-only adducts, fall through to show "+++" instead of showing "[M+3]"
                if (!IsChargeOnly)
                    return Description;
            }
            return (AdductCharge > 0) ? plusses.Substring(0, AdductCharge) : minuses.Substring(0, -AdductCharge);
        }

        public string ToString(CultureInfo culture)
        {
            return AsFormulaOrInt(culture);            
        }

        // For protonation, return something like "2" or "-3", for others the full "[M+Na]" style declaration
        public override string ToString()
        {
            return AsFormulaOrInt(CultureInfo.InvariantCulture);
        }

        #endregion

        public string AuditLogText
        {
            get { return ToString(); }
        }

        public bool IsName
        {
            get { return true; }
        }

        public bool IsValidProductAdduct(Adduct precursorAdduct, TransitionLosses losses)
        {
            int precursorCharge = precursorAdduct.AdductCharge;
            if (losses != null)
            {
                precursorCharge -= losses.TotalCharge;
            }

            return Math.Abs(AdductCharge) <= Math.Abs(precursorCharge);
        }

    }
}
