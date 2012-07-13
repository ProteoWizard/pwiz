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
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using BrightIdeasSoftware;
using PopupControl;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class SpectrumTableForm : BaseTableForm
    {
        #region Wrapper classes for encapsulating query results

        public class AggregateRow : Row
        {
            public int PeptideSpectrumMatches { get; private set; }
            public int Sources { get; private set; }
            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public int DistinctAnalyses { get; private set; }
            public int DistinctCharges { get; private set; }
            public int ProteinGroups { get; private set; }

            public static int ColumnCount = 8;
            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.Id), " +
                                             "COUNT(DISTINCT psm.Spectrum.Source.id), " +
                                             "COUNT(DISTINCT psm.Spectrum.id), " +
                                             "COUNT(DISTINCT psm.DistinctMatchKey), " +
                                             "COUNT(DISTINCT psm.Peptide.id), " +
                                             "COUNT(DISTINCT psm.Analysis.id), " +
                                             "COUNT(DISTINCT psm.Charge), " +
                                             "COUNT(DISTINCT pro.ProteinGroup)";

            #region Constructor
            public AggregateRow(object[] queryRow, DataFilter dataFilter)
            {
                int column = -1;
                PeptideSpectrumMatches = Convert.ToInt32(queryRow[++column]);
                Sources = Convert.ToInt32(queryRow[++column]);
                Spectra = Convert.ToInt32(queryRow[++column]);
                DistinctMatches = Convert.ToInt32(queryRow[++column]);
                DistinctPeptides = Convert.ToInt32(queryRow[++column]);
                DistinctAnalyses = Convert.ToInt32(queryRow[++column]);
                DistinctCharges = Convert.ToInt32(queryRow[++column]);
                ProteinGroups = Convert.ToInt32(queryRow[++column]);
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class SpectrumSourceGroupRow : AggregateRow
        {
            public DataModel.SpectrumSourceGroup SpectrumSourceGroup { get; private set; }

            #region Constructor
            public SpectrumSourceGroupRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                SpectrumSourceGroup = (queryRow[++column] as DataModel.SpectrumSourceGroupLink).Group;
            }
            #endregion
        }

        public class SpectrumSourceRow : AggregateRow
        {
            public DataModel.SpectrumSource SpectrumSource { get; private set; }

            #region Constructor
            public SpectrumSourceRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                SpectrumSource = (DataModel.SpectrumSource) queryRow[++column];
            }
            #endregion
        }

        public class AnalysisRow : AggregateRow
        {
            public DataModel.Analysis Analysis { get; private set; }
            public string Key { get { return Analysis.Name; } }

            #region Constructor
            public AnalysisRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Analysis = (DataModel.Analysis) queryRow[++column];
            }
            #endregion
        }

        public class ChargeRow : AggregateRow
        {
            public int Charge { get; private set; }

            #region Constructor
            public ChargeRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Charge = Convert.ToInt32(queryRow[++column]);
            }
            #endregion
        }

        public class PeptideRow : AggregateRow
        {
            public DataModel.Peptide Peptide { get; private set; }

            #region Constructor
            public PeptideRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Peptide = (DataModel.Peptide) queryRow[++column];
            }
            #endregion
        }

        public class SpectrumRow : AggregateRow
        {
            public DataModel.Spectrum Spectrum { get; private set; }
            public DataModel.SpectrumSource Source { get; private set; }
            public DataModel.SpectrumSourceGroup Group { get; private set; }
            public string Key { get; private set; }

            #region Constructor
            public SpectrumRow(object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Spectrum = (DataModel.Spectrum) queryRow[++column];
                Source = (DataModel.SpectrumSource) queryRow[++column];
                Group = (DataModel.SpectrumSourceGroup) queryRow[++column];

                Key = Spectrum.NativeID;

                // try to abbreviate, e.g. "controllerType=0 controllerNumber=1 scan=123" -> "0.1.123"
                try { Key = pwiz.CLI.msdata.id.abbreviate(Key); }
                catch { }

                // if not grouping by Source, prepend Spectrum.Source to the NativeID
                if (checkedGroupings.Count(o => o.Mode == GroupBy.Source) == 0)
                    Key = (Group.Name + "/" + Source.Name + "/" + Key).Replace("//", "/");
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow : Row
        {
            public string Key { get; private set; }

            // could be from UnfilteredPeptideSpectrumMatch, so we can't store the entity directly
            public long PeptideSpectrumMatchId { get; private set; }
            public int Rank { get; private set; }
            public int Charge { get; private set; }
            public double ExactMass { get; private set; }
            public double ObservedMass { get; private set; }
            public double QValue { get; private set; }
            public string ModifiedSequence { get; private set; }

            public DataModel.Spectrum Spectrum { get; private set; }
            public DataModel.SpectrumSource Source { get; private set; }
            public DataModel.SpectrumSourceGroup Group { get; private set; }
            public DataModel.Analysis Analysis { get; private set; }

            private static string SqlQueryFormat =
                "SELECT {{s.*}}, {{ss.*}}, {{ssg.*}}, {{a.*}}," +
                "       psm.Id, Rank, Charge, QValue, psm.ObservedNeutralMass," +
                "       psm.MonoisotopicMassError, psm.MolecularWeightError," +
                "       IFNULL(dm.DistinctMatchKey, " +
                "              (SELECT GROUP_CONCAT(DISTINCT ROUND(mod.MonoMassDelta, 4) || '@' || pm.Offset)" +
                "               FROM PeptideModification pm" +
                "               JOIN Modification mod ON pm.Modification=mod.Id" +
                "               WHERE pm.PeptideSpectrumMatch=psm.Id)) AS DistinctMatchKey," +
                "       IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), DecoySequence) AS Sequence " +
                "FROM UnfilteredPeptideSpectrumMatch psm " +
                "JOIN UnfilteredPeptide pep ON psm.Peptide=pep.Id " +
                "JOIN UnfilteredPeptideInstance pi ON pep.Id=pi.Peptide " +
                "JOIN UnfilteredProtein pro ON pi.Protein=pro.Id " +
                "LEFT JOIN ProteinData pd ON pi.Protein=pd.Id " +
                "LEFT JOIN PeptideModification pm ON psm.Id=pm.PeptideSpectrumMatch " +
                "LEFT JOIN Modification mod ON pm.Modification=mod.Id " +
                "LEFT JOIN DistinctMatch dm ON psm.Id=dm.PsmId " +
                "JOIN UnfilteredSpectrum s ON psm.Spectrum=s.Id " +
                "JOIN SpectrumSource ss ON s.Source=ss.Id " +
                "JOIN SpectrumSourceGroup ssg ON ss.Group_=ssg.Id " +
                "JOIN Analysis a ON psm.Analysis=a.Id " +
                "{0} " +
                "GROUP BY psm.Id " +
                "ORDER BY Rank, QValue";

            private static string SqlQueryCount =
                "SELECT COUNT(DISTINCT psm.Id) " +
                "FROM UnfilteredPeptideSpectrumMatch psm " +
                "JOIN UnfilteredPeptide pep ON psm.Peptide=pep.Id " +
                "JOIN UnfilteredPeptideInstance pi ON pep.Id=pi.Peptide " +
                "JOIN UnfilteredProtein pro ON pi.Protein=pro.Id " +
                "LEFT JOIN PeptideModification pm ON psm.Id=pm.PeptideSpectrumMatch " +
                "LEFT JOIN Modification mod ON pm.Modification=mod.Id " +
                "LEFT JOIN DistinctMatch dm ON psm.Id=dm.PsmId " +
                "JOIN UnfilteredSpectrum s ON psm.Spectrum=s.Id " +
                "JOIN SpectrumSource ss ON s.Source=ss.Id " +
                "JOIN SpectrumSourceGroup ssg ON ss.Group_=ssg.Id " +
                "JOIN Analysis a ON psm.Analysis=a.Id ";

            private static string HqlQueryFormat =
                "SELECT s, ss, ssg, a," +
                "       psm.Id, psm.Rank, psm.Charge, psm.QValue, psm.ObservedNeutralMass," +
                "       psm.MonoisotopicMassError, psm.MolecularWeightError," +
                "       psm.DistinctMatchKey," +
                "       pep.Sequence ";

            public static NHibernate.IQuery GetQuery(NHibernate.ISession session, DataFilter dataFilter)
            {
                if (dataFilter.Spectrum != null && dataFilter.Spectrum.Count == 1 ||
                    dataFilter.Peptide != null && dataFilter.Peptide.Count == 1 ||
                    dataFilter.DistinctMatchKey != null && dataFilter.DistinctMatchKey.Count == 1)
                {
                    var basicFilter = new DataFilter(dataFilter)
                    {
                        AminoAcidOffset = null,
                        Cluster = null,
                        Protein = null
                    };

                    if (dataFilter.Spectrum != null)
                    {
                        basicFilter.Modifications = null;
                        basicFilter.ModifiedSite = null;
                    }

                    string sql = String.Format(SqlQueryFormat, basicFilter.GetFilteredSqlWhereClause());
                    return session.CreateSQLQuery(sql)
                        .AddEntity("s", typeof (Spectrum))
                        .AddEntity("ss", typeof (SpectrumSource))
                        .AddEntity("ssg", typeof (SpectrumSourceGroup))
                        .AddEntity("a", typeof (Analysis))
                        .AddScalar("Id", NHibernate.NHibernateUtil.Int64)
                        .AddScalar("Rank", NHibernate.NHibernateUtil.Int32)
                        .AddScalar("Charge", NHibernate.NHibernateUtil.Int32)
                        .AddScalar("QValue", NHibernate.NHibernateUtil.Double)
                        .AddScalar("ObservedNeutralMass", NHibernate.NHibernateUtil.Double)
                        .AddScalar("MonoisotopicMassError", NHibernate.NHibernateUtil.Double)
                        .AddScalar("MolecularWeightError", NHibernate.NHibernateUtil.Double)
                        .AddScalar("DistinctMatchKey", NHibernate.NHibernateUtil.String)
                        .AddScalar("Sequence", NHibernate.NHibernateUtil.String);
                }
                
                string hql = HqlQueryFormat +
                             dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                               DataFilter.PeptideSpectrumMatchToPeptideInstance,
                                                               DataFilter.PeptideSpectrumMatchToModification,
                                                               DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup,
                                                               DataFilter.PeptideSpectrumMatchToAnalysis) +
                             "GROUP BY psm.Id";
                return session.CreateQuery(hql);
            }

            public static int GetQueryCount(NHibernate.ISession session, DataFilter dataFilter)
            {
                if (dataFilter.Spectrum != null && dataFilter.Spectrum.Count == 1)
                {
                    dataFilter = new DataFilter(dataFilter)
                    {
                        AminoAcidOffset = null,
                        Modifications = null,
                        ModifiedSite = null,
                        Cluster = null,
                        Protein = null
                    };
                }

                string sql = SqlQueryCount + dataFilter.GetFilteredSqlWhereClause();
                lock (session)
                    return Convert.ToInt32(session.CreateSQLQuery(sql).UniqueResult());
            }

            #region Constructor
            public PeptideSpectrumMatchRow(object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
            {
                DataFilter = dataFilter;

                int column = -1;
                Spectrum = (DataModel.Spectrum) queryRow[++column];
                Source = (DataModel.SpectrumSource) queryRow[++column];
                Group = (DataModel.SpectrumSourceGroup) queryRow[++column];
                Analysis = (DataModel.Analysis) queryRow[++column];

                PeptideSpectrumMatchId = Convert.ToInt64(queryRow[++column]);
                Rank = Convert.ToInt32(queryRow[++column]);
                Charge = Convert.ToInt32(queryRow[++column]);
                QValue = Convert.ToDouble(queryRow[++column]);

                ObservedMass = Convert.ToDouble(queryRow[++column]);
                double monoisotopicError = Convert.ToDouble(queryRow[++column]);
                double averageError = Convert.ToDouble(queryRow[++column]);
                if (Math.Abs(monoisotopicError) < Math.Abs(averageError))
                    ExactMass = ObservedMass - monoisotopicError;
                else
                    ExactMass = ObservedMass - averageError;

                string modificationString = (string) queryRow[++column];
                ModifiedSequence = (string) queryRow[++column];

                if (!String.IsNullOrEmpty(modificationString) && modificationString.Contains('@'))
                {
                    // build modified sequence
                    var modifications = modificationString.Split(' ').Last()
                                                          .Split(',')
                                                          .Select(o => o.Split('@'))
                                                          .Select(o => new { Offset = Convert.ToInt32(o[1]), DeltaMass = Convert.ToDouble(o[0]) })
                                                          .OrderByDescending(o => o.Offset);

                    var sb = new StringBuilder(ModifiedSequence);
                    string formatString = "[{0}]";
                    foreach (var mod in modifications)
                        if (mod.Offset == int.MinValue)
                            sb.Insert(0, String.Format(formatString, mod.DeltaMass));
                        else if (mod.Offset == int.MaxValue)
                            sb.AppendFormat(formatString, mod.DeltaMass);
                        else
                            sb.Insert(mod.Offset + 1, String.Format(formatString, mod.DeltaMass));

                    ModifiedSequence = sb.ToString();
                }

                // if not grouping by Spectrum, use Spectrum as the key column
                if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) == 0)
                {
                    Key = Spectrum.NativeID;

                    // try to abbreviate, e.g. "controllerType=0 controllerNumber=1 scan=123" -> "0.1.123"
                    try { Key = pwiz.CLI.msdata.id.abbreviate(Key); }
                    catch { }

                    // if not grouping by Source, prepend Spectrum.Source to the NativeID
                    if (checkedGroupings.Count(o => o.Mode == GroupBy.Source) == 0)
                        Key = (Group.Name + "/" + Source.Name + "/" + Key).Replace("//", "/");
                }
                else
                    Key = Rank.ToString();
            }
            #endregion
        }

        public class PeptideSpectrumMatchScoreRow : Row
        {
            public string Name { get; private set; }
            public double Value { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchScoreRow(object[] queryRow)
            {
                Name = (string) queryRow[0];
                Value = Convert.ToDouble(queryRow[1]);
            }
            #endregion
        }

        struct TotalCounts
        {
            public int Groups;
            public int Sources;
            public long Spectra;
            public int Charges;
            public int Analyses;

            #region Constructor
            public TotalCounts (NHibernate.ISession session, DataFilter dataFilter)
            {
                lock (session)
                {
                    var total = session.CreateQuery("SELECT " +
                                                    "COUNT(DISTINCT psm.Spectrum.Source.Group.id), " +
                                                    "COUNT(DISTINCT psm.Spectrum.Source.id), " +
                                                    "COUNT(DISTINCT psm.Spectrum.id), " +
                                                    "COUNT(DISTINCT psm.Charge.id), " +
                                                    "COUNT(DISTINCT psm.Analysis.id) " +
                                                    dataFilter.GetFilteredQueryString(
                                                        DataFilter.FromPeptideSpectrumMatch,
                                                        DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink))
                        .List<object[]>()[0];

                    Groups = Convert.ToInt32(total[0]);
                    Sources = Convert.ToInt32(total[1]);
                    Spectra = Convert.ToInt64(total[2]);
                    Charges = Convert.ToInt32(total[3]);
                    Analyses = Convert.ToInt32(total[4]);
                }
            }
            #endregion
        }

        #endregion

        #region getChildren functions for each row type

        // returns both groups and sources
        IList<Row> getSpectrumSourceRows (DataFilter parentFilter)
        {
            var nonGroupParentFilterKey = new DataFilterKey(new DataFilter(parentFilter) { SpectrumSourceGroup = null });

            if (!rowsBySource.ContainsKey(nonGroupParentFilterKey))
                lock (session)
                {
                    var groupsFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null };
                    var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " +
                                                     groupsFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                         DataFilter.PeptideSpectrumMatchToProtein,
                                                                                         DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                     "GROUP BY ssgl.Group.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceGroupRow(o, parentFilter))
                        .ToList();

                    var sources = session.CreateQuery(AggregateRow.Selection + ", s.Source " +
                                                      parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                          DataFilter.PeptideSpectrumMatchToProtein,
                                                                                          DataFilter.PeptideSpectrumMatchToSpectrum) +
                                                      "GROUP BY s.Source.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceRow(o, parentFilter))
                        .ToList();

                    rowsBySource[nonGroupParentFilterKey] = groups.Cast<Row>().Concat(sources.Cast<Row>()).ToList();
                }

            var ssgRows = rowsBySource[nonGroupParentFilterKey].Where(o => o is SpectrumSourceGroupRow).Select(o => o as SpectrumSourceGroupRow);
            var ssRows = rowsBySource[nonGroupParentFilterKey].Where(o => o is SpectrumSourceRow).Select(o => o as SpectrumSourceRow);
            var result = Enumerable.Empty<Row>();

            if (parentFilter != null && parentFilter.SpectrumSourceGroup != null)
                foreach (var item in parentFilter.SpectrumSourceGroup)
                    result = result.Concat(ssgRows.Where(o => o.SpectrumSourceGroup.IsImmediateChildOf(item)).Cast<Row>());
            else
                result = ssgRows.Where(o => o.SpectrumSourceGroup.Name == "/").Cast<Row>();

            if (parentFilter != null && parentFilter.SpectrumSourceGroup != null)
            {
                foreach (var item in parentFilter.SpectrumSourceGroup)
                    result = result.Concat(ssRows.Where(o => o.SpectrumSource.Group != null && o.SpectrumSource.Group.Id == item.Id).Cast<Row>());
            }

            return result.ToList();
        }

        IList<Row> getAnalysisRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Analysis " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToProtein) +
                                       "GROUP BY psm.Analysis.id")
                          .List<object[]>()
                          .Select(o => new AnalysisRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getPeptideRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Peptide " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToProtein) +
                                       "GROUP BY psm.Peptide.id")
                          .List<object[]>()
                          .Select(o => new PeptideRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getChargeRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Charge " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToProtein) +
                                       "GROUP BY psm.Charge")
                          .List<object[]>()
                          .Select(o => new ChargeRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getSpectrumRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToProtein,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY s.id " +
                                       "ORDER BY ss.Name, s.Index")
                          .List<object[]>()
                          .Select(o => new SpectrumRow(o, parentFilter, checkedGroupings) as Row)
                          .ToList();
        }

        IList<Row> getPeptideSpectrumMatchRows (DataFilter parentFilter)
        {
            lock (session)
            return PeptideSpectrumMatchRow.GetQuery(session, parentFilter)
                                          .List<object[]>()
                                          .Select(o => new PeptideSpectrumMatchRow(o, parentFilter, checkedGroupings) as Row)
                                          .ToList();
        }

        IList<Row> getPeptideSpectrumMatchScoreRows (PeptideSpectrumMatchRow parentRow)
        {
            long psmId = parentRow.PeptideSpectrumMatchId;
            lock (session)
                return session.CreateSQLQuery("SELECT Name, CAST (Value AS REAL) " +
                                              "FROM PeptideSpectrumMatchScore " +
                                              "JOIN PeptideSpectrumMatchScoreName ON ScoreNameId = Id " +
                                              "WHERE PsmId = " + psmId)
                              .List<object[]>()
                              .Select(o => new PeptideSpectrumMatchScoreRow(o) as Row)
                              .ToList();
        }

        IList<Row> getChildren (Grouping<GroupBy> grouping, DataFilter parentFilter)
        {
            if (grouping == null)
                return getPeptideSpectrumMatchRows(parentFilter);

            switch (grouping.Mode)
            {
                case GroupBy.Source:
                    // if there is no parent grouping, show the root group, else skip it
                    if (parentFilter == dataFilter)
                        return getSpectrumSourceRows(parentFilter);
                    else
                        return getChildren(getSpectrumSourceRows(parentFilter)[0]);

                case GroupBy.Spectrum: return getSpectrumRows(parentFilter);
                case GroupBy.Analysis: return getAnalysisRows(parentFilter);
                case GroupBy.Peptide: return getPeptideRows(parentFilter);
                case GroupBy.Charge: return getChargeRows(parentFilter);
                default: throw new NotImplementedException();
            }
        }

        DataFilter getChildFilter (Row parentRow)
        {
            var parentFilter = parentRow.DataFilter ?? dataFilter;
            var childFilter = new DataFilter(parentFilter);

            if (parentRow is SpectrumSourceGroupRow)
            {
                var row = parentRow as SpectrumSourceGroupRow;
                childFilter.SpectrumSourceGroup = new List<SpectrumSourceGroup>() { row.SpectrumSourceGroup };
            }
            else if (parentRow is SpectrumSourceRow)
            {
                var row = parentRow as SpectrumSourceRow;
                childFilter.SpectrumSourceGroup = null;
                childFilter.SpectrumSource = new List<SpectrumSource>() { row.SpectrumSource };
            }
            else if (parentRow is AnalysisRow)
            {
                var row = parentRow as AnalysisRow;
                childFilter.Analysis = new List<Analysis>() { row.Analysis };
            }
            else if (parentRow is PeptideRow)
            {
                var row = parentRow as PeptideRow;
                childFilter.Peptide = new List<Peptide>() { row.Peptide };
            }
            else if (parentRow is ChargeRow)
            {
                var row = parentRow as ChargeRow;
                childFilter.Charge = new List<int>() { row.Charge };
            }
            else if (parentRow is SpectrumRow)
            {
                var row = parentRow as SpectrumRow;
                childFilter.SpectrumSourceGroup = null;
                childFilter.SpectrumSource = null;
                childFilter.Spectrum = new List<Spectrum>() { row.Spectrum };
            }
            else if (parentRow is AggregateRow)
                throw new NotImplementedException();

            return childFilter;
        }

        protected override IList<Row> getChildren (Row parentRow)
        {
            var childFilter = getChildFilter(parentRow);

            if (parentRow.ChildRows != null)
            {
                // cached rows might be re-sorted below
            }
            else if (parentRow is SpectrumSourceGroupRow)
            {
                parentRow.ChildRows = getSpectrumSourceRows(childFilter);
            }
            else if (parentRow is SpectrumSourceRow)
            {
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is AnalysisRow)
            {
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is PeptideRow)
            {
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is ChargeRow)
            {
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is SpectrumRow)
            {
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Spectrum);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else // PeptideSpectrumMatchRow
                parentRow.ChildRows = getChildren(parentRow as PeptideSpectrumMatchRow);

            if (!sortColumns.IsNullOrEmpty())
            {
                var sortColumn = sortColumns.Last();
                parentRow.ChildRows = parentRow.ChildRows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
            }

            return parentRow.ChildRows;
        }
        #endregion

        public event EventHandler<DataFilter> SpectrumViewFilter;
        public event EventHandler<SpectrumViewVisualizeEventArgs> SpectrumViewVisualize;

        private TotalCounts totalCounts, basicTotalCounts;
        private Dictionary<DataFilterKey, List<Row>> rowsBySource, basicRowsBySource;
        
        DataGridViewColumn[] aggregateColumns, psmColumns;

        public SpectrumTableForm()
        {
            InitializeComponent();

            Text = TabText = "Spectrum View";
            Icon = Properties.Resources.SpectrumViewIcon;

            aggregateColumns = new DataGridViewColumn[]
            {
                distinctPeptidesColumn,
                distinctMatchesColumn,
                filteredSpectraColumn,
                proteinGroupsColumn,
                distinctAnalysesColumn,
                distinctChargesColumn
            };

            psmColumns = new DataGridViewColumn[]
            {
                analysisColumn,
                chargeColumn,
                observedMassColumn,
                exactMassColumn,
                massErrorColumn,
                qvalueColumn,
                sequenceColumn
            };

            var editSourceGroupsButton = new ToolStripButton()
            {
                Text = "Source Grouping",
                Alignment = ToolStripItemAlignment.Right
            };
            editSourceGroupsButton.Click += editGroupsButton_Click;
            toolStrip.Items.Add(editSourceGroupsButton);

            pivotSetupButton.Visible = false;

            SetDefaults();

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;

            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.CellFormatting += treeDataGridView_CellFormatting;
            treeDataGridView.CellMouseClick += treeDataGridView_CellMouseClick;
            //treeDataGridView.CellContentClick += treeDataGridView_CellContentClick;
            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
            treeDataGridView.CellIconNeeded += treeDataGridView_CellIconNeeded;
            treeDataGridView.ChildRowCountNeeded += treeDataGridView_ChildRowCountNeeded;
        }

        private void treeDataGridView_CellIconNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            if (baseRow is SpectrumSourceGroupRow) e.Value = Properties.Resources.XPfolder_closed;
            else if (baseRow is SpectrumSourceRow) e.Value = Properties.Resources.file;
            else if (baseRow is SpectrumRow) e.Value = Properties.Resources.SpectrumIcon;
            else if (baseRow is PeptideSpectrumMatchRow) e.Value = Properties.Resources.PSMIcon;
            else if (baseRow is PeptideRow) e.Value = Properties.Resources.Peptide;
        }

        private void treeDataGridView_ChildRowCountNeeded (object sender, TreeDataGridViewChildRowCountNeededEventArgs e)
        {
            var parentRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            if (parentRow is PeptideSpectrumMatchRow)
            {
                lock (session)
                    parentRow.ChildRows = getPeptideSpectrumMatchScoreRows(parentRow as PeptideSpectrumMatchRow);
                e.ChildRowCount = parentRow.ChildRows.Count;
                return;
            }

            var childFilter = getChildFilter(parentRow);
            e.ChildRowCount = PeptideSpectrumMatchRow.GetQueryCount(session, childFilter);
        }

        private void getChildRowCount (AggregateRow row, Grouping<GroupBy> childGrouping, TreeDataGridViewCellValueEventArgs e)
        {
            if (childGrouping == null)
            {
                if (checkedGroupings.Any(o => o.Mode == GroupBy.Spectrum || o.Mode == GroupBy.Peptide))
                    e.HasChildRows = true;
                else
                    e.ChildRowCount = row.PeptideSpectrumMatches;
            }
            else if (childGrouping.Mode == GroupBy.Source)
            {
                var dataFilter = row.DataFilter;
                if (dataFilter.SpectrumSourceGroup == null)
                {
                    // create a filter from the cached root group for this data filter
                    var nonGroupParentFilterKey = new DataFilterKey(dataFilter);
                    var rootGroup = (getSpectrumSourceRows(dataFilter)[0] as SpectrumSourceGroupRow).SpectrumSourceGroup;
                    dataFilter = new DataFilter(row.DataFilter) { SpectrumSourceGroup = new List<SpectrumSourceGroup>() { rootGroup } };
                }
                e.ChildRowCount = getSpectrumSourceRows(dataFilter).Count;
            }
            else if (childGrouping.Mode == GroupBy.Spectrum)
                e.ChildRowCount = row.Spectra;
            else if (childGrouping.Mode == GroupBy.Analysis)
                e.ChildRowCount = row.DistinctAnalyses;
            else if (childGrouping.Mode == GroupBy.Peptide)
                e.ChildRowCount = row.DistinctPeptides;
            else if (childGrouping.Mode == GroupBy.Charge)
                e.ChildRowCount = row.DistinctCharges;
            else
                throw new NotImplementedException();
        }

        private void treeDataGridView_CellValueNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            Grouping<GroupBy> childGrouping = null;

            if (baseRow is SpectrumSourceGroupRow)
            {
                var row = baseRow as SpectrumSourceGroupRow;
                var nonGroupParentFilterKey = new DataFilterKey(new DataFilter(row.DataFilter) { SpectrumSourceGroup = null });

                var cachedRowsBySource = rowsBySource[nonGroupParentFilterKey];
                e.ChildRowCount = cachedRowsBySource.Where(o => o is SpectrumSourceGroupRow)
                                                    .Select(o => o as SpectrumSourceGroupRow)
                                                    .Count(o => o.SpectrumSourceGroup.IsImmediateChildOf(row.SpectrumSourceGroup));
                e.ChildRowCount += cachedRowsBySource.Where(o => o is SpectrumSourceRow)
                                                     .Select(o => o as SpectrumSourceRow)
                                                     .Count(o => o.SpectrumSource.Group != null && o.SpectrumSource.Group.Id == row.SpectrumSourceGroup.Id);

                if (e.ChildRowCount == 0)
                    throw new InvalidDataException("no child rows for source group");
            }
            else if (baseRow is SpectrumSourceRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
            else if (baseRow is SpectrumRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Spectrum);
            else if (baseRow is AnalysisRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
            else if (baseRow is PeptideRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
            else if (baseRow is ChargeRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);
            else if (baseRow is PeptideSpectrumMatchRow)
                e.HasChildRows = true;

            if (!e.ChildRowCount.HasValue && baseRow is AggregateRow)
                getChildRowCount(baseRow as AggregateRow, childGrouping, e);

            e.Value = getCellValue(e.ColumnIndex, baseRow);
        }

        protected override object getCellValue (int columnIndex, Row baseRow)
        {
            if (baseRow is SpectrumSourceGroupRow)
            {
                var row = baseRow as SpectrumSourceGroupRow;
                if (columnIndex == keyColumn.Index) return Path.GetFileName(row.SpectrumSourceGroup.Name) ?? "/";
            }
            else if (baseRow is SpectrumSourceRow)
            {
                var row = baseRow as SpectrumSourceRow;
                if (columnIndex == keyColumn.Index) return row.SpectrumSource.Name;
            }
            else if (baseRow is SpectrumRow)
            {
                var row = baseRow as SpectrumRow;
                if (columnIndex == keyColumn.Index) return row.Key;
                else if (columnIndex == precursorMzColumn.Index) return row.Spectrum.PrecursorMZ;
                else if (columnIndex == scanTimeColumn.Index) return row.Spectrum.ScanTimeInSeconds / 60.0;
            }
            else if (baseRow is AnalysisRow)
            {
                var row = baseRow as AnalysisRow;
                if (columnIndex == keyColumn.Index) return String.Format("{0} {1}", row.Analysis.Id, row.Analysis.Name);
            }
            else if (baseRow is PeptideRow)
            {
                var row = baseRow as PeptideRow;
                if (columnIndex == keyColumn.Index) return row.Peptide.Sequence;
            }
            else if (baseRow is ChargeRow)
            {
                var row = baseRow as ChargeRow;
                if (columnIndex == keyColumn.Index) return row.Charge;
            }
            else if (baseRow is PeptideSpectrumMatchRow)
            {
                var row = baseRow as PeptideSpectrumMatchRow;
                if (columnIndex == keyColumn.Index) return row.Key;
                else if (columnIndex == observedMassColumn.Index) return row.ObservedMass;
                else if (columnIndex == exactMassColumn.Index) return row.ExactMass;
                else if (columnIndex == massErrorColumn.Index) return row.ObservedMass - row.ExactMass;
                else if (columnIndex == analysisColumn.Index) return String.Format("{0} {1}", row.Analysis.Id, row.Analysis.Name);
                else if (columnIndex == chargeColumn.Index) return row.Charge;
                else if (columnIndex == qvalueColumn.Index) return row.QValue > 1 ? Double.PositiveInfinity : row.QValue;
                else if (columnIndex == sequenceColumn.Index) return row.ModifiedSequence;
                else if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) == 0)
                {
                    if (columnIndex == precursorMzColumn.Index) return row.Spectrum.PrecursorMZ;
                    else if (columnIndex == scanTimeColumn.Index) return row.Spectrum.ScanTimeInSeconds / 60.0;
                }
            }
            else if (baseRow is PeptideSpectrumMatchScoreRow)
            {
                var row = baseRow as PeptideSpectrumMatchScoreRow;
                if (columnIndex == keyColumn.Index) return String.Format("{0} = {1}", row.Name, row.Value);
            }

            if (baseRow is AggregateRow)
            {
                var row = baseRow as AggregateRow;
                if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                else if (columnIndex == proteinGroupsColumn.Index) return row.ProteinGroups;
                else if (columnIndex == distinctChargesColumn.Index) return row.DistinctCharges;
                else if (columnIndex == distinctAnalysesColumn.Index) return row.DistinctAnalyses;
            }

            return null;
        }

        protected override RowFilterState getRowFilterState (Row parentRow)
        {
            bool result = false;
            if (parentRow is SpectrumSourceGroupRow)
            {
                if (viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Contains((parentRow as SpectrumSourceGroupRow).SpectrumSourceGroup);
                result = result || viewFilter.SpectrumSourceGroup == null;
            }
            else if (parentRow is SpectrumSourceRow)
            {
                if (viewFilter.Spectrum != null) result = viewFilter.Spectrum.Select(o => o.Source).Contains((parentRow as SpectrumSourceRow).SpectrumSource);
                if (!result && viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains((parentRow as SpectrumSourceRow).SpectrumSource);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect((parentRow as SpectrumSourceRow).SpectrumSource.Groups.Select(o => o.Group)).Any();
                result = result || viewFilter.SpectrumSourceGroup == null && viewFilter.SpectrumSource == null && viewFilter.Spectrum == null;
            }
            else if (parentRow is SpectrumRow)
            {
                var row = parentRow as SpectrumRow;
                if (viewFilter.Spectrum != null) result = viewFilter.Spectrum.Contains(row.Spectrum);
                if (!result && viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains(row.Spectrum.Source);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect(row.Spectrum.Source.Groups.Select(o => o.Group)).Any();
                result = result || viewFilter.SpectrumSourceGroup == null && viewFilter.SpectrumSource == null && viewFilter.Spectrum == null;
            }
            else if (parentRow is PeptideSpectrumMatchRow)
            {
                var row = parentRow as PeptideSpectrumMatchRow;
                if (row.Rank > 1 || row.QValue > 1) return RowFilterState.Out;
                if (row.QValue > dataFilter.MaximumQValue) return RowFilterState.Partial;
                if (viewFilter.Spectrum != null) result = viewFilter.Spectrum.Contains(row.Spectrum);
                if (!result && viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains(row.Spectrum.Source);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect(row.Spectrum.Source.Groups.Select(o => o.Group)).Any();
                result = result || viewFilter.SpectrumSourceGroup == null && viewFilter.SpectrumSource == null && viewFilter.Spectrum == null;
            }
            else if (parentRow is AnalysisRow)
            {
                if (viewFilter.Analysis != null) result = viewFilter.Analysis.Contains((parentRow as AnalysisRow).Analysis);
                else result = true;
            }
            else if (parentRow is ChargeRow)
            {
                if (viewFilter.Charge != null) result = viewFilter.Charge.Contains((parentRow as ChargeRow).Charge);
                else result = true;
            }
            else if (parentRow is PeptideRow)
            {
                if (viewFilter.Peptide != null) result = viewFilter.Peptide.Contains((parentRow as PeptideRow).Peptide);
                else result = true;
            }
            else if (parentRow is PeptideSpectrumMatchScoreRow)
                return RowFilterState.In;

            if (result) return RowFilterState.In;
            if (parentRow.ChildRows == null) return RowFilterState.Out;

            return parentRow.ChildRows.Aggregate(RowFilterState.Unknown, (x, y) => x | getRowFilterState(y));
        }

        private void treeDataGridView_CellFormatting (object sender, TreeDataGridViewCellFormattingEventArgs e)
        {
            var column = treeDataGridView.Columns[e.ColumnIndex];
            if (_columnSettings.ContainsKey(column) && _columnSettings[column].BackColor.HasValue)
                e.CellStyle.BackColor = _columnSettings[column].BackColor.Value;
            else
                e.CellStyle.BackColor = e.CellStyle.BackColor;

            Row row = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            switch (getRowFilterState(row))
            {
                case RowFilterState.Out:
                    e.CellStyle.ForeColor = filteredOutColor;
                    break;
                case RowFilterState.Partial:
                    e.CellStyle.ForeColor = filteredPartialColor;
                    break;
            }

            if (column is DataGridViewLinkColumn)
            {
                var cell = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy] as DataGridViewLinkCell;
                cell.LinkColor = cell.ActiveLinkColor = e.CellStyle.ForeColor;
            }
        }

        void treeDataGridView_CellMouseClick (object sender, TreeDataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0)
                return;

            // was column header clicked?
            if (e.RowIndexHierarchy.First() < 0)
            {
                Sort(e.ColumnIndex);

                // expand the root group automatically
                var rootGrouping = checkedGroupings.FirstOrDefault();
                if (rootGrouping != null && rootGrouping.Mode == GroupBy.Source)
                    treeDataGridView.Expand(0);
            }
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { keyColumn, new ColumnProperty() {Type = typeof(string)}},
                { distinctPeptidesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty() {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctAnalysesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctChargesColumn, new ColumnProperty() {Type = typeof(int)}},
                { proteinGroupsColumn, new ColumnProperty() {Type = typeof(int)}},
                { precursorMzColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { scanTimeColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { observedMassColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { exactMassColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { massErrorColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { analysisColumn, new ColumnProperty() {Type = typeof(int)}},
                { chargeColumn, new ColumnProperty() {Type = typeof(int)}},
                { qvalueColumn, new ColumnProperty() {Type = typeof(float), Precision = 2}},
                { sequenceColumn, new ColumnProperty() {Type = typeof(string)}}
            };

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Name = kvp.Key.Name;
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.DisplayIndex;
            }

            initialColumnSortOrders = new Map<int, SortOrder>()
            {
                {keyColumn.Index, SortOrder.Ascending},
                {distinctPeptidesColumn.Index, SortOrder.Descending},
                {distinctMatchesColumn.Index, SortOrder.Descending},
                {filteredSpectraColumn.Index, SortOrder.Descending},
                {distinctAnalysesColumn.Index, SortOrder.Descending},
                {distinctChargesColumn.Index, SortOrder.Descending},
                {proteinGroupsColumn.Index, SortOrder.Descending},
                {precursorMzColumn.Index, SortOrder.Ascending},
                {scanTimeColumn.Index, SortOrder.Ascending},
                {observedMassColumn.Index, SortOrder.Ascending},
                {exactMassColumn.Index, SortOrder.Ascending},
                {massErrorColumn.Index, SortOrder.Ascending},
                {analysisColumn.Index, SortOrder.Ascending},
                {chargeColumn.Index, SortOrder.Ascending},
                {qvalueColumn.Index, SortOrder.Ascending},
                {sequenceColumn.Index, SortOrder.Ascending},
            };
        }

        void treeDataGridView_CellDoubleClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.First() < 0)
                return;

            Row row = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (row is SpectrumSourceGroupRow)
                newDataFilter.SpectrumSourceGroup = new List<SpectrumSourceGroup>() { (row as SpectrumSourceGroupRow).SpectrumSourceGroup };
            else if (row is SpectrumSourceRow)
                newDataFilter.SpectrumSource = new List<SpectrumSource>() { (row as SpectrumSourceRow).SpectrumSource };
            else if (row is SpectrumRow)
                newDataFilter.Spectrum = new List<Spectrum>() { (row as SpectrumRow).Spectrum };
            else if (row is AnalysisRow)
                newDataFilter.Analysis = new List<Analysis>() { (row as AnalysisRow).Analysis };
            else if (row is PeptideRow)
                newDataFilter.Peptide = new List<Peptide>() { (row as PeptideRow).Peptide };
            else if (row is ChargeRow)
                newDataFilter.Charge = new List<int>() { (row as ChargeRow).Charge };
            else if (row is PeptideSpectrumMatchRow)
            {
                if (SpectrumViewVisualize != null)
                    SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs(row as PeptideSpectrumMatchRow));
                return;
            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        void treeDataGridView_PreviewKeyDown (object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                treeDataGridView.ClearSelection();

            if (e.KeyCode != Keys.Enter)
                return;

            var newDataFilter = new DataFilter { FilterSource = this };

            if (treeDataGridView.SelectedCells.Count == 0)
                return;

            var processedRows = new Set<int>();
            var selectedSourceGroups = new List<SpectrumSourceGroup>();
            var selectedSources = new List<SpectrumSource>();
            var selectedSpectra = new List<Spectrum>();
            var selectedAnalyses = new List<Analysis>();
            var selectedPeptides = new List<Peptide>();
            var selectedCharges = new List<int>();
            var selectedMatches = new List<PeptideSpectrumMatchRow>();

            foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
            {
                if (!processedRows.Insert(cell.RowIndex).WasInserted)
                    continue;

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(cell.RowIndex);
                Row row = GetRowFromRowHierarchy(rowIndexHierarchy);

                if (row is SpectrumSourceGroupRow)
                    selectedSourceGroups.Add((row as SpectrumSourceGroupRow).SpectrumSourceGroup);
                else if (row is SpectrumSourceRow)
                    selectedSources.Add((row as SpectrumSourceRow).SpectrumSource);
                else if (row is SpectrumRow)
                    selectedSpectra.Add((row as SpectrumRow).Spectrum);
                else if (row is AnalysisRow)
                    selectedAnalyses.Add((row as AnalysisRow).Analysis);
                else if (row is PeptideRow)
                    selectedPeptides.Add((row as PeptideRow).Peptide);
                else if (row is ChargeRow)
                    selectedCharges.Add((row as ChargeRow).Charge);
                else if (row is PeptideSpectrumMatchRow)
                    selectedMatches.Add(row as PeptideSpectrumMatchRow);
            }

            if (selectedSourceGroups.Count > 0) newDataFilter.SpectrumSourceGroup = selectedSourceGroups;
            if (selectedSources.Count > 0) newDataFilter.SpectrumSource = selectedSources;
            if (selectedSpectra.Count > 0) newDataFilter.Spectrum = selectedSpectra;
            if (selectedAnalyses.Count > 0) newDataFilter.Analysis = selectedAnalyses;
            if (selectedPeptides.Count > 0) newDataFilter.Peptide = selectedPeptides;
            if (selectedCharges.Count > 0) newDataFilter.Charge = selectedCharges;

            // TODO: visualize multiple PSMs?
            //if (selectedMatches.Count > 0)

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        protected override bool updatePivots (FormProperty formProperty)
        {
            checkedPivots = new List<Pivot<PivotBy>>();
            return false;
        }

        protected override bool updateGroupings (FormProperty formProperty)
        {
            bool groupingChanged = false;
            if (groupingSetupControl != null && formProperty.GroupingModes != null)
                groupingChanged = base.updateGroupings(formProperty);
            else
                setGroupings(new Grouping<GroupBy>(true) { Mode = GroupBy.Source, Text = "Group/Source" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Spectrum, Text = "Spectrum" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Analysis, Text = "Analysis" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Charge, Text = "Charge" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Peptide, Text = "Peptide" });

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;

            if (groupingChanged)
                setColumnVisibility();

            return groupingChanged;
        }

        public override void ClearData ()
        {
            Text = TabText = "Spectrum View";

            treeDataGridView.RootRowCount = 0;
            Refresh();
        }

        public override void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        public override void SetData(NHibernate.ISession session, DataFilter dataFilter)
        {
            if (session == null)
                return;

            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) { Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null };

            // if grouping by analysis, an analysis filter should not affect this view
            if(checkedGroupings.Any(o => o.Mode == GroupBy.Analysis))
                this.dataFilter.Analysis = null;

            // remember the first selected row
            saveSelectionPath();

            ClearData();

            Text = TabText = "Loading spectrum view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        void setData(object sender, DoWorkEventArgs e)
        {
            try
            {
                var rootGrouping = checkedGroupings.FirstOrDefault();

                if (dataFilter.IsBasicFilter)
                {
                    if (basicDataFilter == null || (viewFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = dataFilter;
                        basicTotalCounts = new TotalCounts(session, viewFilter);

                        rowsBySource = new Dictionary<DataFilterKey, List<Row>>();
                        basicRows = getChildren(rootGrouping, dataFilter);
                        basicRowsBySource = rowsBySource;
                    }

                    if(viewFilter.IsBasicFilter)
                        totalCounts = basicTotalCounts;
                    else
                        totalCounts = new TotalCounts(session, viewFilter);
                    rowsBySource = basicRowsBySource;
                    rows = basicRows;
                }
                else
                {
                    totalCounts = new TotalCounts(session, viewFilter);
                    rowsBySource = new Dictionary<DataFilterKey, List<Row>>();
                    rows = getChildren(rootGrouping, dataFilter);
                }

                applySort();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            treeDataGridView.RootRowCount = rows.Count();

            // show total counts in the form title
            Text = TabText = String.Format("Spectrum View: {0} groups, {1} sources, {2} spectra",
                                           totalCounts.Groups,
                                           totalCounts.Sources,
                                           totalCounts.Spectra);

            // try to (re)set selected item
            restoreSelectionPath();

            // expand the root group automatically
            var rootGrouping = checkedGroupings.FirstOrDefault();
            if (rootGrouping != null && rootGrouping.Mode == GroupBy.Source && rows.Count > 0)
                treeDataGridView.Expand(0);
            else
                treeDataGridView.Refresh();
        }

        private List<string> getGroupTreePath(DataModel.SpectrumSourceGroup group)
        {
            var result = new List<string>();
            string groupPath = group.Name;
            while (!String.IsNullOrEmpty(Path.GetDirectoryName(groupPath)))
            {
                result.Add(Path.GetFileName(groupPath) + '/');
                groupPath = Path.GetDirectoryName(groupPath);
            }
            result.Add("/");
            result.Reverse();
            return result;
        }

        private void editGroupsButton_Click(object sender, EventArgs e)
        {
            if (session != null)
            {
                var gcf = new GroupingControlForm(session.SessionFactory);

                if (gcf.ShowDialog() == DialogResult.OK)
                {
                    ClearData();
                    session.Clear();

                    //reload grouping
                    var rootGrouping = checkedGroupings.Count > 0 ? checkedGroupings.First() : null;
                    basicDataFilter = dataFilter;
                    basicTotalCounts = new TotalCounts(session, viewFilter);
                    rowsBySource = new Dictionary<DataFilterKey, List<Row>>();
                    basicRows = getChildren(rootGrouping, dataFilter);
                    basicRowsBySource = rowsBySource;

                    SetData(session, viewFilter);
                    //(this.ParentForm as IDPickerForm).ApplyBasicFilter();
                }
                //TODO- Find a better way of doing this
            }

        }

        private void groupingSetupControl_GroupingChanging(object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // GroupBy.Spectrum cannot be before GroupBy.Source

            if (e.Grouping.Mode != GroupBy.Spectrum && e.Grouping.Mode != GroupBy.Source)
                return;

            var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
            newGroupings[newGroupings.IndexOf(e.Grouping)] = newGroupings.First(o => o.Mode == GroupBy.Analysis);
            newGroupings.Insert(e.NewIndex, e.Grouping);

            e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.Source, GroupBy.Spectrum);
        }

        protected override void setColumnVisibility ()
        {
            var keys = new List<string>();
            foreach (var grouping in checkedGroupings)
                keys.Add(grouping.Text);

            if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) > 0)
                keys.Add("Rank");
            else
                keys.Add("Spectrum");

            keyColumn.HeaderText = String.Join("/", keys.ToArray());

            var columnsIrrelevantForGrouping = new Set<DataGridViewColumn>(new Comparison<DataGridViewColumn>((x, y) => x.Name.CompareTo(y.Name)));

            if (session != null && session.IsOpen)
                lock (session)
                    if (session.Query<Analysis>().Count() == 1)
                    {
                        columnsIrrelevantForGrouping.Add(analysisColumn);
                        columnsIrrelevantForGrouping.Add(distinctAnalysesColumn);
                    }

            if (checkedGroupings.IsNullOrEmpty())
                aggregateColumns.ForEach(o => columnsIrrelevantForGrouping.Add(o));
            else if (checkedGroupings.First().Mode == GroupBy.Spectrum)
                columnsIrrelevantForGrouping.Add(filteredSpectraColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Peptide)
                columnsIrrelevantForGrouping.Add(distinctPeptidesColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Analysis)
            {
                columnsIrrelevantForGrouping.Add(analysisColumn);
                columnsIrrelevantForGrouping.Add(distinctAnalysesColumn);
            }
            else if (checkedGroupings.First().Mode == GroupBy.Charge)
            {
                columnsIrrelevantForGrouping.Add(chargeColumn);
                columnsIrrelevantForGrouping.Add(distinctChargesColumn);
            }

            // if visibility is not forced, use grouping mode to set automatic visibility
            foreach (var kvp in _columnSettings)
                kvp.Key.Visible = kvp.Value.Visible ?? !columnsIrrelevantForGrouping.Contains(kvp.Key);

            base.setColumnVisibility();
        }

        protected override void OnGroupingChanged (object sender, EventArgs e)
        {
            setColumnVisibility();
            base.OnGroupingChanged(sender, e);
        }

        internal Dictionary<string[], List<TreeNode>> getSourceContentsForHTML()
        {
            const int decimalPlaces = 4;
            var allContents = new Dictionary<string[], List<TreeNode>>();

            //get score info
            //score gathering is one of the most time-intensive processes in item retreaval
            //loading everything to memory if possible should speed things up significantly
            //low speed system should stay in place in case memory cant handle the load
            var rawScoreList = session.CreateSQLQuery("select name, id from PeptideSpectrumMatchScoreName").List<object[]>();
            var scoreList = new List<string>();
            var scoreNameToId = new Dictionary<string, string>();
            foreach (var item in rawScoreList)
            {
                scoreList.Add(item[0].ToString());
                if (!scoreNameToId.ContainsKey(item[0].ToString()))
                    scoreNameToId.Add(item[0].ToString(), item[1].ToString());
            }
            var scoresCaptured = true;
            var scoreCache = new Dictionary<string, Dictionary<string, string>>();
            try
            {
                var allScores = session.CreateSQLQuery("Select PsmID, ScoreNameId, CAST(Value AS TEXT) from PeptideSpectrumMatchScore").List();
                foreach (var row in allScores)
                {
                    var rowContents = (object[]) row;
                    if (!scoreCache.ContainsKey(rowContents[0].ToString()))
                        scoreCache.Add(rowContents[0].ToString(), new Dictionary<string, string>());
                    if (!scoreCache[rowContents[0].ToString()].ContainsKey(rowContents[1].ToString()))
                        scoreCache[rowContents[0].ToString()].Add(rowContents[1].ToString(), rowContents[2].ToString());
                }
            }
            catch (Exception e)
            {
                scoresCaptured = false;
                var errorMessage =
                        "[SpectrumTableForm] Error when precaching data. " +
                        "Results may be processed slower than expected - " +
                        Environment.NewLine + e.Message;
                if (InvokeRequired)
                    Invoke(new Action(() => MessageBox.Show(errorMessage)));
                else
                    MessageBox.Show(errorMessage);
            }


            //get source info
            var sources = session.QueryOver<SpectrumSource>().List();
            foreach (var source in sources)
            {
                var exportTable = new List<TreeNode>();
                var sourceFilter = new DataFilter(dataFilter) { SpectrumSource = new List<SpectrumSource> { source } };
                var spectraRows = getSpectrumRows(sourceFilter);

                foreach (SpectrumRow spectra in spectraRows)
                {
                    var key = spectra.Spectrum.NativeID;
                    try
                    {
                        key = pwiz.CLI.msdata.id.abbreviate(key);
                    }
                    catch
                    {
                    }
                    var newBranch = new TreeNode
                    {
                        Text = key,
                        Tag = new[]
                                                      {
                                                          "'" + key + "'", spectra.DistinctPeptides.ToString(),
                                                          spectra.DistinctAnalyses.ToString(),
                                                          spectra.DistinctCharges.ToString(),
                                                          Math.Round(spectra.Spectrum.PrecursorMZ,decimalPlaces).ToString()
                                                      }
                    };
                    foreach (var match in spectra.Spectrum.Matches)
                    {
                        var observedMass = match.Spectrum.PrecursorMZ * match.Charge -
                                           match.Charge * pwiz.CLI.chemistry.Proton.Mass;
                        var matchNode = new TreeNode { Text = match.Rank.ToString() };
                        var tag = new List<string>
                                      {
                                          "'" + match.Rank + "'",
                                          match.Charge.ToString(),
                                          Math.Round(observedMass,decimalPlaces).ToString(),
                                          Math.Round(match.ObservedNeutralMass,decimalPlaces).ToString(),
                                          Math.Round(match.MonoisotopicMassError,decimalPlaces).ToString(),
                                          Math.Round(match.QValue,decimalPlaces).ToString(),
                                          "'" + match.Peptide.Sequence + "'"
                                      };
                        foreach (var score in scoreList)
                        {
                            if (scoresCaptured)
                            {
                                var scoreValue = scoreCache[match.Id.ToString()][scoreNameToId[score]];
                                tag.Add(Math.Round(double.Parse(scoreValue), decimalPlaces).ToString());
                            }
                            else
                            {
                                try
                                {
                                    tag.Add(Math.Round(match.Scores[score.Trim("'".ToCharArray())], decimalPlaces).ToString());
                                }
                                catch
                                {
                                    tag.Add(string.Empty);
                                }
                            }
                        }
                        matchNode.Tag = tag.ToArray();
                        newBranch.Nodes.Add(matchNode);
                    }
                    exportTable.Add(newBranch);
                }
                var headers = new List<string>()
                                  {
                                      "'Rank'",
                                      "'Charge'",
                                      "'Observed Mass'",
                                      "'Monoisotopic Mass'",
                                      "'Mass Error'",
                                      "'Q Value'",
                                      "'Sequence'"
                                  };
                headers.AddRange(scoreList.Select(item => string.Format("'{0}'", item)));
                allContents.Add(
                    new[]
                        {
                            source.Name, "source" + (source.Id == null
                                                         ? string.Empty
                                                         : source.Id.ToString())
                                         + ".html",
                            string.Join("|", headers.ToArray())
                        }, exportTable);
            }
            return allContents;
        }

        internal List<TreeNode> getSpectrumSourceGroupTree()
        {
            var groupNodes = new List<TreeNode>();
            var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " +
                                                     dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                         DataFilter.PeptideSpectrumMatchToProtein,
                                                                                         DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                     "GROUP BY ssgl.Group.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceGroupRow(o, viewFilter));
            foreach (var group in groups)
            {
                var newNode = new TreeNode
                {
                    Text = group.SpectrumSourceGroup.Name,
                    Tag =
                        new[]
                                              {
                                                  "'" + group.SpectrumSourceGroup.Name + "'", group.Spectra.ToString(),
                                                  group.DistinctPeptides.ToString(), group.DistinctMatches.ToString(),
                                                  group.DistinctAnalyses.ToString(), group.DistinctCharges.ToString()
                                              }
                };
                var groupFilter = new DataFilter(viewFilter)
                {
                    SpectrumSourceGroup =
                        new List<SpectrumSourceGroup> { group.SpectrumSourceGroup }
                };
                var sources = session.CreateQuery(AggregateRow.Selection + ", s.Source " +
                                                      groupFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                         DataFilter.PeptideSpectrumMatchToProtein,
                                                                                         DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                      "GROUP BY s.Source.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceRow(o, groupFilter));
                SpectrumSourceGroupRow ssgr = group;
                sources = from SpectrumSourceRow s in sources
                          where s.SpectrumSource.Group == ssgr.SpectrumSourceGroup
                          select s;
                foreach (var source in sources)
                {
                    var newSubNode = new TreeNode
                    {
                        Text = "source" + source.SpectrumSource.Id ?? string.Empty + ".html",
                        Tag = new[]
                                                       {
                                                           "'<a href =\"source" + (source.SpectrumSource.Id != null
                                                                                       ? source.SpectrumSource.Id.ToString()
                                                                                       : string.Empty) + ".html\">" +
                                                           source.SpectrumSource.Name + "</a>'",
                                                           source.Spectra.ToString(),
                                                           source.DistinctPeptides.ToString(),
                                                           source.DistinctMatches.ToString(),
                                                           source.DistinctAnalyses.ToString(),
                                                           source.DistinctCharges.ToString()
                                                       }
                    };
                    newNode.Nodes.Add(newSubNode);
                }
                groupNodes.Add(newNode);
            }
            return groupNodes;
        }
    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public SpectrumViewVisualizeEventArgs (SpectrumTableForm.PeptideSpectrumMatchRow row)
        {
            PeptideSpectrumMatchId = row.PeptideSpectrumMatchId;
            Spectrum = row.Spectrum;
            SpectrumSource = row.Source;
            Analysis = row.Analysis;
            ModifiedSequence = row.ModifiedSequence;
            Charge = row.Charge;
        }

        public long PeptideSpectrumMatchId { get; private set; }
        public Spectrum Spectrum { get; private set; }
        public SpectrumSource SpectrumSource { get; private set; }
        public Analysis Analysis { get; private set; }
        public string ModifiedSequence { get; private set; }
        public int Charge { get; private set; }
    }
}
