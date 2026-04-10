using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Binary cache for fast reload of parsed spectral libraries.
    /// Ported from osprey-io/src/library/cache.rs.
    /// </summary>
    public static class LibraryCache
    {
        /// <summary>Magic bytes at the start of every cache file.</summary>
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("OSPRLBR\0");

        /// <summary>Current cache format version.</summary>
        private const uint VERSION = 1;

        /// <summary>
        /// Save library entries to a binary cache file.
        /// </summary>
        public static void SaveCache(string path, List<LibraryEntry> entries)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(MAGIC);
                w.Write(VERSION);
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
                        WriteNeutralLoss(w, frag.Annotation.NeutralLoss);
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
        }

        /// <summary>
        /// Load library entries from a binary cache file.
        /// Returns null if the file is invalid or has an unsupported version.
        /// </summary>
        public static List<LibraryEntry> LoadCache(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var r = new BinaryReader(stream))
            {
                // Validate magic
                byte[] magic = r.ReadBytes(8);
                if (magic.Length != 8 || !BytesEqual(magic, MAGIC))
                    return null;

                uint version = r.ReadUInt32();
                if (version != VERSION)
                    return null;

                ulong count = r.ReadUInt64();
                var entries = new List<LibraryEntry>((int)count);

                for (ulong idx = 0; idx < count; idx++)
                {
                    uint id = r.ReadUInt32();
                    string sequence = ReadString(r);
                    string modifiedSequence = ReadString(r);
                    byte charge = r.ReadByte();
                    double precursorMz = r.ReadDouble();
                    double retentionTime = r.ReadDouble();
                    bool rtCalibrated = r.ReadByte() != 0;
                    bool isDecoy = r.ReadByte() != 0;

                    // Modifications
                    uint nMods = r.ReadUInt32();
                    var modifications = new List<Modification>((int)nMods);
                    for (uint mi = 0; mi < nMods; mi++)
                    {
                        int position = (int)r.ReadUInt32();
                        bool hasUnimod = r.ReadByte() != 0;
                        int? unimodId = hasUnimod ? (int?)r.ReadUInt32() : null;
                        double massDelta = r.ReadDouble();
                        bool hasName = r.ReadByte() != 0;
                        string name = hasName ? ReadString(r) : null;

                        modifications.Add(new Modification
                        {
                            Position = position,
                            UnimodId = unimodId,
                            MassDelta = massDelta,
                            Name = name
                        });
                    }

                    // Fragments
                    uint nFrags = r.ReadUInt32();
                    var fragments = new List<LibraryFragment>((int)nFrags);
                    for (uint fi = 0; fi < nFrags; fi++)
                    {
                        double mz = r.ReadDouble();
                        float relativeIntensity = r.ReadSingle();
                        IonType ionType = ByteToIonType(r.ReadByte());
                        byte ordinal = r.ReadByte();
                        byte fragCharge = r.ReadByte();
                        NeutralLoss neutralLoss = ReadNeutralLoss(r);

                        fragments.Add(new LibraryFragment
                        {
                            Mz = mz,
                            RelativeIntensity = relativeIntensity,
                            Annotation = new FragmentAnnotation
                            {
                                IonType = ionType,
                                Ordinal = ordinal,
                                Charge = fragCharge,
                                NeutralLoss = neutralLoss
                            }
                        });
                    }

                    // Protein IDs
                    uint nProteins = r.ReadUInt32();
                    var proteinIds = new List<string>((int)nProteins);
                    for (uint pi = 0; pi < nProteins; pi++)
                        proteinIds.Add(ReadString(r));

                    // Gene names
                    uint nGenes = r.ReadUInt32();
                    var geneNames = new List<string>((int)nGenes);
                    for (uint gi = 0; gi < nGenes; gi++)
                        geneNames.Add(ReadString(r));

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

        private static void WriteNeutralLoss(BinaryWriter w, NeutralLoss nl)
        {
            if (nl == null)
            {
                w.Write((byte)0);
            }
            else if (ReferenceEquals(nl, NeutralLoss.H2O) ||
                     Math.Abs(nl.Mass - NeutralLoss.H2O.Mass) < 1e-6)
            {
                w.Write((byte)1);
            }
            else if (ReferenceEquals(nl, NeutralLoss.NH3) ||
                     Math.Abs(nl.Mass - NeutralLoss.NH3.Mass) < 1e-6)
            {
                w.Write((byte)2);
            }
            else if (ReferenceEquals(nl, NeutralLoss.H3PO4) ||
                     Math.Abs(nl.Mass - NeutralLoss.H3PO4.Mass) < 1e-6)
            {
                w.Write((byte)3);
            }
            else
            {
                w.Write((byte)4);
                w.Write(nl.Mass);
            }
        }

        private static NeutralLoss ReadNeutralLoss(BinaryReader r)
        {
            byte tag = r.ReadByte();
            switch (tag)
            {
                case 0: return null;
                case 1: return NeutralLoss.H2O;
                case 2: return NeutralLoss.NH3;
                case 3: return NeutralLoss.H3PO4;
                case 4:
                    double mass = r.ReadDouble();
                    return NeutralLoss.Custom(mass);
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
