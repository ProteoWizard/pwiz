/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Binary cache for fast reload of parsed spectral libraries.
    /// Ported from osprey-io/src/library/cache.rs.
    /// </summary>
    public static class LibraryCache
    {
        /// <summary>Magic bytes at the start of every cache file.</summary>
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("OSPRLBR\0");

        /// <summary>
        /// Current cache format version. v2 stamps the source library's
        /// identity hash (<see cref="pwiz.Osprey.Core.SearchIdentity.LibraryIdentityHash"/>)
        /// into the header immediately after the version, so a cache built from
        /// a different build of the same library path is detected and rebuilt.
        /// v1 had no identity and a different header layout, so it reads as
        /// <see cref="LibraryCacheStatus.Invalid"/> and is rebuilt once.
        /// </summary>
        private const uint VERSION = 2;

        /// <summary>
        /// Outcome of a <see cref="LoadCache(string,string,out LibraryCacheStatus)"/>
        /// attempt.
        /// </summary>
        public enum LibraryCacheStatus
        {
            /// <summary>Cache was valid and its entries were read.</summary>
            Loaded,
            /// <summary>
            /// Cache was structurally valid but was built from a different
            /// version of the source library (stored identity hash mismatch).
            /// Its entries were NOT read, so the caller should rebuild.
            /// </summary>
            IdentityMismatch,
            /// <summary>
            /// Cache was unreadable: bad magic bytes or an unsupported version.
            /// </summary>
            Invalid
        }

        /// <summary>
        /// Save library entries to a binary cache file, stamping the source
        /// library's identity hash into the header.
        /// </summary>
        public static void SaveCache(string path, List<LibraryEntry> entries, string libraryHash)
        {
            using (var saver = new FileSaver(path))
            {
                using (var stream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(stream))
                {
                    w.Write(MAGIC);
                    w.Write(VERSION);
                    WriteString(w, libraryHash ?? string.Empty);
                    w.Write((ulong)entries.Count);

                    foreach (var entry in entries)
                    {
                        w.Write(entry.Id);
                        WriteString(w, entry.Sequence);
                        WriteString(w, entry.ModifiedSequence);
                        w.Write(entry.Charge);
                        w.Write(entry.PrecursorMz);
                        w.Write(entry.RetentionTime);
                        w.Write(entry.RtCalibrated ? (byte)1 : (byte)0);
                        w.Write(entry.IsDecoy ? (byte)1 : (byte)0);

                        // Modifications
                        w.Write((uint)entry.Modifications.Count);
                        foreach (var m in entry.Modifications)
                        {
                            w.Write((uint)m.Position);
                            if (m.UnimodId.HasValue)
                            {
                                w.Write((byte)1);
                                w.Write((uint)m.UnimodId.Value);
                            }
                            else
                            {
                                w.Write((byte)0);
                            }
                            w.Write(m.MassDelta);
                            if (m.Name != null)
                            {
                                w.Write((byte)1);
                                WriteString(w, m.Name);
                            }
                            else
                            {
                                w.Write((byte)0);
                            }
                        }

                        // Fragments
                        w.Write((uint)entry.Fragments.Count);
                        foreach (var frag in entry.Fragments)
                        {
                            w.Write(frag.Mz);
                            w.Write(frag.RelativeIntensity);
                            w.Write(IonTypeToByte(frag.Annotation.IonType));
                            w.Write(frag.Annotation.Ordinal);
                            w.Write(frag.Annotation.Charge);
                            WriteNeutralLoss(w, frag.Annotation.NeutralLoss, frag.Annotation.CustomLossMass);
                        }

                        // Protein IDs
                        w.Write((uint)entry.ProteinIds.Count);
                        foreach (string pid in entry.ProteinIds)
                            WriteString(w, pid);

                        // Gene names
                        w.Write((uint)entry.GeneNames.Count);
                        foreach (string gn in entry.GeneNames)
                            WriteString(w, gn);
                    }

                    w.Flush();
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Load library entries from a binary cache file, identity-agnostic.
        /// Returns null if the file is invalid or has an unsupported version.
        /// Thin overload of <see cref="LoadCache(string,string,out LibraryCacheStatus)"/>
        /// with no expected identity hash (used by round-trip tests and the
        /// source-missing fallback, where the cache is the only copy available).
        /// </summary>
        public static List<LibraryEntry> LoadCache(string path)
        {
            return LoadCache(path, null, out _);
        }

        /// <summary>
        /// Overload accepting a log callback so the string-interning summary
        /// (emitted once per load) reaches the pipeline log. See the primary
        /// <see cref="LoadCache(string,string,Action{string},out LibraryCacheStatus)"/>.
        /// </summary>
        public static List<LibraryEntry> LoadCache(string path, string expectedLibraryHash,
            out LibraryCacheStatus status)
        {
            return LoadCache(path, expectedLibraryHash, null, out status);
        }

        /// <summary>
        /// Load library entries from a binary cache file, validating the source
        /// library's identity hash against <paramref name="expectedLibraryHash"/>.
        /// On bad magic or an unsupported version, returns null with
        /// <paramref name="status"/> = <see cref="LibraryCacheStatus.Invalid"/>.
        /// When <paramref name="expectedLibraryHash"/> is non-empty and the
        /// stored hash differs, returns null with
        /// <see cref="LibraryCacheStatus.IdentityMismatch"/> WITHOUT reading the
        /// entries (skips a multi-GB read on a stale cache). Otherwise reads the
        /// entries and returns <see cref="LibraryCacheStatus.Loaded"/>.
        /// </summary>
        public static List<LibraryEntry> LoadCache(string path, string expectedLibraryHash,
            Action<string> logInfo, out LibraryCacheStatus status)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(stream))
            {
                // Validate magic
                byte[] magic = r.ReadBytes(8);
                if (magic.Length != 8 || !BytesEqual(magic, MAGIC))
                {
                    status = LibraryCacheStatus.Invalid;
                    return null;
                }

                uint version = r.ReadUInt32();
                if (version != VERSION)
                {
                    status = LibraryCacheStatus.Invalid;
                    return null;
                }

                // Identity hash stamped at write time (v2). When the caller
                // supplies a non-empty expected hash and it disagrees, the cache
                // was built from a different version of the source library at
                // this path -- treat it as stale and skip reading the entries.
                string storedLibraryHash = ReadString(r);
                if (!string.IsNullOrEmpty(expectedLibraryHash) &&
                    !string.Equals(storedLibraryHash, expectedLibraryHash, StringComparison.Ordinal))
                {
                    status = LibraryCacheStatus.IdentityMismatch;
                    return null;
                }

                ulong count = r.ReadUInt64();
                var entries = new List<LibraryEntry>((int)count);

                // Intern the repeated strings (sequences, modification names,
                // protein / gene accessions) as the interned arrays are filled,
                // so no member is mutated after assignment. One pool per load
                // call; only object identity changes, so output is unchanged.
                var interner = new LibraryStringInterner();

                for (ulong idx = 0; idx < count; idx++)
                {
                    uint id = r.ReadUInt32();
                    string sequence = interner.Intern(ReadString(r));
                    string modifiedSequence = interner.Intern(ReadString(r));
                    byte charge = r.ReadByte();
                    double precursorMz = r.ReadDouble();
                    double retentionTime = r.ReadDouble();
                    bool rtCalibrated = r.ReadByte() != 0;
                    bool isDecoy = r.ReadByte() != 0;

                    // Modifications (share one empty array when none).
                    uint nMods = r.ReadUInt32();
                    var modifications = nMods == 0
                        ? Array.Empty<Modification>()
                        : new Modification[nMods];
                    for (uint mi = 0; mi < nMods; mi++)
                    {
                        int position = (int)r.ReadUInt32();
                        bool hasUnimod = r.ReadByte() != 0;
                        int? unimodId = hasUnimod ? (int?)r.ReadUInt32() : null;
                        double massDelta = r.ReadDouble();
                        bool hasName = r.ReadByte() != 0;
                        string name = hasName ? interner.Intern(ReadString(r)) : null;

                        modifications[mi] = new Modification
                        {
                            Position = position,
                            UnimodId = unimodId,
                            MassDelta = massDelta,
                            Name = name
                        };
                    }

                    // Fragments
                    uint nFrags = r.ReadUInt32();
                    var fragments = nFrags == 0
                        ? Array.Empty<LibraryFragment>()
                        : new LibraryFragment[nFrags];
                    for (uint fi = 0; fi < nFrags; fi++)
                    {
                        double mz = r.ReadDouble();
                        float relativeIntensity = r.ReadSingle();
                        IonType ionType = ByteToIonType(r.ReadByte());
                        byte ordinal = r.ReadByte();
                        byte fragCharge = r.ReadByte();
                        var (lossCode, lossMass) = ReadNeutralLoss(r);

                        fragments[fi] = new LibraryFragment
                        {
                            Mz = mz,
                            RelativeIntensity = relativeIntensity,
                            Annotation = new FragmentAnnotation
                            {
                                IonType = ionType,
                                Ordinal = ordinal,
                                Charge = fragCharge,
                                NeutralLoss = lossCode,
                                CustomLossMass = lossMass
                            }
                        };
                    }

                    // Protein IDs / gene names (share one empty array when none).
                    uint nProteins = r.ReadUInt32();
                    var proteinIds = nProteins == 0
                        ? Array.Empty<string>()
                        : new string[nProteins];
                    for (uint pi = 0; pi < nProteins; pi++)
                        proteinIds[pi] = interner.Intern(ReadString(r));

                    uint nGenes = r.ReadUInt32();
                    var geneNames = nGenes == 0
                        ? Array.Empty<string>()
                        : new string[nGenes];
                    for (uint gi = 0; gi < nGenes; gi++)
                        geneNames[gi] = interner.Intern(ReadString(r));

                    var entry = new LibraryEntry(id, sequence, modifiedSequence,
                        charge, precursorMz, retentionTime);
                    entry.RtCalibrated = rtCalibrated;
                    entry.IsDecoy = isDecoy;
                    entry.Modifications = modifications;
                    entry.Fragments = fragments;
                    entry.ProteinIds = proteinIds;
                    entry.GeneNames = geneNames;

                    entries.Add(entry);
                }

                interner.LogSummary(logInfo);
                status = LibraryCacheStatus.Loaded;
                return entries;
            }
        }

        #region Private helpers

        private static void WriteString(BinaryWriter w, string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            w.Write((uint)bytes.Length);
            w.Write(bytes);
        }

        private static string ReadString(BinaryReader r)
        {
            uint len = r.ReadUInt32();
            byte[] bytes = r.ReadBytes((int)len);
            return Encoding.UTF8.GetString(bytes);
        }

        private static byte IonTypeToByte(IonType ion)
        {
            switch (ion)
            {
                case IonType.A: return 0;
                case IonType.B: return 1;
                case IonType.C: return 2;
                case IonType.X: return 3;
                case IonType.Y: return 4;
                case IonType.Z: return 5;
                case IonType.Precursor: return 6;
                case IonType.Immonium: return 7;
                case IonType.Internal: return 8;
                default: return 9; // Unknown
            }
        }

        private static IonType ByteToIonType(byte v)
        {
            switch (v)
            {
                case 0: return IonType.A;
                case 1: return IonType.B;
                case 2: return IonType.C;
                case 3: return IonType.X;
                case 4: return IonType.Y;
                case 5: return IonType.Z;
                case 6: return IonType.Precursor;
                case 7: return IonType.Immonium;
                case 8: return IonType.Internal;
                default: return IonType.Unknown;
            }
        }

        private static void WriteNeutralLoss(BinaryWriter w, NeutralLossCode code, double customMass)
        {
            switch (code)
            {
                case NeutralLossCode.None:
                    w.Write((byte)0);
                    break;
                case NeutralLossCode.H2O:
                    w.Write((byte)1);
                    break;
                case NeutralLossCode.NH3:
                    w.Write((byte)2);
                    break;
                case NeutralLossCode.H3PO4:
                    w.Write((byte)3);
                    break;
                default:
                    // Custom -- collapse to a named tag when the mass matches one
                    // within 1e-6, matching the legacy reference-type writer so the
                    // on-disk bytes are unchanged.
                    if (Math.Abs(customMass - NeutralLoss.H2OMass) < 1e-6)
                    {
                        w.Write((byte)1);
                    }
                    else if (Math.Abs(customMass - NeutralLoss.NH3Mass) < 1e-6)
                    {
                        w.Write((byte)2);
                    }
                    else if (Math.Abs(customMass - NeutralLoss.H3PO4Mass) < 1e-6)
                    {
                        w.Write((byte)3);
                    }
                    else
                    {
                        w.Write((byte)4);
                        w.Write(customMass);
                    }
                    break;
            }
        }

        private static (NeutralLossCode Code, double CustomMass) ReadNeutralLoss(BinaryReader r)
        {
            byte tag = r.ReadByte();
            switch (tag)
            {
                case 0: return (NeutralLossCode.None, 0.0);
                case 1: return (NeutralLossCode.H2O, 0.0);
                case 2: return (NeutralLossCode.NH3, 0.0);
                case 3: return (NeutralLossCode.H3PO4, 0.0);
                case 4:
                    double mass = r.ReadDouble();
                    return (NeutralLossCode.Custom, mass);
                default:
                    throw new InvalidDataException(string.Format(
                        "Unknown neutral loss tag: {0}", tag));
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        #endregion
    }
}
