/*
 * Original author: Brian Pratt <bspratt at proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class MoleculeAccessionNumbers : IComparable<MoleculeAccessionNumbers>, IEquatable<MoleculeAccessionNumbers>
    {
        /// <summary>
        /// For molecule IDs such as InChiKey, CAS, etc to match blib (see pwiz_tools\BiblioSpec\src\SmallMolMetadata.h)
        /// </summary>

        public static MoleculeAccessionNumbers EMPTY = new MoleculeAccessionNumbers(string.Empty);
        public static bool IsNullOrEmpty(MoleculeAccessionNumbers item) { return item == null || Equals(item, EMPTY); }
        public ImmutableSortedList<string, string> AccessionNumbers { get; private set; } // Accession type/value pairs, sorted by PREFERRED_ACCESSION_TYPE_ORDER
        public string PrimaryAccessionType { get { return AccessionNumbers == null ? null : AccessionNumbers.Keys.FirstOrDefault(); } } // Type of key, if any, in first order of PREFERRED_ACCESSION_TYPE_ORDER
        public string PrimaryAccessionValue { get { return AccessionNumbers == null ? null : AccessionNumbers.Values.FirstOrDefault(); } } // Value of key, if any, in first order of PREFERRED_ACCESSION_TYPE_ORDER

        // Familiar molecule ID formats, and our order of preference as primary key
        public static readonly string[] PREFERRED_ACCESSION_TYPE_ORDER = { TagInChiKey, TagCAS, TagHMDB, TagInChI, TagSMILES, TagKEGG };
        public const string TagInChiKey = "InChiKey";
        public const string TagCAS = "CAS";
        public const string TagHMDB = "HMDB";
        public const string TagInChI = "InChI";
        public const string TagSMILES = "SMILES";
        public const string TagKEGG = "KEGG";

        public static MoleculeAccessionNumbers FromString(string tsv)
        {
            if (string.IsNullOrEmpty(tsv))
            {
                return EMPTY;
            }
            return new MoleculeAccessionNumbers(tsv);
        }

        private static readonly SortByPreferredAccessionType ACCESSION_TYPE_SORTER = new SortByPreferredAccessionType();

        public MoleculeAccessionNumbers(IDictionary<string, string> keys)
        {
            var nonEmptyKeys = keys==null ? new KeyValuePair<string, string>[]{} : keys.Where(kvp => !string.IsNullOrEmpty(kvp.Value)).ToArray();
            AccessionNumbers = ImmutableSortedList<string, string>.FromValues(nonEmptyKeys, ACCESSION_TYPE_SORTER); 
        }

        /// <summary>
        /// Pick apart a string like "CAS:58-08-2\tinchi:1S/C8H10N4O2/c1-10-4-9-6-5(10)7(13)12(3)8(14)11(6)2/h4H,1-3H3\tmykey:a:b:c:d"
        /// </summary>
        public static Dictionary<string, string> FormatAccessionNumbers(string keysTSV, string inChiKey = null)
        {
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Treat "cas" and "CAS" as identical lookups
            if (!string.IsNullOrEmpty(keysTSV) || !string.IsNullOrEmpty(inChiKey))
            {
                if (!string.IsNullOrEmpty(keysTSV))
                {
                    foreach (var kvp in keysTSV.Split(TextUtil.SEPARATOR_TSV))
                    {
                        var pair = kvp.Split(':');
                        if (pair.Length > 1)
                        {
                            var key = pair[0].Trim();
                            var value = string.Join(@":", pair.Skip(1)).Trim(); // In case value contains semicolons
                            if (!string.IsNullOrEmpty(value))
                            {
                                keys.Add(key, value);
                            }
                        }
                    }
                }
            }

            return keys;
        }

        public MoleculeAccessionNumbers(string keysTSV, string inChiKey = null)
        {
            var keys = FormatAccessionNumbers(keysTSV, inChiKey);
            if (!string.IsNullOrEmpty(inChiKey))
            {
                if (keys.ContainsKey(TagInChiKey))
                    Assume.AreEqual(inChiKey, keys[TagInChiKey]);
                else
                    keys.Add(TagInChiKey, inChiKey);
            }
            AccessionNumbers = ImmutableSortedList<string, string>.FromValues(keys, ACCESSION_TYPE_SORTER); 
        }

        public bool IsEmpty {
            get
            {
                return AccessionNumbers == null || AccessionNumbers.Count == 0;
            }
        }

        class SortByPreferredAccessionType : IComparer<string>
        {

            public int Compare(string left, string right)
            {
                // Treat "cas" and "CAS" as identical lookups
                var orderleft = PREFERRED_ACCESSION_TYPE_ORDER.IndexOf(s => StringComparer.OrdinalIgnoreCase.Compare(left, s) == 0);
                var orderright = PREFERRED_ACCESSION_TYPE_ORDER.IndexOf(s => StringComparer.OrdinalIgnoreCase.Compare(right, s) == 0);
                if (orderleft >= 0)
                {
                    return orderright >= 0 ? orderleft - orderright : -1;
                }
                return (orderright >= 0) ? 1 : StringComparer.OrdinalIgnoreCase.Compare(left, right);
            }

        }

        public string GetInChiKey()
        {
            string inchikey;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagInChiKey, out inchikey) ? inchikey : null;
        }
        public string GetInChI()
        {
            string inchikey;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagInChI, out inchikey) ? inchikey : null;
        }
        public string GetCAS()
        {
            string cas;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagCAS, out cas) ? cas : null;
        }

        public string GetHMDB()
        {
            string hmdb;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagHMDB, out hmdb) ? hmdb : null;
        }

        public string GetSMILES()
        {
            string smiles;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagSMILES, out smiles) ? smiles : null;
        }

        public string GetKEGG()
        {
            string kegg;
            return AccessionNumbers != null && AccessionNumbers.TryGetValue(TagKEGG, out kegg) ? kegg : null;
        }

        public string GetNonInChiKeys()
        {
            return AccessionNumbers != null && AccessionNumbers.Any() ?
                AccessionNumbers.Where(k => k.Key != TagInChiKey).Select(kvp => string.Format(@"{0}:{1}", kvp.Key, kvp.Value)).ToDsvLine(TextUtil.SEPARATOR_TSV) :
                null;
        }

        public string[] GetAllKeys()
        {
            return ToString().Split(TextUtil.SEPARATOR_TSV);
        }

        public int CompareTo(MoleculeAccessionNumbers other)
        {
            if (other == null)
                return 1;
            if (other.AccessionNumbers == null)
                return AccessionNumbers == null ? 0 : 1;
            if (AccessionNumbers == null)
                return other.AccessionNumbers == null ? 0 : -1;
            foreach (var key in AccessionNumbers)
            {
                string value;
                if (!other.AccessionNumbers.TryGetValue(key.Key, out value))
                {
                    return 1;
                }
                var result = string.Compare(key.Value, value, StringComparison.InvariantCultureIgnoreCase);
                if (result != 0)
                    return result;
            }
            return AccessionNumbers.Count.CompareTo(other.AccessionNumbers.Count);
        }

        // Return true iff this and other have some accessions in common and others in conflict
        public bool InconsistentWith(MoleculeAccessionNumbers other)
        {
            if (IsEmpty && IsNullOrEmpty(other))
            {
                return false;
            }
            return AccessionNumbers.Any(kvp => 
                       other.AccessionNumbers.TryGetValue(kvp.Key, out var value) && Equals(value, kvp.Value)) &&
                   AccessionNumbers.Any(kvp =>
                       other.AccessionNumbers.TryGetValue(kvp.Key, out var value) && !Equals(value, kvp.Value));
        }

        // Return the values in common of two sets of accession numbers - return null if no intersection, or any conflict
        public MoleculeAccessionNumbers Intersection(MoleculeAccessionNumbers other)
        {
            if (IsEmpty || IsNullOrEmpty(other))
            {
                return null;
            }

            if (Equals(other))
            {
                return this;
            }

            var result = new Dictionary<string, string>();
            foreach (var keyValuePair in AccessionNumbers)
            {
                if (other.AccessionNumbers.TryGetValue(keyValuePair.Key, out var value))
                {
                    var compare = string.Compare(keyValuePair.Value, value, StringComparison.InvariantCultureIgnoreCase);
                    if (compare != 0)
                    {
                        return null; // Conflict, can't intersect
                    }
                    result.Add(keyValuePair.Key, keyValuePair.Value); // Same value for this and other
                }
            }

            return result.Count == 0 ? null : new MoleculeAccessionNumbers(result);
        }

        // Merge two sets of accession numbers if they don't have conflicts - return null if they do
        public MoleculeAccessionNumbers Union(MoleculeAccessionNumbers other)
        {
            if (IsNullOrEmpty(other))
            {
                return this;
            }
            if (IsEmpty)
            {
                return other;
            }
            if (Equals(other))
            {
                return this;
            }

            var result = new Dictionary<string, string>();
            foreach (var keyValuePair in AccessionNumbers)
            {
                if (other.AccessionNumbers.TryGetValue(keyValuePair.Key, out var value))
                {
                    var compare = string.Compare(keyValuePair.Value, value, StringComparison.InvariantCultureIgnoreCase);
                    if (compare != 0)
                    {
                        return null; // Conflict, can't merge
                    }
                }
                result.Add(keyValuePair.Key, keyValuePair.Value); // No conflict
            }

            foreach (var keyValuePair in other.AccessionNumbers)
            {
                if (!result.ContainsKey(keyValuePair.Key))
                {
                    result.Add(keyValuePair.Key, keyValuePair.Value); // Not in the other
                }
            }

            return new MoleculeAccessionNumbers(result);
        }

        public bool Equals(MoleculeAccessionNumbers other)
        {
            return CompareTo(other) == 0;
        }



        public override string ToString()
        {
            return ToString(TextUtil.SEPARATOR_TSV_STR);
        }

        public string ToString(string separator)
        {
            var result = string.Empty;
            if (AccessionNumbers != null && AccessionNumbers.Any())
            {
                foreach (var key in PREFERRED_ACCESSION_TYPE_ORDER)
                {
                    string value;
                    if (AccessionNumbers.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                    {
                        result += string.Format(@"{0}{1}:{2}",
                            string.IsNullOrEmpty(result) ? string.Empty : separator, key, value);
                    }
                }
            }
            return result;
        }

        public string EscapeTabsForXML(string tsv)
        {
            if (string.IsNullOrEmpty(tsv))
                return tsv;

            // Replace tab with something that XML parsers won't mess with
            var newsep = @"$";
            while (tsv.Contains(newsep))
            {
                newsep += @"_"; // Grow it until it's unique
            }
            // Encode the TSV, declaring the separator 
            return string.Format(@"#{0}#{1}", newsep, tsv.Replace(TextUtil.SEPARATOR_TSV_STR, newsep));
        }

        public static string UnescapeTabsForXML(string val)
        {
            if (string.IsNullOrEmpty(val))
                return val;
            // First thing in string will be the seperator, bounded by # on either end
            var sep = val.Split('#')[1];
            return val.Substring(sep.Length + 2).Replace(sep, TextUtil.SEPARATOR_TSV_STR);
        }

        public string ToSerializableString()
        {
            // Replace tab with something that XML parsers won't mess with
            return EscapeTabsForXML(ToString());
        }
        public static MoleculeAccessionNumbers FromSerializableString(string val)
        {
            return FromString(UnescapeTabsForXML(val));
        }

        public override int GetHashCode()
        {
             return (AccessionNumbers != null && AccessionNumbers.Any()) ? AccessionNumbers.GetHashCode() : 0;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MoleculeAccessionNumbers) obj);
        }
    }

    public class CustomMolecule : IValidating, IComparable<CustomMolecule>
    {
        private string _formula;
        public const double MAX_MASS = 160000;
        public const double MIN_MASS = MeasuredIon.MIN_REPORTER_MASS;

        /// <summary>
        /// A simple object used to represent any molecule
        /// </summary>
        /// <param name="formula">The molecular formula of the molecule, possibly including an adduct description if subclassed as a CustomIon</param>
        /// <param name="monoisotopicMass">The monoisotopic mass of the molecule(can be calculated from formula)</param>
        /// <param name="averageMass">The average mass of the molecule (can be calculated by the formula)</param>
        /// <param name="name">The arbitrary name given to this molecule</param>
        /// <param name="moleculeAccessionNumbers">Provides InChiKey, CAS number etc</param>
        protected CustomMolecule(string formula, double? monoisotopicMass, double? averageMass, string name, MoleculeAccessionNumbers moleculeAccessionNumbers = null)
            : this(formula, new TypedMass(monoisotopicMass ?? averageMass ?? 0, MassType.Monoisotopic),
                            new TypedMass(averageMass ?? monoisotopicMass ?? 0, MassType.Average), name, moleculeAccessionNumbers)
        {
        }

        public CustomMolecule(string formula, string name = null, MoleculeAccessionNumbers moleculeAccessionNumbers = null)
            : this(formula, null, null, name, moleculeAccessionNumbers)
        {
        }

        public CustomMolecule(TypedMass monoisotopicMass, TypedMass averageMass, string name = null, MoleculeAccessionNumbers moleculeAccessionNumbers = null)
            : this(null, monoisotopicMass, averageMass, name, moleculeAccessionNumbers)
        {
        }

        public static CustomMolecule FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes libraryAttributes)
        {
            Assume.IsFalse(libraryAttributes.IsEmpty);
            SmallMoleculeLibraryAttributes.ParseMolecularFormulaOrMassesString(libraryAttributes.ChemicalFormulaOrMassesString, out var formula, out var monoMass, out var averageMass);
            return new CustomMolecule(formula, monoMass, averageMass, libraryAttributes.MoleculeName, libraryAttributes.CreateMoleculeID());
        }

        public CustomMolecule(string formula, TypedMass monoisotopicMass, TypedMass averageMass, string name, MoleculeAccessionNumbers moleculeAccessionNumbers)
        {
            Formula = formula;
            MonoisotopicMass = monoisotopicMass;
            AverageMass = averageMass;
            Name = name ?? string.Empty;
            AccessionNumbers = moleculeAccessionNumbers ?? MoleculeAccessionNumbers.EMPTY;
            Validate();
        }

        public static CustomMolecule EMPTY = new CustomMolecule
        {
            Formula = string.Empty,
            MonoisotopicMass =  TypedMass.ZERO_MONO_MASSH,
            AverageMass = TypedMass.ZERO_AVERAGE_MASSNEUTRAL,
            Name = string.Empty
        };

        public static bool IsNullOrEmpty(CustomMolecule mol)
        {
            return mol == null || mol.IsEmpty;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected CustomMolecule()
        {
            AccessionNumbers = MoleculeAccessionNumbers.EMPTY;
        }

        /// <summary>
        /// For matching heavy/light pairs in small molecule documents
        /// </summary>
        public string PrimaryEquivalenceKey
        {
            get
            {
                return AccessionNumbers.PrimaryAccessionValue ?? // InChiKey, or CAS, etc
                       Name.Replace(TextUtil.SEPARATOR_TSV_STR, @" "); // Tab is a reserved char in our lib cache scheme
            }
        }
        public string SecondaryEquivalenceKey { get { return UnlabeledFormula; } }

        [Track]
        public string Name { get; protected set; }

        [Track]
        public string Formula // The molecular formula - may contain isotopes
        {
            get { return _formula; }
            protected set
            {
                _formula = value ?? string.Empty;
                var unlabeled = string.IsNullOrEmpty(_formula) ? _formula : BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(_formula);
                UnlabeledFormula = Equals(_formula, unlabeled) ? _formula : unlabeled;
            }
        } 

        public string UnlabeledFormula { get; private set; } // Formula with any heavy isotopes translated to light

        public MoleculeAccessionNumbers AccessionNumbers { get; private set; } // InChiKey, CAS, etc to match blib, (see pwiz_tools\BiblioSpec\src\SmallMolMetadata.h)

        public TypedMass MonoisotopicMass { get; private set; }
        public TypedMass AverageMass { get; private set; }

        protected const int DEFAULT_ION_MASS_PRECISION = 6;
        protected static readonly string massFormat = @"{0} [{1:F0"+DEFAULT_ION_MASS_PRECISION+@"}/{2:F0"+DEFAULT_ION_MASS_PRECISION+@"}]";
        protected static readonly string massFormatSameMass = @"{0} [{1:F0" + DEFAULT_ION_MASS_PRECISION + @"}]";
        protected const string massFormatRegex = @"(?:[a-z][a-z]+)\s+\[([+-]?\d*\.\d+)(?![-+0-9\.])\/([+-]?\d*\.\d+)(?![-+0-9\.])\]";

        public SmallMoleculeLibraryAttributes GetSmallMoleculeLibraryAttributes()
        {
            return SmallMoleculeLibraryAttributes.Create(Name, Formula,
                MonoisotopicMass, AverageMass, // In case forumla is empty
                AccessionNumbers.GetInChiKey(), AccessionNumbers.GetNonInChiKeys());
        }

        public string ToSerializableString()
        {
            // Replace tab with something that XML parsers won't mess with
            var tsv = ToTSV();
            return AccessionNumbers.EscapeTabsForXML(tsv);
        }
        public static CustomMolecule FromSerializableString(string val)
        {
            var tsv = MoleculeAccessionNumbers.UnescapeTabsForXML(val);
            return FromTSV(tsv);
        }

        public const char MASS_SPLITTER = '/';

        public static string FormattedMasses(double monoisotopicMass, double averageMass)
        {
            return string.Format(CultureInfo.InvariantCulture, @"{0:F09}/{1:F09}", monoisotopicMass, averageMass);
        }

        public List<string> AsFields()
        {
            var massOrFormula = !string.IsNullOrEmpty(Formula) ?
                Formula :
                FormattedMasses(MonoisotopicMass, AverageMass);
            var parts = new[] { Name, massOrFormula, AccessionNumbers.ToString() };
            return (parts.All(string.IsNullOrEmpty) ? new[] { InvariantName } : parts).ToList();
        }

        public string ToTSV()
        {
            return TextUtil.ToEscapedTSV(AsFields());
        }

        public static CustomMolecule FromTSV(string val)
        {
            var vals = val.FromEscapedTSV();
            var name = vals.Length > 0 ? vals[0] : null;
            var formula = vals.Length > 1 ? vals[1] : null;
            var keysTSV = vals.Length > 2 ? vals[2] : null;
            if (formula == null && name != null && name.StartsWith(INVARIANT_NAME_DETAIL))
            {
                // Looks like a mass-only description
                Regex r = new Regex(massFormatRegex,RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant);
                Match m = r.Match(val);
                if (m.Success)
                {
                    try
                    {
                        var massMono = new TypedMass(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), MassType.Monoisotopic);
                        var massAvg =  new TypedMass(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), MassType.Average);
                        return new CustomMolecule(massMono, massAvg);
                    }
                    catch
                    {
                        Assume.Fail(@"unable to read custom molecule information");
                    }
                }
            }
            else if (formula != null && formula.Contains(MASS_SPLITTER))
            {
                // "formula" is actually mono and average masses
                try
                {
                    var values = formula.Split(MASS_SPLITTER);
                    var massMono = new TypedMass(double.Parse(values[0], CultureInfo.InvariantCulture), MassType.Monoisotopic);
                    var massAvg = new TypedMass(double.Parse(values[1], CultureInfo.InvariantCulture), MassType.Average);
                    return new CustomMolecule(massMono, massAvg, name, new MoleculeAccessionNumbers(keysTSV));
                }
                catch
                {
                    Assume.Fail(@"unable to read custom molecule information");
                }
            }
            return new CustomMolecule(formula, null, null, name, new MoleculeAccessionNumbers(keysTSV));
        }

        public CustomMolecule ChangeFormula(string formula)
        {
            if (Equals(Formula, formula))
                return this;
            return new CustomMolecule(formula, null, null, Name, AccessionNumbers);
        }

        public string DisplayName
        {
            get { return GetDisplayName(); }
        }

        public string GetDisplayName(double? tolerance = null)
        {
            string key;
            if (!string.IsNullOrEmpty(Name))
                return Name;
            else if (!string.IsNullOrEmpty(key = PrimaryEquivalenceKey))
                return key;
            else if (!string.IsNullOrEmpty(Formula))
                return Formula;
            else if (tolerance.HasValue)
            {
                // Display mass at same precision as the tolerance value. Also do not repeat mass if mono and average are the same.
                var format = MonoisotopicMass.Value.Equals(AverageMass.Value) ? massFormatSameMass : massFormat;
                var tol = tolerance.Value.ToString(CultureInfo.InvariantCulture);
                var precision = tol.Length - tol.IndexOf(@".", StringComparison.Ordinal) - 1;
                format = format.Replace(@"F0" + DEFAULT_ION_MASS_PRECISION, @"F0" + precision);
                return String.Format(format, DisplayNameDetail, MonoisotopicMass, AverageMass);
            }
            else
                return String.Format(massFormat, DisplayNameDetail, MonoisotopicMass, AverageMass);  
           
        }

        public virtual string InvariantName
        {
            get
            {
                var key = PrimaryEquivalenceKey;
                if (!string.IsNullOrEmpty(key))
                    return key;
                else if (!string.IsNullOrEmpty(Formula))
                    return Formula;
                else
                    return String.Format(CultureInfo.InvariantCulture, massFormat, InvariantNameDetail, MonoisotopicMass, AverageMass);
            }
        }

        public const string INVARIANT_NAME_DETAIL = "Molecule";
        public virtual string InvariantNameDetail { get { return INVARIANT_NAME_DETAIL; } } 
        public virtual string DisplayNameDetail { get { return Resources.CustomMolecule_DisplayName_Molecule; } }

        public TypedMass GetMass(MassType massType)
        {
            return massType.IsMonoisotopic() ? MonoisotopicMass :  AverageMass;
        }

        public void Validate()
        {
            if (!string.IsNullOrEmpty(Formula))
            {
                try
                {
                    MonoisotopicMass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, Formula);
                    AverageMass = SequenceMassCalc.FormulaMass(BioMassCalc.AVERAGE, Formula);
                }
                catch (ArgumentException x)
                {
                    throw new InvalidDataException(x.Message, x);  // Pass original as inner exception
                }
            }
            if (AverageMass == 0 || MonoisotopicMass == 0)
                throw new InvalidDataException(Resources.CustomMolecule_Validate_Custom_molecules_must_specify_a_formula_or_valid_monoisotopic_and_average_masses_);
            if(AverageMass > MAX_MASS || MonoisotopicMass > MAX_MASS)
                throw new InvalidDataException(string.Format(Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_exceeeds_the_maximum_of__1__, 
                    AverageMass > MAX_MASS ? AverageMass : MonoisotopicMass, MAX_MASS));
            if(AverageMass < MIN_MASS || MonoisotopicMass < MIN_MASS)
                throw new InvalidDataException(string.Format(Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_is_less_than_the_minimum_of__1__,
                    AverageMass < MIN_MASS ? AverageMass : MonoisotopicMass, MIN_MASS));
        }

        public bool IsEmpty
        {
            get
            {
                return ReferenceEquals(this, EMPTY) ||
                       string.IsNullOrEmpty(Name) &&
                       string.IsNullOrEmpty(Formula) &&
                       MonoisotopicMass.Value == 0 &&
                       AverageMass.Value == 0 &&
                       AccessionNumbers.IsEmpty;
            }
        }

        private bool Equals(CustomMolecule other)
        {
            var equal = CompareTo(other) == 0;
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CustomMolecule)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Formula != null ? Formula.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MonoisotopicMass.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageMass.GetHashCode();
                hashCode = (hashCode * 397) ^ AccessionNumbers.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// For use in heavy/light matching, where formula or name is only reliable match value
        /// Without that we use transition list mz sort order
        /// </summary>
        public static bool Equivalent(CustomMolecule molA, CustomMolecule molB)
        {
            if (Equals(molA, molB))
                return true;
            if (molA == null || molB == null)
                return false; // One null, one non-null
            // Name
            if (molA.PrimaryEquivalenceKey != null || molB.PrimaryEquivalenceKey != null)
                return Equals(molA.PrimaryEquivalenceKey, molB.PrimaryEquivalenceKey);
            // Formula (stripped of labels)
            if (molA.SecondaryEquivalenceKey != null || molB.SecondaryEquivalenceKey != null)
                return Equals(molA.SecondaryEquivalenceKey, molB.SecondaryEquivalenceKey);
            return true; // Not proven to be unequivalent - it's up to caller to think about mz
        }

        public int GetEquivalentHashCode()
        {
            if (!string.IsNullOrEmpty(PrimaryEquivalenceKey))
                return PrimaryEquivalenceKey.GetHashCode();
            if (!string.IsNullOrEmpty(SecondaryEquivalenceKey))
                return SecondaryEquivalenceKey.GetHashCode();
            return 0;
        }

        protected enum ATTR
        {
            name, //For Measured ion as in reporter ions
            custom_ion_name, // For custom ion as found in molecule list
            formula, // Obsolete - but this is a cue that the formula is missing a hydrogen, since we used to assume M+H ionization
            ion_formula,
            mass_monoisotopic, //  Obsolete - would be most properly called mass_h_monoisotopic
            mass_average, // Obsolete - would be most properly called mass_h_average
            neutral_formula, // Chemical formula, no adducts
            neutral_mass_monoisotopic,
            neutral_mass_average,
            id
        }

        public TypedMass ReadAverageMass(XmlReader reader)
        {
            var mass = reader.GetNullableDoubleAttribute(ATTR.mass_average); // Pre-3.62 we wrote out massH for custom ions but not for reporter ions
            if (mass.HasValue)
            {
                return new TypedMass(mass.Value, (this is SettingsCustomIon) ? MassType.Average : MassType.AverageMassH);
            }
            return new TypedMass(reader.GetDoubleAttribute(ATTR.neutral_mass_average), MassType.Average); 
        }

        public TypedMass ReadMonoisotopicMass(XmlReader reader)
        {
            var mass = reader.GetNullableDoubleAttribute(ATTR.mass_monoisotopic); // Pre-3.62 we wrote out massH for custom ions but not for reporter ions
            if (mass.HasValue)
            {
                return new TypedMass(mass.Value, (this is SettingsCustomIon) ? MassType.Monoisotopic : MassType.MonoisotopicMassH);
            }
            return new TypedMass(reader.GetDoubleAttribute(ATTR.neutral_mass_monoisotopic), MassType.Monoisotopic);
        }

        /// <summary>
        /// Deserialize an XML description, noting that in pre-3.62 Skyline
        /// the formula may possibly have an adduct appended to it because we
        /// treated the "peptide" and its first precursor as the same thing
        /// </summary>
        public static CustomMolecule Deserialize(XmlReader reader, out Adduct embeddedAdduct)
        {
            var molecule = new CustomMolecule();
            molecule.ReadAttributes(reader, out embeddedAdduct);
            return molecule;
        }

        protected virtual void ReadAttributes(XmlReader reader, out Adduct embeddedAdduct)
        {

            var formula = reader.GetAttribute(ATTR.formula);
            if (!string.IsNullOrEmpty(formula))
            {
                formula = BioMassCalc.AddH(formula);  // Update this old style formula to current by adding the hydrogen we formerly left out due to assuming protonation
            }
            else
            {
                var text = reader.GetAttribute(ATTR.ion_formula) ?? reader.GetAttribute(ATTR.neutral_formula);
                if (text != null)
                    text = text.Trim(); // We've seen some trailing spaces in the wild
                formula = text;
            }
            string neutralFormula;
            Molecule mol;
            // We commonly see the adduct inline with the neutral formula ("C12H5[M+Na]"), so be ready to preserve that
            if (IonInfo.IsFormulaWithAdduct(formula, out mol, out embeddedAdduct, out neutralFormula))
            {
                formula = neutralFormula;
            }
            else
            {
                embeddedAdduct = Adduct.EMPTY;
            }
            if (string.IsNullOrEmpty(formula))
            {
                AverageMass = ReadAverageMass(reader);
                MonoisotopicMass = ReadMonoisotopicMass(reader);
            }
            Formula = formula;

            Name = reader.GetAttribute(ATTR.custom_ion_name);

            if (string.IsNullOrEmpty(Name))
            {
                Name = reader.GetAttribute(ATTR.name) ?? string.Empty;
            }

            AccessionNumbers = MoleculeAccessionNumbers.FromSerializableString(reader.GetAttribute(ATTR.id));

            Validate();
        }

        public void WriteXml(XmlWriter writer, Adduct adduct)
        {
            if (adduct.IsEmpty)
            {
                writer.WriteAttributeIfString(ATTR.neutral_formula, Formula);
            }
            else
            {
                writer.WriteAttributeIfString(ATTR.ion_formula, 
                    (Formula ?? string.Empty) + 
                    (adduct.IsProteomic ? string.Empty : adduct.ToString())); 
            }
            Assume.IsFalse(AverageMass.IsMassH()); // We're going to read these as neutral masses
            Assume.IsFalse(MonoisotopicMass.IsMassH());
            writer.WriteAttributeNullable(ATTR.neutral_mass_average, AverageMass);
            writer.WriteAttributeNullable(ATTR.neutral_mass_monoisotopic, MonoisotopicMass);
            if (!string.IsNullOrEmpty(Name))
                writer.WriteAttribute(ATTR.custom_ion_name, Name);
            writer.WriteAttributeIfString(ATTR.id, AccessionNumbers.ToSerializableString());
        }

        public int CompareTo(CustomMolecule other)
        {
            if (other == null)
                return 1;
            var result = string.CompareOrdinal(Name, other.Name);
            if (result == 0)
            {
                result = string.CompareOrdinal(Formula, other.Formula);
                if (result == 0)
                {
                    result = AccessionNumbers.CompareTo(other.AccessionNumbers);
                    if (result == 0)
                    {
                        result = MonoisotopicMass.Equals(other.MonoisotopicMass, 5E-10) ? // Allow for float vs double serialization effects
                            0 : MonoisotopicMass.CompareTo(other.MonoisotopicMass);
                    }
                }
            }
            return result;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public string ToString(double? tolerance)
        {
            return GetDisplayName(tolerance);
        }

        public static bool IsValidLibKey(string key)
        {
            try
            {
                SequenceMassCalc.FormulaMass(BioMassCalc.AVERAGE, key);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
