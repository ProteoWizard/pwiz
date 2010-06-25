//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Iesi.Collections.Generic;

namespace IDPicker.DataModel
{
    public class SpectrumSourceGroup : Entity<SpectrumSourceGroup>
    {
        public virtual string Name { get; set; }
        public virtual ISet<SpectrumSourceGroupLink> Sources { get; set; }
    }

    public class SpectrumSource : Entity<SpectrumSource>
    {
        /// <summary>
        /// The direct parent group of this spectrum source.
        /// </summary>
        public virtual SpectrumSourceGroup Group { get; set; }

        /// <summary>
        /// All parent groups of this spectrum source (direct and indirect).
        /// </summary>
        public virtual ISet<SpectrumSourceGroupLink> Groups { get; set; }
        public virtual string Name { get; set; }
        public virtual string URL { get; set; }
        public virtual IList<Spectrum> Spectra { get; set; }

        public virtual string MetadataPath { get; protected set; }
        public virtual pwiz.CLI.msdata.MSData Metadata { get { return new pwiz.CLI.msdata.MSDataFile(MetadataPath); } }
    }

    // HACK: explicit many-to-many linker table is needed for
    // fast queries on indirect relationships between sources and groups
    // i.e. group '/' has a link to ALL sources
    public class SpectrumSourceGroupLink : Entity<SpectrumSourceGroupLink>
    {
        public virtual SpectrumSourceGroup Group { get; set; }
        public virtual SpectrumSource Source { get; set; }
    }

    public class Spectrum : Entity<Spectrum>
    {
        public virtual SpectrumSource Source { get; set; }
        public virtual int Index { get; set; }
        public virtual string NativeID { get; set; }
        public virtual double PrecursorMZ { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }

        /*public struct Peak
        {
            public double MZ { get; set; }
            public double Intensity { get; set; }
        }

        public virtual Peak[] Peaks { get; set; }*/

        public virtual pwiz.CLI.msdata.Spectrum Metadata
        {
            get
            {
                var spectrumList = Source.Metadata.run.spectrumList;
                return spectrumList.spectrum(spectrumList.find(NativeID), false);
            }
        }
    }

    public class AnalysisSoftware
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public enum AnalysisType
    {
        DatabaseSearch,
        SpectrumLibrary,
        SequenceTag
    }

    public class AnalysisParameter : Entity<AnalysisParameter>, IComparable<AnalysisParameter>
    {
        public virtual Analysis Analysis { get; set; }
        public virtual string Name { get; set; }
        public virtual string Value { get; set; }

        public virtual int CompareTo (AnalysisParameter other)
        {
            int nameCompare = Name.CompareTo(other.Name);
            if (nameCompare == 0)
                return Value.CompareTo(other.Value);
            return nameCompare;
        }
    }

    public class Analysis : Entity<Analysis>
    {
        public virtual string Name { get; set; }
        public virtual AnalysisSoftware Software { get; set; }
        public virtual AnalysisType Type { get; set; }
        public virtual DateTime StartTime { get; set; }
        public virtual ISet<AnalysisParameter> Parameters { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }
    }

    public class Protein : Entity<Protein>
    {
        public virtual string Accession { get; set; }
        public virtual string Description { get; set; }
        public virtual string Sequence { get; set; }
        public virtual IList<PeptideInstance> Peptides { get; set; }

        public virtual long Cluster { get { return cluster; } }
        public virtual string ProteinGroup { get { return proteinGroup; } }
        public virtual long Length { get { return length; } }

        #region Property implementation
        long cluster = 0;
        string proteinGroup = "";
        long length = 0;
        #endregion
    }

    public class Peptide : Entity<Peptide>
    {
        public virtual string Sequence { get { return sequence; } }

        public virtual IList<PeptideInstance> Instances { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }

        public virtual double MonoisotopicMass { get; set; }
        public virtual double MolecularWeight { get; set; }

        #region Sequence implementation for transient instances
        public Peptide () { }
        internal Peptide (string sequence) { this.sequence = sequence; }
        private string sequence = null;
        #endregion
    }

    public class PeptideInstance : Entity<PeptideInstance>
    {
        public virtual Peptide Peptide { get; set; }
        public virtual Protein Protein { get; set; }
        public virtual int Offset { get; set; }
        public virtual int Length { get; set; }
        public virtual bool NTerminusIsSpecific { get; set; }
        public virtual bool CTerminusIsSpecific { get; set; }
        public virtual int MissedCleavages { get; set; }

        private int specificTermini = -1;
        public virtual int SpecificTermini { get { return specificTermini; } }
    }

    public class Modification : Entity<Modification>
    {
        public virtual double MonoMassDelta { get; set; }
        public virtual double AvgMassDelta { get; set; }
        public virtual string Formula { get; set; }
        public virtual string Name { get; set; }
    }

    public class PeptideSpectrumMatch : Entity<PeptideSpectrumMatch>
    {
        public virtual Spectrum Spectrum { get; set; }
        public virtual Analysis Analysis { get; set; }
        public virtual Peptide Peptide { get; set; }
        public virtual IList<PeptideModification> Modifications { get; set; }
        public virtual double QValue { get; set; }
        public virtual double MonoisotopicMass { get; set; }
        public virtual double MolecularWeight { get; set; }
        public virtual double MonoisotopicMassError { get; set; }
        public virtual double MolecularWeightError { get; set; }
        public virtual int Rank { get; set; }
        public virtual int Charge { get; set; }
        public virtual IDictionary<string, double> Scores { get; set; }

        private string fullDistinctKey = "", sequenceAndMassDistinctKey = "";
        public virtual string FullDistinctKey { get { return fullDistinctKey; } }
        public virtual string SequenceAndMassDistinctKey { get { return sequenceAndMassDistinctKey; } }
    }

    public class PeptideModification : Entity<PeptideModification>
    {
        public virtual PeptideSpectrumMatch PeptideSpectrumMatch { get; set; }
        public virtual Modification Modification { get; set; }

        public virtual int Offset
        {
            get { return offset; }
            set
            {
                offset = value;

                if (offset == int.MinValue)
                    site = '(';
                else if (offset == int.MaxValue)
                    site = ')';
                else
                {
                    var firstInstance = PeptideSpectrumMatch.Peptide.Instances.First();
                    site = firstInstance.Protein.Sequence[firstInstance.Offset + offset - 1];
                }
            }
        }

        /// <summary>
        /// A symbol representing the modified site, taking one of the following forms:
        /// <para>- an amino acid symbol for a non-terminal modification</para>
        /// <para>- '(' for an N-terminal modification</para>
        /// <para>- ')' for a C-terminal modification</para>
        /// </summary>
        public virtual char Site { get { return site; } }

        private int offset;
        private char site;
    }

    public class Entity<T> : IEquatable<T> where T : Entity<T>, new()
    {
        public virtual long? Id { get; set; }
        protected virtual int Version { get; set; }

        public override bool Equals (Object o)
        {
            if (Id == null)
            {
                return base.Equals(o);
            }
            Entity<T> that = o as Entity<T>;
            if (that == null)
            {
                return false;
            }
            return Equals(Id, that.Id);
        }

        public virtual bool Equals (T o) { return Equals((object) o); }

        public override int GetHashCode ()
        {
            if (Id == null)
            {
                return base.GetHashCode();
            }
            return Id.GetHashCode() ^ typeof(T).GetHashCode();
        }
    }

    #region Implementation for custom types
    
    public class SpectrumSourceMetadataPathUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble (object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is byte[]))
                throw new ArgumentException();

            var msdataBytes = cached as byte[];
            string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
            File.WriteAllBytes(tempFilepath, msdataBytes);
            return tempFilepath;
        }

        public object DeepCopy (object value)
        {
            if (value == null)
                return null;
            var msd = value as pwiz.CLI.msdata.MSData;
            return msd;
        }

        public object Disassemble (object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is pwiz.CLI.msdata.MSData))
                throw new ArgumentException();

            var msd = value as pwiz.CLI.msdata.MSData;
            throw new NotImplementedException();
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet (System.Data.IDataReader rs, string[] names, object owner)
        {
            return Assemble(rs.GetValue(0), owner);
        }

        public void NullSafeSet (System.Data.IDbCommand cmd, object value, int index)
        {
            throw new NotImplementedException();
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(pwiz.CLI.msdata.MSData); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetBinaryBlob(1) }; }
        }

        public bool IsMutable
        {
            get { return true; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    /*public class SpectrumPeakListUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble (object cached, object owner)
        {
            if (!(cached is byte[]))
                throw new ArgumentException();
            var peakListBytes = cached as byte[];
            var stream = new System.IO.MemoryStream(peakListBytes, false);
            var reader = new System.IO.BinaryReader(stream);
            long peakCount = stream.Length / (sizeof(double) + sizeof(double));
            var peakList = new Spectrum.Peak[peakCount];
            for (long i = 0; i < peakCount; ++i)
            {
                peakList[i].MZ = reader.ReadDouble();
                peakList[i].Intensity = reader.ReadDouble();
            }
            return peakList;
        }

        public object DeepCopy (object value)
        {
            if ( value == null )
            return null;
            var peakList = value as Spectrum.Peak[];
            var peakListCopy = new Spectrum.Peak[peakList.LongLength];
            peakList.CopyTo(peakListCopy, 0);
            return peakListCopy;
        }

        public object Disassemble (object value)
        {
            if (!(value is Spectrum.Peak[]))
                throw new ArgumentException();
            var peakList = value as Spectrum.Peak[];
            var stream = new System.IO.MemoryStream();
            var writer = new System.IO.BinaryWriter(stream);
            for (long i = 0; i < peakList.LongLength; ++i)
            {
                writer.Write(peakList[i].MZ);
                writer.Write(peakList[i].Intensity);
            }
            writer.Close();
            return stream.ToArray();
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet (System.Data.IDataReader rs, string[] names, object owner)
        {
            return Assemble(rs.GetValue(5), owner);
        }

        public void NullSafeSet (System.Data.IDbCommand cmd, object value, int index)
        {
            throw new NotImplementedException();
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(Spectrum.Peak[]); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetBinaryBlob(50 * 2 * sizeof(double)) }; }
        }

        public bool IsMutable
        {
            get { return true; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class Reader_IDPickerDB : pwiz.CLI.msdata.Reader
    {
        public override string identify (string filename, string head)
        {
            if (filename.ToLower().EndsWith(".idpdb"))
                return "IDPicker DB";
            return String.Empty;
        }

        public override void read (string filename, string head, pwiz.CLI.msdata.MSData result)
        {
            read(filename, head, result, 0);
        }

        public override void read (string filename, string head, pwiz.CLI.msdata.MSData result, int runIndex)
        {
            var session = SessionFactoryFactory.CreateSessionFactory(filename, false, false).OpenSession();
            result = session.QueryOver<SpectrumSource>().List()[runIndex].Metadata;
        }

        public override void read (string filename, string head, pwiz.CLI.msdata.MSDataList results)
        {
            var session = SessionFactoryFactory.CreateSessionFactory(filename, false, false).OpenSession();
            foreach (SpectrumSource ss in session.QueryOver<SpectrumSource>().List())
                results.Add(ss.Metadata);
        }

        public override string[] readIds (string filename, string head)
        {
            var session = SessionFactoryFactory.CreateSessionFactory(filename, false, false).OpenSession();
            return session.QueryOver<SpectrumSource>().Select(ss => ss.Name).List<string>().ToArray();
        }
    }

    public class SpectrumList_IDPickerDB : pwiz.CLI.msdata.SpectrumList
    {
        public SpectrumList_IDPickerDB (NHibernate.ISession session, SpectrumSource spectrumSource)
        {
            this.session = session;
            this.spectrumSource = spectrumSource;
        }

        public override int size ()
        {
            return spectrumSource.Spectra.Count;
        }

        public override pwiz.CLI.msdata.Spectrum spectrum (int index, bool getBinaryData)
        {
            var result = new pwiz.CLI.msdata.Spectrum();
            var spectrum = spectrumSource.Spectra[index];
            result.index = spectrum.Index;
            result.id = spectrum.NativeID;

            if (getBinaryData)
            {
                List<double> mzArray = spectrum.Peaks.Select(p => p.MZ).ToList();
                List<double> intensityArray = spectrum.Peaks.Select(p => p.Intensity).ToList();
                result.setMZIntensityArrays(mzArray, intensityArray);
            }
            else
                result.defaultArrayLength = (ulong) spectrum.Peaks.Count();

            return result;
        }

        public override pwiz.CLI.msdata.SpectrumIdentity spectrumIdentity (int index)
        {
            return base.spectrumIdentity(index);
        }

        public override int find (string id)
        {
            return base.find(id);
        }

        private NHibernate.ISession session;
        private SpectrumSource spectrumSource;
    }*/
    #endregion

    public static class ExtensionMethods
    {
        public static string ToModifiedString (this PeptideSpectrumMatch psm, int precision)
        {
            string format = String.Format("[{{0:f{0}}}]", precision);
            StringBuilder sb = new StringBuilder(psm.Peptide.Sequence);
            foreach (var mod in (from m in psm.Modifications orderby m.Offset descending select m))
                if (mod.Offset == int.MinValue)
                    sb.Insert(0, String.Format(format, mod.Modification.MonoMassDelta));
                else if (mod.Offset == int.MaxValue)
                    sb.AppendFormat(format, mod.Modification.MonoMassDelta);
                else
                    sb.Insert(mod.Offset, String.Format(format, mod.Modification.MonoMassDelta));
            return sb.ToString();
        }

        public static string ToModifiedString (this PeptideSpectrumMatch psm)
        {
            return ToModifiedString(psm, 0);
        }

        public static int GetGroupDepth (this SpectrumSourceGroup group)
        {
            if (group.Name == "/")
                return 0;

            int count = 0;
            foreach (char c in group.Name)
                if (c == '/')
                    ++count;
            return count;
        }

        public static bool IsChildOf (this SpectrumSourceGroup childGroup, SpectrumSourceGroup parentGroup)
        {
            return childGroup.Name.StartsWith(parentGroup.Name);
        }

        public static bool IsImmediateChildOf (this SpectrumSourceGroup childGroup, SpectrumSourceGroup parentGroup)
        {
            return childGroup.IsChildOf(parentGroup) &&
                   childGroup.GetGroupDepth() - 1 == parentGroup.GetGroupDepth();
        }

        public static SpectrumSourceGroup GetImmediateParentGroup (this SpectrumSource source)
        {
            return (from g in source.Groups
                    where g.Group.Name.Length == source.Groups.Max(o => o.Group.Name.Length)
                    select g).First().Group;
        }

        public static string ToProteinIdList (this IList<PeptideInstance> instances)
        {
            return String.Format("({0})", String.Join(",", (from pi in instances select pi.Protein.Id.ToString()).ToArray()));
        }
    }
}