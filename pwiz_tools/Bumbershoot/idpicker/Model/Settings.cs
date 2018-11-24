//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Globalization;
using NHibernate;
using NHibernate.Linq;
using Iesi.Collections.Generic;
using NHibernate.Engine;
using pwiz.Common.Collections;
using Color = System.Drawing.Color;
using XmlSerializer = System.Runtime.Serialization.NetDataContractSerializer;
using SortOrder = System.Windows.Forms.SortOrder;
using MZTolerance = pwiz.CLI.chemistry.MZTolerance;

namespace IDPicker.DataModel
{
    public class About : Entity<About>
    {
        public virtual AnalysisSoftware Software { get; set; }
        public virtual DateTime StartTime { get; set; }
        public virtual int SchemaRevision { get; set; }
    }

    public class QonverterSettings : Entity<QonverterSettings>
    {
        /// <summary>
        /// The analysis to which this entity is associated.
        /// </summary>
        public virtual Analysis Analysis { get; set; }

        /// <summary>
        /// The algorithm to use for qonversion, i.e.
        /// static-weighted, Monte Carlo optimization, or Percolator optimization.
        /// </summary>
        public virtual Qonverter.QonverterMethod QonverterMethod { get; set; }

        /// <summary>
        /// The prefix on a protein accession which designates the protein as a decoy.
        /// </summary>
        public virtual string DecoyPrefix { get; set; }

        /// <summary>
        /// If true, the Qonverter will rerank PSMs on a per-spectrum basis based on the PSMs' total scores,
        /// which will likely be different from the original score(s) used for ranking.
        /// </summary>
        public virtual bool RerankMatches { get; set; }

        public virtual Qonverter.Kernel Kernel { get; set; }
        public virtual Qonverter.MassErrorHandling MassErrorHandling { get; set; }
        public virtual Qonverter.MissedCleavagesHandling MissedCleavagesHandling { get; set; }
        public virtual Qonverter.TerminalSpecificityHandling TerminalSpecificityHandling { get; set; }
        public virtual Qonverter.ChargeStateHandling ChargeStateHandling { get; set; }

        /// <summary>
        /// A description of scores keyed by score name.
        /// </summary>
        public virtual IDictionary<string, Qonverter.Settings.ScoreInfo> ScoreInfoByName { get; set; }

        public virtual double MaxFDR { get; set; }

        /// <summary>
        /// Converts a QonverterSettings entity to a Qonverter.Settings instance.
        /// </summary>
        public virtual Qonverter.Settings ToQonverterSettings ()
        {
            return new Qonverter.Settings()
            {
                QonverterMethod = QonverterMethod,
                DecoyPrefix = DecoyPrefix,
                RerankMatches = RerankMatches,
                Kernel = Kernel,
                MassErrorHandling = MassErrorHandling,
                MissedCleavagesHandling = MissedCleavagesHandling,
                TerminalSpecificityHandling = TerminalSpecificityHandling,
                ChargeStateHandling = ChargeStateHandling,
                MaxFDR = MaxFDR,
                ScoreInfoByName = ScoreInfoByName
            };
        }

        #region Load/Save QonverterSettings properties
        /// <summary>
        /// Loads QonverterSettings from user settings.
        /// </summary>
        /// <returns>Returns QonverterSettings configurations keyed by preset name.</returns>
        public static IDictionary<string, QonverterSettings> LoadQonverterSettings ()
        {
            IDictionary<string, QonverterSettings> qonverterSettingsByName = null;

            try
            {
                qonverterSettingsByName = parseQonverterSettings(Properties.Settings.Default.QonverterSettings);
            }
            catch
            {
            }

            if (qonverterSettingsByName == null || qonverterSettingsByName.Count == 0)
                qonverterSettingsByName = parseQonverterSettings(Properties.Settings.Default.DefaultQonverterSettings);

            return qonverterSettingsByName;
        }

        /// <summary>
        /// Saves QonverterSettings to user settings.
        /// </summary>
        /// <param name="qonverterSettingsByName">The QonverterSettings configurations (keyed by preset name) to save.</param>
        public static void SaveQonverterSettings (IDictionary<string, QonverterSettings> qonverterSettingsByName)
        {
            var qonverterSettingsCollection = new StringCollection();
            foreach (var kvp in qonverterSettingsByName)
                qonverterSettingsCollection.Add(String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}",
                                                              kvp.Key,
                                                              kvp.Value.QonverterMethod,
                                                              kvp.Value.RerankMatches,
                                                              kvp.Value.Kernel,
                                                              kvp.Value.MassErrorHandling,
                                                              kvp.Value.MissedCleavagesHandling,
                                                              kvp.Value.TerminalSpecificityHandling,
                                                              kvp.Value.ChargeStateHandling,
                                                              kvp.Value.MaxFDR.ToString(CultureInfo.InvariantCulture),
                                                              assembleScoreInfo(kvp.Value.ScoreInfoByName)));

            Properties.Settings.Default.QonverterSettings = qonverterSettingsCollection;
            Properties.Settings.Default.Save();
        }

        internal static string assembleScoreInfo (IDictionary<string, Qonverter.Settings.ScoreInfo> scoreInfoByName)
        {
            // "Weight Order NormalizationMethod ScoreName"
            // The score name is last because it can have spaces in it (and many other potential delimiters).
            var scoreInfoStrings = new List<string>();
            foreach (var scoreInfoPair in scoreInfoByName)
                scoreInfoStrings.Add(String.Format("{0} {1} {2} {3}",
                                                   scoreInfoPair.Value.Weight.ToString(CultureInfo.InvariantCulture),
                                                   scoreInfoPair.Value.Order,
                                                   scoreInfoPair.Value.NormalizationMethod,
                                                   scoreInfoPair.Key));
            return String.Join(";", scoreInfoStrings.ToArray());
        }

        internal static void parseScoreInfo (IEnumerable<string> scoreInfoStrings, IDictionary<string, Qonverter.Settings.ScoreInfo> scoreInfoByName)
        {
            // "Weight Order NormalizationMethod ScoreName"
            // The score name is last because it can have spaces in it (and many other potential delimiters).
            foreach(var scoreInfoString in scoreInfoStrings)
            {
                if (string.IsNullOrEmpty(scoreInfoString))
                    continue;

                string[] scoreInfoTokens = scoreInfoString.Trim().Split(' ');
                var weight = Convert.ToDouble(scoreInfoTokens[0], CultureInfo.InvariantCulture);
                var order = (Qonverter.Settings.Order) Enum.Parse(typeof(Qonverter.Settings.Order), scoreInfoTokens[1]);
                var normalizationMethod = (Qonverter.Settings.NormalizationMethod) Enum.Parse(typeof(Qonverter.Settings.NormalizationMethod), scoreInfoTokens[2]);
                var name = String.Join(" ", scoreInfoTokens, 3, scoreInfoTokens.Length - 3);

                scoreInfoByName[name] = new Qonverter.Settings.ScoreInfo()
                {
                    Weight = weight,
                    Order = order,
                    NormalizationMethod = normalizationMethod
                };
            }
        }

        private static IDictionary<string, QonverterSettings> parseQonverterSettings (StringCollection serializedSettings)
        {
            var qonverterSettingsByName = new Dictionary<string, QonverterSettings>();

            if (serializedSettings == null)
                return qonverterSettingsByName;

            // Zero or more strings like:
            // SettingsName;QonverterMethod;RerankMatches;
            // Kernel;MassErrorHandling;MissedCleavagesHandling;TerminalSpecificityHandling;ChargeStateHandling;
            // ScoreInfo1;ScoreInfo2;...etc... (no line breaks)
            foreach (var row in serializedSettings)
            {
                string[] tokens = row.Split(';');

                var qonverterSettings = qonverterSettingsByName[tokens[0]] = new QonverterSettings()
                {
                    QonverterMethod = (Qonverter.QonverterMethod) Enum.Parse(typeof(Qonverter.QonverterMethod), tokens[1]),
                    RerankMatches = Convert.ToBoolean(tokens[2]),
                    Kernel = (Qonverter.Kernel) Enum.Parse(typeof(Qonverter.Kernel), tokens[3]),
                    MassErrorHandling = (Qonverter.MassErrorHandling) Enum.Parse(typeof(Qonverter.MassErrorHandling), tokens[4]),
                    MissedCleavagesHandling = (Qonverter.MissedCleavagesHandling) Enum.Parse(typeof(Qonverter.MissedCleavagesHandling), tokens[5]),
                    TerminalSpecificityHandling = (Qonverter.TerminalSpecificityHandling) Enum.Parse(typeof(Qonverter.TerminalSpecificityHandling), tokens[6]),
                    ChargeStateHandling = (Qonverter.ChargeStateHandling) Enum.Parse(typeof(Qonverter.ChargeStateHandling), tokens[7]),
                    MaxFDR = Convert.ToDouble(tokens[8], CultureInfo.InvariantCulture),
                    ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                };

                // The rest of the tokens are score info
                parseScoreInfo(tokens.Skip(9), qonverterSettings.ScoreInfoByName);
            }

            return qonverterSettingsByName;
        }
        #endregion
    }

    public enum GroupBy { Cluster, ProteinGroup, Peptide, PeptideGroup, Source, Spectrum, Analysis, Charge, GeneFamily, GeneGroup, Gene, Chromosome, Off }
    public enum PivotBy { SpectraByGroup, SpectraBySource, MatchesByGroup, MatchesBySource, PeptidesByGroup, PeptidesBySource, iTRAQByGroup, iTRAQBySource, TMTByGroup, TMTBySource, MS1IntensityByGroup, MS1IntensityBySource, Off }

    [Serializable]
    public class Grouping : IComparable<Grouping>
    {
        public GroupBy Mode { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int CompareTo (Grouping other) { return Mode.CompareTo(other.Mode); }
    }

    [Serializable]
    public class Pivot : IComparable<Pivot>
    {
        public PivotBy Mode { get; set; }
        public string Name { get; set; }
        public int CompareTo (Pivot other) { return Mode.CompareTo(other.Mode); }
    }
    
    [Serializable]
    public class SortColumn : IComparable<SortColumn>
    {
        public int Index { get; set; }
        public SortOrder Order { get; set; }
        public int CompareTo (SortColumn other) { return Index.CompareTo(other.Index); }
    }

    [Serializable]
    public class FormProperty : IComparable<FormProperty>
    {
        public string Name { get; set; }
        public Color? ForeColor { get; set; }
        public Color? BackColor { get; set; }
        public List<Grouping> GroupingModes { get; set; }
        public List<Pivot> PivotModes { get; set; }
        public List<SortColumn> SortColumns { get; set; }
        public List<ColumnProperty> ColumnProperties { get; set; }

        public int CompareTo (FormProperty rhs)
        {
            int result = Name.CompareTo(rhs.Name);
            if (result != 0) return result;
            result = (ForeColor ?? Color.Black).ToArgb().CompareTo((rhs.ForeColor ?? Color.Black).ToArgb());
            if (result != 0) return result;
            result = (BackColor ?? Color.Black).ToArgb().CompareTo((rhs.BackColor ?? Color.Black).ToArgb());
            if (result != 0) return result;
            result = GroupingModes.SequenceCompareTo(rhs.GroupingModes);
            if (result != 0) return result;
            result = PivotModes.SequenceCompareTo(rhs.PivotModes);
            if (result != 0) return result;
            result = SortColumns.SequenceCompareTo(rhs.SortColumns);
            if (result != 0) return result;
            return ColumnProperties.SequenceCompareTo(rhs.ColumnProperties);
        }
    }

    [Serializable]
    public class ColumnProperty : IComparable<ColumnProperty>
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public int DisplayIndex { get; set; }
        public Type Type { get; set; }
        public int? Precision { get; set; }
        public bool? Visible { get; set; }
        public Color? ForeColor { get; set; }
        public Color? BackColor { get; set; }

        public int CompareTo (ColumnProperty rhs)
        {
            int result = Name.CompareTo(rhs.Name); 
            if (result != 0) return result;
            result = Index.CompareTo(rhs.Index);
            if (result != 0) return result;
            result = DisplayIndex.CompareTo(rhs.DisplayIndex);
            if (result != 0) return result;
            result = Type.Name.CompareTo(rhs.Type.Name);
            if (result != 0) return result;
            result = (Precision ?? 0).CompareTo(rhs.Precision ?? 0);
            if (result != 0) return result;
            result = (Visible ?? false).CompareTo(rhs.Visible ?? false);
            if (result != 0) return result;
            result = (ForeColor ?? Color.Black).ToArgb().CompareTo((rhs.ForeColor ?? Color.Black).ToArgb());
            if (result != 0) return result;
            return (BackColor ?? Color.Black).ToArgb().CompareTo((rhs.BackColor ?? Color.Black).ToArgb());
        }
    }

    public class LayoutProperty : Entity<LayoutProperty>
    {
        public virtual string Name { get; set; }
        public virtual string PaneLocations { get; set; }
        public virtual bool HasCustomColumnSettings { get; set; }
        public virtual IDictionary<string, FormProperty> FormProperties { get; set; }
    }

    #region Implementation for custom types

    public class ScoreInfoUserType : NHibernate.UserTypes.IUserType
    {
        public object Assemble (object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is string))
                throw new ArgumentException();

            var scoreInfo = cached as string;
            var scoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>();
            QonverterSettings.parseScoreInfo(scoreInfo.Split(';'), scoreInfoByName);
            return scoreInfoByName;
        }

        public object DeepCopy (object value)
        {
            if (value == null)
                return null;
            return new Dictionary<string, Qonverter.Settings.ScoreInfo>(value as Dictionary<string, Qonverter.Settings.ScoreInfo>);
        }

        public object Disassemble (object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is IDictionary<string, Qonverter.Settings.ScoreInfo>))
                throw new ArgumentException();

            return QonverterSettings.assembleScoreInfo(value as IDictionary<string, Qonverter.Settings.ScoreInfo>);
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        {
            return Assemble(rs.GetValue(rs.GetOrdinal(names[0])), owner);
        }

        public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(IDictionary<string, Qonverter.Settings.ScoreInfo>); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetString(1) }; }
        }

        public bool IsMutable
        {
            get { return true; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var scoreInfoByName1 = x as IDictionary<string, Qonverter.Settings.ScoreInfo>;
            var scoreInfoByName2 = y as IDictionary<string, Qonverter.Settings.ScoreInfo>;

            foreach (var kvp in scoreInfoByName1)
            {
                if (scoreInfoByName2.ContainsKey(kvp.Key))
                {
                    var scoreInfo = scoreInfoByName2[kvp.Key];
                    if (scoreInfo.Weight != kvp.Value.Weight ||
                        scoreInfo.Order != kvp.Value.Order ||
                        scoreInfo.NormalizationMethod != kvp.Value.NormalizationMethod)
                        return false;
                }
                else
                    return false;
            }
            return true;
        }
    }

    public class FormPropertiesUserType : NHibernate.UserTypes.IUserType
    {
        public object Assemble (object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is string))
                throw new ArgumentException();

            var serializedList = cached as string;
            var formatter = new XmlSerializer();
            return formatter.Deserialize(new MemoryStream(Encoding.UTF8.GetBytes(serializedList)));
        }

        public object DeepCopy (object value)
        {
            if (value == null)
                return null;
            return new Dictionary<string, FormProperty>(value as Dictionary<string, FormProperty>);
        }

        public object Disassemble (object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is IDictionary<string, FormProperty>))
                throw new ArgumentException();

            using (var stream = new MemoryStream())
            {
                var formatter = new XmlSerializer();
                formatter.Serialize(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        {
            return Assemble(rs.GetValue(rs.GetOrdinal(names[0])), owner);
        }

        public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(IDictionary<string, FormProperty>); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetString(0) }; }
        }

        public bool IsMutable
        {
            get { return true; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var a = x as IDictionary<string, FormProperty>;
            var b = y as IDictionary<string, FormProperty>;

            foreach (var kvp in a)
                if (!b.ContainsKey(kvp.Key) || !(kvp.Value == b[kvp.Key]))
                    return false;
            return true;
        }
    }

    public class DistinctMatchFormatUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble(object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is string))
                throw new ArgumentException();

            var serializedString = cached as string;
            string[] serializedTokens = serializedString.Split(' ');

            var result = new DistinctMatchFormat
            {
                IsChargeDistinct = Convert.ToInt32(serializedTokens[0]) != 0 ? true : false,
                IsAnalysisDistinct = Convert.ToInt32(serializedTokens[1]) != 0 ? true : false,
                AreModificationsDistinct = Convert.ToInt32(serializedTokens[2]) != 0 ? true : false,
                ModificationMassRoundToNearest = Convert.ToDecimal(serializedTokens[3])
            };

            return result;
        }

        public object DeepCopy(object value)
        {
            if (value == null)
                return null;
            return value as DistinctMatchFormat;
        }

        public object Disassemble(object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is DistinctMatchFormat))
                throw new ArgumentException();

            var dmf = value as DistinctMatchFormat;
            return String.Join(" ", dmf.IsChargeDistinct ? 1 : 0, dmf.IsAnalysisDistinct ? 1 : 0, dmf.AreModificationsDistinct ? 1 : 0, dmf.ModificationMassRoundToNearest);
        }

        public int GetHashCode(object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        {
            return Assemble(rs.GetValue(rs.GetOrdinal(names[0])), owner);
        }

        public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace(object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(DistinctMatchFormat); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetString(1) }; }
        }

        public bool IsMutable
        {
            get { return false; }
        }

        bool NHibernate.UserTypes.IUserType.Equals(object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var dmf1 = x as DistinctMatchFormat;
            var dmf2 = y as DistinctMatchFormat;
            return dmf1 == dmf2;
        }

        #endregion
    }

    public class PrecursorMzToleranceUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble(object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is string))
                throw new ArgumentException();

            var serializedString = cached as string;
            if (serializedString.IsNullOrEmpty())
                return null;

            var result = new MZTolerance(serializedString);
            return result;
        }

        public object DeepCopy(object value)
        {
            if (value == null)
                return null;
            return value as MZTolerance;
        }

        public object Disassemble(object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is MZTolerance))
                throw new ArgumentException();

            var tolerance = value as MZTolerance;
            return String.Format("{0}{1}", tolerance.value, tolerance.units.ToString().ToLowerInvariant());
        }

        public int GetHashCode(object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        {
            return Assemble(rs.GetValue(rs.GetOrdinal(names[0])), owner);
        }

        public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace(object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(MZTolerance); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetString(1) }; }
        }

        public bool IsMutable
        {
            get { return false; }
        }

        bool NHibernate.UserTypes.IUserType.Equals(object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var t1 = x as MZTolerance;
            var t2 = y as MZTolerance;
            return t1 == t2;
        }

        #endregion
    }
    #endregion
}