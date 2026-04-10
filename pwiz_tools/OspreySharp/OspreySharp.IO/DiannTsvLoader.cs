using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Loads spectral libraries from DIA-NN TSV format.
    /// Ported from osprey-io/src/library/diann.rs.
    /// </summary>
    public class DiannTsvLoader
    {
        private const int DEFAULT_MIN_FRAGMENTS = 3;

        private readonly int _minFragments;

        public DiannTsvLoader() : this(DEFAULT_MIN_FRAGMENTS)
        {
        }

        public DiannTsvLoader(int minFragments)
        {
            _minFragments = minFragments;
        }

        /// <summary>
        /// Load library entries from a DIA-NN TSV file.
        /// </summary>
        public List<LibraryEntry> Load(string path)
        {
            using (var reader = new StreamReader(path))
            {
                return ParseReader(reader);
            }
        }

        /// <summary>
        /// Parse library entries from a text reader (for testability).
        /// </summary>
        public List<LibraryEntry> ParseReader(TextReader reader)
        {
            string headerLine = reader.ReadLine();
            if (headerLine == null)
                throw new InvalidDataException("Empty library file: no header row");

            string[] headers = headerLine.Split('\t');
            var cols = ColumnIndices.FromHeaders(headers);

            var precursorMap = new Dictionary<string, PrecursorData>();

            string line;
            int rowNum = 1;
            while ((line = reader.ReadLine()) != null)
            {
                rowNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] fields = line.Split('\t');
                ParseRow(fields, cols, rowNum, precursorMap);
            }

            // Convert to LibraryEntry list
            var entries = new List<LibraryEntry>(precursorMap.Count);
            uint id = 0;

            foreach (var data in precursorMap.Values)
            {
                if (data.Fragments.Count < _minFragments)
                    continue;

                var modifications = ParseModifications(data.ModifiedSequence);

                var entry = new LibraryEntry(id, data.Sequence, data.ModifiedSequence,
                    data.Charge, data.PrecursorMz, data.RetentionTime);
                entry.Modifications = modifications;
                entry.Fragments = data.Fragments;
                entry.ProteinIds = data.ProteinIds;
                entry.GeneNames = data.GeneNames;

                entries.Add(entry);
                id++;
            }

            return entries;
        }

        private void ParseRow(string[] fields, ColumnIndices cols, int rowNum,
            Dictionary<string, PrecursorData> precursorMap)
        {
            double precursorMz = ParseDouble(GetField(fields, cols.PrecursorMz, "PrecursorMz", rowNum), "PrecursorMz", rowNum);
            byte charge = ParseByte(GetField(fields, cols.PrecursorCharge, "PrecursorCharge", rowNum), "PrecursorCharge", rowNum);
            string modifiedSequence = StripFlankingChars(GetField(fields, cols.ModifiedPeptide, "ModifiedPeptide", rowNum));
            double fragmentMz = ParseDouble(GetField(fields, cols.FragmentMz, "FragmentMz", rowNum), "FragmentMz", rowNum);
            float relativeIntensity = ParseFloat(GetField(fields, cols.RelativeIntensity, "RelativeIntensity", rowNum), "RelativeIntensity", rowNum);

            // Retention time from multiple possible columns
            double retentionTime = 0.0;
            if (cols.IRT >= 0)
                retentionTime = ParseDoubleOrDefault(GetFieldOrNull(fields, cols.IRT), 0.0);
            else if (cols.NormalizedRT >= 0)
                retentionTime = ParseDoubleOrDefault(GetFieldOrNull(fields, cols.NormalizedRT), 0.0);

            // Stripped sequence
            string strippedSequence = GetFieldOrNull(fields, cols.StrippedPeptide);
            if (string.IsNullOrEmpty(strippedSequence))
                strippedSequence = StripModifications(modifiedSequence);

            // Fragment annotation
            IonType ionType = IonType.Unknown;
            string fragTypeStr = GetFieldOrNull(fields, cols.FragmentType);
            if (!string.IsNullOrEmpty(fragTypeStr) && fragTypeStr.Length > 0)
                ionType = IonTypeExtensions.FromChar(fragTypeStr[0]);

            byte ordinal = 0;
            string ordinalStr = GetFieldOrNull(fields, cols.FragmentSeriesNumber);
            if (!string.IsNullOrEmpty(ordinalStr))
                byte.TryParse(ordinalStr, out ordinal);

            byte fragmentCharge = 1;
            string fragChargeStr = GetFieldOrNull(fields, cols.FragmentCharge);
            if (!string.IsNullOrEmpty(fragChargeStr))
                byte.TryParse(fragChargeStr, out fragmentCharge);

            NeutralLoss neutralLoss = null;
            string lossStr = GetFieldOrNull(fields, cols.FragmentLossType);
            if (!string.IsNullOrEmpty(lossStr))
                neutralLoss = NeutralLoss.Parse(lossStr);

            var annotation = new FragmentAnnotation
            {
                IonType = ionType,
                Ordinal = ordinal,
                Charge = fragmentCharge,
                NeutralLoss = neutralLoss
            };

            // Protein and gene info
            List<string> proteinIds = new List<string>();
            string proteinStr = GetFieldOrNull(fields, cols.ProteinId);
            if (!string.IsNullOrEmpty(proteinStr))
                proteinIds = SplitList(proteinStr);

            List<string> geneNames = new List<string>();
            string geneStr = GetFieldOrNull(fields, cols.GeneName);
            if (!string.IsNullOrEmpty(geneStr))
                geneNames = SplitList(geneStr);

            // Group by precursor key
            string key = modifiedSequence + "_" + charge;

            PrecursorData precursor;
            if (!precursorMap.TryGetValue(key, out precursor))
            {
                precursor = new PrecursorData
                {
                    Sequence = strippedSequence,
                    ModifiedSequence = modifiedSequence,
                    Charge = charge,
                    PrecursorMz = precursorMz,
                    RetentionTime = retentionTime,
                    ProteinIds = proteinIds,
                    GeneNames = geneNames,
                    Fragments = new List<LibraryFragment>()
                };
                precursorMap[key] = precursor;
            }

            precursor.Fragments.Add(new LibraryFragment
            {
                Mz = fragmentMz,
                RelativeIntensity = relativeIntensity,
                Annotation = annotation
            });
        }

        /// <summary>
        /// Parse modifications from a modified peptide sequence.
        /// Handles bracket notation e.g. "PEPTM[+15.9949]IDE" and
        /// parenthetical notation e.g. "M(UniMod:35)PEPTIDE".
        /// </summary>
        public static List<Modification> ParseModifications(string modified)
        {
            var modifications = new List<Modification>();
            int position = 0;
            int i = 0;

            while (i < modified.Length)
            {
                char c = modified[i];

                if (char.IsLetter(c))
                {
                    i++;
                    // Check for bracket modification after this residue
                    if (i < modified.Length && modified[i] == '[')
                    {
                        i++; // consume '['
                        int start = i;
                        while (i < modified.Length && modified[i] != ']')
                            i++;
                        string modStr = modified.Substring(start, i - start);
                        if (i < modified.Length)
                            i++; // consume ']'

                        double? mass = ParseModMass(modStr);
                        if (mass.HasValue)
                        {
                            modifications.Add(new Modification
                            {
                                Position = position,
                                UnimodId = ParseUnimodId(modStr),
                                MassDelta = mass.Value,
                                Name = modStr
                            });
                        }
                    }
                    position++;
                }
                else if (c == '(')
                {
                    i++; // consume '('
                    int start = i;
                    while (i < modified.Length && modified[i] != ')')
                        i++;
                    string modStr = modified.Substring(start, i - start);
                    if (i < modified.Length)
                        i++; // consume ')'

                    double? mass = ParseModMass(modStr);
                    if (mass.HasValue)
                    {
                        modifications.Add(new Modification
                        {
                            Position = Math.Max(0, position - 1),
                            UnimodId = ParseUnimodId(modStr),
                            MassDelta = mass.Value,
                            Name = modStr
                        });
                    }
                }
                else if (c == '[')
                {
                    // N-terminal bracket modification before any residue
                    i++; // consume '['
                    int start = i;
                    while (i < modified.Length && modified[i] != ']')
                        i++;
                    string modStr = modified.Substring(start, i - start);
                    if (i < modified.Length)
                        i++; // consume ']'

                    double? mass = ParseModMass(modStr);
                    if (mass.HasValue)
                    {
                        modifications.Add(new Modification
                        {
                            Position = 0,
                            UnimodId = ParseUnimodId(modStr),
                            MassDelta = mass.Value,
                            Name = modStr
                        });
                    }
                }
                else
                {
                    i++;
                }
            }

            return modifications;
        }

        /// <summary>
        /// Strip modifications from a modified peptide sequence, returning bare amino acid letters.
        /// </summary>
        public static string StripModifications(string modified)
        {
            var result = new System.Text.StringBuilder(modified.Length);
            bool inBracket = false;
            bool inParen = false;

            foreach (char c in modified)
            {
                switch (c)
                {
                    case '[':
                        inBracket = true;
                        break;
                    case ']':
                        inBracket = false;
                        break;
                    case '(':
                        inParen = true;
                        break;
                    case ')':
                        inParen = false;
                        break;
                    default:
                        if (!inBracket && !inParen && char.IsLetter(c))
                            result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Parse mass value from a modification string.
        /// Handles numeric values, +/- prefixed values, UniMod notation, and named modifications.
        /// </summary>
        public static double? ParseModMass(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            // Try parsing as a number directly
            double mass;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                return mass;

            // Handle +/- prefix
            if (s.Length > 1 && (s[0] == '+' || s[0] == '-'))
            {
                if (double.TryParse(s.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                    return s[0] == '-' ? -mass : mass;
            }

            // Try UniMod notation (e.g. "UniMod:4" or "UNIMOD:4")
            string idStr = null;
            if (s.StartsWith("UniMod:", StringComparison.OrdinalIgnoreCase))
                idStr = s.Substring(7);
            else if (s.StartsWith("UNIMOD:", StringComparison.OrdinalIgnoreCase))
                idStr = s.Substring(7);

            if (idStr != null)
            {
                int unimodId;
                if (int.TryParse(idStr, out unimodId))
                {
                    double? unimodMass = UnimodIdToMass(unimodId);
                    if (unimodMass.HasValue)
                        return unimodMass;
                }
            }

            // Known modifications by name
            switch (s.ToUpperInvariant())
            {
                case "OXIDATION": return 15.9949;
                case "CARBAMIDOMETHYL":
                case "CAM": return 57.0215;
                case "PHOSPHO": return 79.9663;
                case "ACETYL": return 42.0106;
                case "DEAMIDATED":
                case "DEAMIDATION": return 0.9840;
                default: return null;
            }
        }

        /// <summary>
        /// Parse UniMod ID from a modification string, if present.
        /// </summary>
        public static int? ParseUnimodId(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            int idx = s.IndexOf("UniMod:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            string rest = s.Substring(idx + 7);
            int end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
                end++;

            if (end == 0)
                return null;

            int id;
            if (int.TryParse(rest.Substring(0, end), out id))
                return id;

            return null;
        }

        /// <summary>
        /// Look up mass delta for a UniMod ID.
        /// </summary>
        public static double? UnimodIdToMass(int id)
        {
            switch (id)
            {
                case 1: return 42.010565;    // Acetyl
                case 4: return 57.021464;    // Carbamidomethyl
                case 5: return 43.005814;    // Carbamyl
                case 7: return 0.984016;     // Deamidated
                case 21: return 79.966331;   // Phospho
                case 28: return -18.010565;  // Glu->pyro-Glu
                case 34: return 14.015650;   // Methyl
                case 35: return 15.994915;   // Oxidation
                case 36: return 28.031300;   // Dimethyl
                case 37: return 42.046950;   // Trimethyl
                case 121: return 114.042927; // Ubiquitin (GlyGly)
                case 122: return 383.228102; // SUMO
                case 214: return 44.985078;  // Nitro
                case 312: return -17.026549; // Ammonia loss
                case 385: return 229.162932; // TMT6plex
                case 737: return 229.162932; // TMT6plex (alternate)
                case 747: return 304.207146; // TMTpro
                default: return null;
            }
        }

        /// <summary>
        /// Split a semicolon or comma separated list, trimming whitespace.
        /// </summary>
        public static List<string> SplitList(string s)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s))
                return result;

            string[] parts = s.Split(new[] { ';', ',' }, StringSplitOptions.None);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
            return result;
        }

        /// <summary>
        /// Strip flanking characters from peptide sequences.
        /// Handles "_PEPTIDE_", "K.PEPTIDE.R", "-PEPTIDE-" formats.
        /// </summary>
        internal static string StripFlankingChars(string seq)
        {
            string trimmed = seq.Trim('_', '.', '-');

            // Handle internal patterns like "K.PEPTIDE.R" -> "PEPTIDE"
            int firstDot = trimmed.IndexOf('.');
            int lastDot = trimmed.LastIndexOf('.');
            if (firstDot >= 0 && lastDot > firstDot)
                return trimmed.Substring(firstDot + 1, lastDot - firstDot - 1);

            return trimmed;
        }

        #region Private helpers

        private static string GetField(string[] fields, int index, string name, int rowNum)
        {
            if (index < 0 || index >= fields.Length)
                throw new InvalidDataException(string.Format("Missing {0} at row {1}", name, rowNum));
            return fields[index];
        }

        private static string GetFieldOrNull(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
                return null;
            return fields[index];
        }

        private static double ParseDouble(string s, string name, int rowNum)
        {
            double value;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new InvalidDataException(string.Format("Invalid {0} '{1}' at row {2}", name, s, rowNum));
            return value;
        }

        private static float ParseFloat(string s, string name, int rowNum)
        {
            float value;
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new InvalidDataException(string.Format("Invalid {0} '{1}' at row {2}", name, s, rowNum));
            return value;
        }

        private static byte ParseByte(string s, string name, int rowNum)
        {
            byte value;
            if (!byte.TryParse(s, out value))
                throw new InvalidDataException(string.Format("Invalid {0} '{1}' at row {2}", name, s, rowNum));
            return value;
        }

        private static double ParseDoubleOrDefault(string s, double defaultValue)
        {
            if (string.IsNullOrEmpty(s))
                return defaultValue;
            double value;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return defaultValue;
        }

        #endregion

        /// <summary>
        /// Column index lookup for DIA-NN TSV headers.
        /// </summary>
        private class ColumnIndices
        {
            public int PrecursorMz = -1;
            public int PrecursorCharge = -1;
            public int ModifiedPeptide = -1;
            public int StrippedPeptide = -1;
            public int FragmentMz = -1;
            public int RelativeIntensity = -1;
            public int FragmentType = -1;
            public int FragmentSeriesNumber = -1;
            public int FragmentCharge = -1;
            public int FragmentLossType = -1;
            public int IRT = -1;
            public int NormalizedRT = -1;
            public int ProteinId = -1;
            public int GeneName = -1;

            public static ColumnIndices FromHeaders(string[] headers)
            {
                var indices = new ColumnIndices();

                indices.PrecursorMz = FindColumn(headers, "PrecursorMz", "Precursor.Mz", "Q1");
                indices.PrecursorCharge = FindColumn(headers, "PrecursorCharge", "Precursor.Charge");
                indices.ModifiedPeptide = FindColumn(headers, "ModifiedPeptide", "Modified.Peptide", "FullPeptideName");
                indices.StrippedPeptide = FindColumn(headers, "StrippedPeptide", "Stripped.Peptide", "PeptideSequence");
                indices.FragmentMz = FindColumn(headers, "FragmentMz", "Fragment.Mz", "ProductMz", "Q3");
                indices.RelativeIntensity = FindColumn(headers, "RelativeIntensity", "Relative.Intensity", "LibraryIntensity");
                indices.FragmentType = FindColumn(headers, "FragmentType", "Fragment.Type", "IonType");
                indices.FragmentSeriesNumber = FindColumn(headers, "FragmentSeriesNumber", "FragmentNumber", "IonNumber");
                indices.FragmentCharge = FindColumn(headers, "FragmentCharge", "Fragment.Charge", "ProductCharge");
                indices.FragmentLossType = FindColumn(headers, "FragmentLossType", "LossType", "NeutralLoss");
                indices.IRT = FindColumn(headers, "iRT", "iRt");
                indices.NormalizedRT = FindColumn(headers, "NormalizedRetentionTime", "Tr_recalibrated", "RT");
                indices.ProteinId = FindColumn(headers, "ProteinId", "Protein.Id", "ProteinName", "Protein", "ProteinIds", "Protein.Ids");
                indices.GeneName = FindColumn(headers, "GeneName", "Gene.Name", "Genes", "Protein.Names");

                // Validate required columns
                if (indices.PrecursorMz < 0)
                    throw new InvalidDataException("Missing required column: PrecursorMz");
                if (indices.PrecursorCharge < 0)
                    throw new InvalidDataException("Missing required column: PrecursorCharge");
                if (indices.ModifiedPeptide < 0)
                    throw new InvalidDataException("Missing required column: ModifiedPeptide");
                if (indices.FragmentMz < 0)
                    throw new InvalidDataException("Missing required column: FragmentMz");
                if (indices.RelativeIntensity < 0)
                    throw new InvalidDataException("Missing required column: RelativeIntensity");

                return indices;
            }

            private static int FindColumn(string[] headers, params string[] names)
            {
                foreach (string name in names)
                {
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Intermediate data for grouping fragments by precursor.
        /// </summary>
        private class PrecursorData
        {
            public string Sequence;
            public string ModifiedSequence;
            public byte Charge;
            public double PrecursorMz;
            public double RetentionTime;
            public List<string> ProteinIds;
            public List<string> GeneNames;
            public List<LibraryFragment> Fragments;
        }
    }
}
