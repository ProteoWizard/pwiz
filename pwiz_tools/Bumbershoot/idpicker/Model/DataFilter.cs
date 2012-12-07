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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IDPicker.DataModel;
using NHibernate.Linq;

namespace IDPicker.DataModel
{
    public class PersistentDataFilter : Entity<PersistentDataFilter>
    {
        public virtual double MaximumQValue { get; set; }
        public virtual int MinimumDistinctPeptidesPerProtein { get; set; }
        public virtual int MinimumSpectraPerProtein { get; set; }
        public virtual int MinimumAdditionalPeptidesPerProtein { get; set; }
        public virtual int MinimumSpectraPerDistinctMatch { get; set; }
        public virtual int MinimumSpectraPerDistinctPeptide { get; set; }
        public virtual int MaximumProteinGroupsPerPeptide { get; set; }

        public virtual TotalCounts TotalCounts { get; set; }


        public PersistentDataFilter() { }

        public PersistentDataFilter(PersistentDataFilter other)
        {
            MaximumQValue = other.MaximumQValue;
            MinimumDistinctPeptidesPerProtein = other.MinimumDistinctPeptidesPerProtein;
            MinimumSpectraPerProtein = other.MinimumSpectraPerProtein;
            MinimumAdditionalPeptidesPerProtein = other.MinimumAdditionalPeptidesPerProtein;
            MinimumSpectraPerDistinctMatch = other.MinimumSpectraPerDistinctMatch;
            MinimumSpectraPerDistinctPeptide = other.MinimumSpectraPerDistinctPeptide;
            MaximumProteinGroupsPerPeptide = other.MaximumProteinGroupsPerPeptide;

            TotalCounts = other.TotalCounts;
        }

        /// <summary>
        /// If either the LHS or RHS of the equality has a null Id, the test is done on the business key instead of the Id.
        /// </summary>
        public override bool Equals(PersistentDataFilter other)
        {
            if (Id == null || other.Id == null)
                return MaximumQValue == other.MaximumQValue &&
                       MinimumDistinctPeptidesPerProtein == other.MinimumDistinctPeptidesPerProtein &&
                       MinimumSpectraPerProtein == other.MinimumSpectraPerProtein &&
                       MinimumAdditionalPeptidesPerProtein == other.MinimumAdditionalPeptidesPerProtein &&
                       MinimumSpectraPerDistinctMatch == other.MinimumSpectraPerDistinctMatch &&
                       MinimumSpectraPerDistinctPeptide == other.MinimumSpectraPerDistinctPeptide &&
                       MaximumProteinGroupsPerPeptide == other.MaximumProteinGroupsPerPeptide;

            return base.Equals(other);
        }
    }

    public class DataFilter : EventArgs
    {
        #region Events
        public event EventHandler<FilteringProgressEventArgs> FilteringProgress;
        #endregion

        #region Event arguments
        public class FilteringProgressEventArgs : System.ComponentModel.CancelEventArgs
        {
            public FilteringProgressEventArgs(string stage, int completed, Exception ex)
            {
                CompletedFilters = completed;
                TotalFilters = 19;
                FilteringStage = stage;
                FilteringException = ex;
            }

            public int CompletedFilters { get; protected set; }
            public int TotalFilters { get; protected set; }

            public string FilteringStage { get; protected set; }
            public Exception FilteringException { get; protected set; }
        }
        #endregion

        protected bool OnFilteringProgress (FilteringProgressEventArgs e)
        {
            if (FilteringProgress != null)
                FilteringProgress(this, e);
            return e.Cancel;
        }

        public DataFilter ()
        {
            PersistentDataFilter = new PersistentDataFilter();
            MaximumQValue = Properties.Settings.Default.DefaultMaxFDR;
            MinimumDistinctPeptidesPerProtein = Properties.Settings.Default.DefaultMinDistinctPeptidesPerProtein;
            MinimumSpectraPerProtein = Properties.Settings.Default.DefaultMinSpectraPerProtein;
            MinimumAdditionalPeptidesPerProtein = Properties.Settings.Default.DefaultMinAdditionalPeptides;
            MinimumSpectraPerDistinctMatch = Properties.Settings.Default.DefaultMinSpectraPerDistinctMatch;
            MinimumSpectraPerDistinctPeptide = Properties.Settings.Default.DefaultMinSpectraPerDistinctPeptide;
            MaximumProteinGroupsPerPeptide = Properties.Settings.Default.DefaultMaxProteinGroupsPerPeptide;

            DistinctMatchFormat = new DistinctMatchFormat()
            {
                AreModificationsDistinct = true,
                IsAnalysisDistinct = false,
                IsChargeDistinct = true,
                ModificationMassRoundToNearest = 1m
            };

            OriginalPersistentDataFilter = new PersistentDataFilter(PersistentDataFilter);
        }

        public DataFilter (DataFilter other)
        {
            PersistentDataFilter = new PersistentDataFilter(other.PersistentDataFilter);
            OriginalPersistentDataFilter = new PersistentDataFilter(other.OriginalPersistentDataFilter);

            DistinctMatchFormat = other.DistinctMatchFormat;

            Cluster = other.Cluster == null ? null : new List<int>(other.Cluster);
            ProteinGroup = other.ProteinGroup == null ? null : new List<int>(other.ProteinGroup);
            Protein = other.Protein == null ? null : new List<Protein>(other.Protein);
            PeptideGroup = other.PeptideGroup == null ? null : new List<int>(other.PeptideGroup);
            Peptide = other.Peptide == null ? null : new List<Peptide>(other.Peptide);
            DistinctMatchKey = other.DistinctMatchKey == null ? null : new List<DistinctMatchKey>(other.DistinctMatchKey);
            Modifications = other.Modifications == null ? null : new List<Modification>(other.Modifications);
            ModifiedSite = other.ModifiedSite == null ? null : new List<char>(other.ModifiedSite);
            Charge = other.Charge == null ? null : new List<int>(other.Charge);
            Analysis = other.Analysis == null ? null : new List<Analysis>(other.Analysis);
            Spectrum = other.Spectrum == null ? null : new List<Spectrum>(other.Spectrum);
            SpectrumSource = other.SpectrumSource == null ? null : new List<SpectrumSource>(other.SpectrumSource);
            SpectrumSourceGroup = other.SpectrumSourceGroup == null ? null : new List<SpectrumSourceGroup>(other.SpectrumSourceGroup);
            AminoAcidOffset = other.AminoAcidOffset == null ? null : new List<int>(other.AminoAcidOffset);
        }

        // persistent filter properties are in a separate POCO class for NHibernate compatibility
        public PersistentDataFilter PersistentDataFilter { get; set; }
        public PersistentDataFilter OriginalPersistentDataFilter { get; private set; }

        public DataFilter(PersistentDataFilter persistentDataFilter)
        {
            PersistentDataFilter = persistentDataFilter;
            OriginalPersistentDataFilter = new PersistentDataFilter(persistentDataFilter);

            DistinctMatchFormat = new DistinctMatchFormat()
            {
                AreModificationsDistinct = true,
                IsAnalysisDistinct = false,
                IsChargeDistinct = true,
                ModificationMassRoundToNearest = 1m
            };
        }


        public object FilterSource { get; set; }


        #region Filter properties

        public double MaximumQValue { get { return PersistentDataFilter.MaximumQValue; } set { PersistentDataFilter.MaximumQValue = value; } }
        public int MinimumDistinctPeptidesPerProtein { get { return PersistentDataFilter.MinimumDistinctPeptidesPerProtein; } set { PersistentDataFilter.MinimumDistinctPeptidesPerProtein = value; } }
        public int MinimumSpectraPerProtein { get { return PersistentDataFilter.MinimumSpectraPerProtein; } set { PersistentDataFilter.MinimumSpectraPerProtein = value; } }
        public int MinimumAdditionalPeptidesPerProtein { get { return PersistentDataFilter.MinimumAdditionalPeptidesPerProtein; } set { PersistentDataFilter.MinimumAdditionalPeptidesPerProtein = value; } }
        public int MinimumSpectraPerDistinctMatch { get { return PersistentDataFilter.MinimumSpectraPerDistinctMatch; } set { PersistentDataFilter.MinimumSpectraPerDistinctMatch = value; } }
        public int MinimumSpectraPerDistinctPeptide { get { return PersistentDataFilter.MinimumSpectraPerDistinctPeptide; } set { PersistentDataFilter.MinimumSpectraPerDistinctPeptide = value; } }
        public int MaximumProteinGroupsPerPeptide { get { return PersistentDataFilter.MaximumProteinGroupsPerPeptide; } set { PersistentDataFilter.MaximumProteinGroupsPerPeptide = value; } }

        public DistinctMatchFormat DistinctMatchFormat { get; set; }

        public IList<int> Cluster { get; set; }
        public IList<int> ProteinGroup { get; set; }
        public IList<int> PeptideGroup { get; set; }
        public IList<Protein> Protein { get; set; }
        public IList<Peptide> Peptide { get; set; }
        public IList<DistinctMatchKey> DistinctMatchKey { get; set; }
        public IList<Modification> Modifications { get; set; }
        public IList<char> ModifiedSite { get; set; }
        public IList<int> Charge { get; set; }
        public IList<SpectrumSourceGroup> SpectrumSourceGroup { get; set; }
        public IList<SpectrumSource> SpectrumSource { get; set; }
        public IList<Spectrum> Spectrum { get; set; }
        public IList<Analysis> Analysis { get; set; }

        /// <summary>
        /// A list of amino acid offsets to filter on; any peptide which contains
        /// one of the specified offsets passes the filter.
        /// </summary>
        public IList<int> AminoAcidOffset { get; set; }

        /// <summary>
        /// A regular expression for filtering peptide sequences based on amino-acid composition.
        /// </summary>
        /// <example>To match peptides with at least one histidine: "*H*"</example>
        /// <example>To match peptides with at least two histidines: "*H*H*</example>
        /// <example>To match peptides with two adjacent histidines: "*HH*</example>
        /// <example>To match peptides starting with glutamine: "Q*"</example>
        /// <example>To match peptides with a  histidines: "*H*H*</example>
        public string Composition { get; set; }
        #endregion


        public TotalCounts TotalCounts
        {
            get { return PersistentDataFilter.TotalCounts; }
            private set { PersistentDataFilter.TotalCounts = value; }
        }


        #region Logic and filter manipulation methods

        public bool HasProteinFilter
        {
            get { return !Cluster.IsNullOrEmpty() || !ProteinGroup.IsNullOrEmpty() || !Protein.IsNullOrEmpty() || !AminoAcidOffset.IsNullOrEmpty(); }
        }

        public bool HasPeptideFilter
        {
            get { return !PeptideGroup.IsNullOrEmpty() || !Peptide.IsNullOrEmpty() || !DistinctMatchKey.IsNullOrEmpty() || !Composition.IsNullOrEmpty(); }
        }

        public bool HasSpectrumFilter
        {
            get
            {
                return !SpectrumSourceGroup.IsNullOrEmpty() || !SpectrumSource.IsNullOrEmpty() || !Spectrum.IsNullOrEmpty() ||
                       !Charge.IsNullOrEmpty();
            }
        }

        public bool HasModificationFilter
        {
            get { return !Modifications.IsNullOrEmpty() || !ModifiedSite.IsNullOrEmpty(); }
        }

        public bool IsBasicFilter
        {
            get { return !HasProteinFilter && !HasPeptideFilter && !HasSpectrumFilter && !HasModificationFilter && Analysis.IsNullOrEmpty(); }
        }

        private static bool NullSafeSequenceEqual<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == rhs)
                return true;
            else if (lhs == null || rhs == null)
                return false;
            else
                return lhs.SequenceEqual<T>(rhs);
        }

        private static IList<T> NullSafeSequenceUnion<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == null && rhs == null)
                return null;
            else if (lhs == null)
                return rhs.ToList();
            else if (rhs == null)
                return lhs.ToList();
            else if (lhs == rhs)
                return lhs.ToList();
            else
                return lhs.Union<T>(rhs).ToList();
        }

        public override int GetHashCode () { return (this as object).GetHashCode(); }

        public override bool Equals (object obj)
        {
            var other = obj as DataFilter;
            if (other == null)
                return false;
            else if (object.ReferenceEquals(this, obj))
                return true;

            return PersistentDataFilter.Equals(other.PersistentDataFilter) &&
                   OriginalPersistentDataFilter.Equals(other.OriginalPersistentDataFilter) &&
                   NullSafeSequenceEqual(Cluster, other.Cluster) &&
                   NullSafeSequenceEqual(ProteinGroup, other.ProteinGroup) &&
                   NullSafeSequenceEqual(PeptideGroup, other.PeptideGroup) &&
                   NullSafeSequenceEqual(Protein, other.Protein) &&
                   NullSafeSequenceEqual(Peptide, other.Peptide) &&
                   NullSafeSequenceEqual(DistinctMatchKey, other.DistinctMatchKey) &&
                   NullSafeSequenceEqual(Modifications, other.Modifications) &&
                   NullSafeSequenceEqual(ModifiedSite, other.ModifiedSite) &&
                   NullSafeSequenceEqual(Charge, other.Charge) &&
                   NullSafeSequenceEqual(Analysis, other.Analysis) &&
                   NullSafeSequenceEqual(Spectrum, other.Spectrum) &&
                   NullSafeSequenceEqual(SpectrumSource, other.SpectrumSource) &&
                   NullSafeSequenceEqual(SpectrumSourceGroup, other.SpectrumSourceGroup) &&
                   NullSafeSequenceEqual(AminoAcidOffset, other.AminoAcidOffset) &&
                   Composition == other.Composition &&
                   DistinctMatchFormat == other.DistinctMatchFormat;
        }

        public static DataFilter operator + (DataFilter lhs, DataFilter rhs)
        {
            var newFilter = new DataFilter(lhs);
            newFilter.Cluster = NullSafeSequenceUnion(newFilter.Cluster, rhs.Cluster);
            newFilter.ProteinGroup = NullSafeSequenceUnion(newFilter.ProteinGroup, rhs.ProteinGroup);
            newFilter.PeptideGroup = NullSafeSequenceUnion(newFilter.PeptideGroup, rhs.PeptideGroup);
            newFilter.Protein = NullSafeSequenceUnion(newFilter.Protein, rhs.Protein);
            newFilter.Peptide = NullSafeSequenceUnion(newFilter.Peptide, rhs.Peptide);
            newFilter.DistinctMatchKey = NullSafeSequenceUnion(newFilter.DistinctMatchKey, rhs.DistinctMatchKey);
            newFilter.Modifications = NullSafeSequenceUnion(newFilter.Modifications, rhs.Modifications);
            newFilter.ModifiedSite = NullSafeSequenceUnion(newFilter.ModifiedSite, rhs.ModifiedSite);
            newFilter.Charge = NullSafeSequenceUnion(newFilter.Charge, rhs.Charge);
            newFilter.Analysis = NullSafeSequenceUnion(newFilter.Analysis, rhs.Analysis);
            newFilter.Spectrum = NullSafeSequenceUnion(newFilter.Spectrum, rhs.Spectrum);
            newFilter.SpectrumSource = NullSafeSequenceUnion(newFilter.SpectrumSource, rhs.SpectrumSource);
            newFilter.SpectrumSourceGroup = NullSafeSequenceUnion(newFilter.SpectrumSourceGroup, rhs.SpectrumSourceGroup);
            newFilter.AminoAcidOffset = NullSafeSequenceUnion(newFilter.AminoAcidOffset, rhs.AminoAcidOffset);
            newFilter.Composition = rhs.Composition;
            return newFilter;
        }

        public static bool operator == (DataFilter lhs, DataFilter rhs) { return object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs); }
        public static bool operator != (DataFilter lhs, DataFilter rhs) { return !(lhs == rhs); }

        private void toStringHelper<T> (string memberSingular, string memberPlural, IEnumerable<T> memberList,
                                        Func<T, string> memberLambda, IList<string> toStringResult)
        {
            if (memberList.IsNullOrEmpty())
                return;

            var distinctMemberList = memberList.Select(memberLambda).Distinct();
            if (distinctMemberList.Count() > 1)
                toStringResult.Add(String.Format("{0} {1}", distinctMemberList.Count(), memberPlural.ToLower()));
            else
                toStringResult.Add(String.Format("{0} {1}", memberSingular, distinctMemberList.First()));
        }

        private void toStringHelper<T> (string member, IEnumerable<T> memberList, Func<T, string> memberLambda,
                                        IList<string> toStringResult)
        {
            toStringHelper(member, member + "s", memberList, memberLambda, toStringResult);
        }

        private void toStringHelper<T> (string member, IEnumerable<T> memberList, IList<string> toStringResult)
        {
            toStringHelper(member, memberList, o => o.ToString(), toStringResult);
        }

        public override string ToString ()
        {
            var result = new List<string>();
            toStringHelper("Cluster", Cluster, result);
            toStringHelper("Protein group", ProteinGroup, result);
            toStringHelper("Peptide group", PeptideGroup, result);
            toStringHelper("Protein", Protein, o => o.Accession, result);
            toStringHelper("Peptide", Peptide, o => o.Sequence, result);
            toStringHelper("Distinct match", "Distinct matches", DistinctMatchKey, o => o.ToString(), result);
            toStringHelper("Modified site", ModifiedSite, result);
            toStringHelper("Mass shift", Modifications, o => Math.Round(o.MonoMassDelta).ToString(), result);
            toStringHelper("Charge", Charge, result);
            toStringHelper("Analysis", "Analyses", Analysis, o => o.Name, result);
            toStringHelper("Group", SpectrumSourceGroup, o => o.Name, result);
            toStringHelper("Source", SpectrumSource, o => o.Name, result);
            toStringHelper("Spectrum", "Spectra", Spectrum, o => o.NativeID, result);
            toStringHelper("Offset", AminoAcidOffset, o => (o + 1).ToString(), result);

            if (!Composition.IsNullOrEmpty())
                result.Add(String.Format("Composition \"{0}\"", Composition));

            if (result.Count > 0)
                return String.Join("; ", result.ToArray());

            return String.Format("Q-value ≤ {0}; " +
                                 "Distinct peptides per protein ≥ {1}; " +
                                 "Spectra per protein ≥ {2}; " +
                                 "Additional peptides per protein ≥ {3}" +
                                 "Spectra per distinct match ≥ {4}; " +
                                 "Spectra per distinct peptide ≥ {5}; ",
                                 "Protein groups per peptide ≤ {6}",
                                 MaximumQValue,
                                 MinimumDistinctPeptidesPerProtein,
                                 MinimumSpectraPerProtein,
                                 MinimumAdditionalPeptidesPerProtein,
                                 MinimumSpectraPerDistinctMatch,
                                 MinimumSpectraPerDistinctPeptide,
                                 MaximumProteinGroupsPerPeptide);
        }
        #endregion

        public static DataFilter LoadFilter (NHibernate.ISession session)
        {
            // load the most recent data filter (with the maximum Id)
            var mostRecentFilter = session.Query<PersistentDataFilter>().OrderByDescending(o => o.Id).FirstOrDefault();
            if (mostRecentFilter == null)
                return null;

            return new DataFilter(mostRecentFilter);
        }

        private void SaveFilter(NHibernate.ISession session)
        {
            // if this filter is already in the history, delete it
            var existingFilter = session.Query<PersistentDataFilter>().ToList().SingleOrDefault(o => o.Equals(PersistentDataFilter));
            if (existingFilter != null)
            {
                session.Delete(existingFilter);
                session.Flush();
            }

            // increment Id and save the filter as the most recent one
            PersistentDataFilter.Id = Convert.ToInt64(session.CreateSQLQuery("SELECT IFNULL(MAX(Id), 0)+1 FROM FilterHistory").UniqueResult());
            session.Save(PersistentDataFilter);
        }

        public static void DropFilters (System.Data.IDbConnection conn)
        {
            // ignore errors if main tables haven't been created yet

            #region Drop Filtered* tables
            conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS FilteredProtein;
                                   DROP TABLE IF EXISTS FilteredPeptideInstance;
                                   DROP TABLE IF EXISTS FilteredPeptide;
                                   DROP TABLE IF EXISTS FilteredPeptideSpectrumMatch;
                                   DROP TABLE IF EXISTS FilteredSpectrum
                                  ");
            #endregion

            #region Restore Unfiltered* tables as the main tables
            try
            {
                // if unfiltered tables have not been created, this will throw and skip the rest of the block
                conn.ExecuteNonQuery("SELECT Id FROM UnfilteredProtein LIMIT 1");

                // drop filtered tables
                conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS Protein;
                                       DROP TABLE IF EXISTS PeptideInstance;
                                       DROP TABLE IF EXISTS Peptide;
                                       DROP TABLE IF EXISTS PeptideSpectrumMatch;
                                       DROP TABLE IF EXISTS Spectrum
                                      ");

                // rename unfiltered tables 
                conn.ExecuteNonQuery(@"ALTER TABLE UnfilteredProtein RENAME TO Protein;
                                       ALTER TABLE UnfilteredPeptideInstance RENAME TO PeptideInstance;
                                       ALTER TABLE UnfilteredPeptide RENAME TO Peptide;
                                       ALTER TABLE UnfilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;
                                       ALTER TABLE UnfilteredSpectrum RENAME TO Spectrum
                                      ");

                // reset QValues
                //conn.ExecuteNonQuery("UPDATE PeptideSpectrumMatch SET QValue = 2");
            }
            catch
            {
            }
            #endregion
        }

        public void DropFilters (NHibernate.ISession session)
        {
            DropFilters(session.Connection);
        }

        public void ApplyBasicFilters (NHibernate.ISession session)
        {
            // free up memory
            session.Clear();

            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            int stepsCompleted = 0;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Dropping current filters...", ++stepsCompleted, null)))
                return;

            DropFilters(session);

            #region Create Filtered* tables by applying the basic filters to the main tables
            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering spectra...", ++stepsCompleted, null)))
                return;
            string filterSpectraSql =
                @"CREATE TABLE FilteredSpectrum (Id INTEGER PRIMARY KEY, Source INTEGER, Index_ INTEGER, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);
                  INSERT INTO FilteredSpectrum SELECT s.*
                  FROM PeptideSpectrumMatch psm
                  JOIN Spectrum s ON psm.Spectrum = s.Id
                  JOIN SpectrumSource ss ON s.Source = ss.Id
                  -- filter out ungrouped spectrum sources
                  WHERE ss.Group_ AND {0} >= psm.QValue AND psm.Rank = 1
                  GROUP BY s.Id;
                  CREATE UNIQUE INDEX FilteredSpectrum_SourceNativeID ON FilteredSpectrum (Source, NativeID);";
            session.CreateSQLQuery(String.Format(filterSpectraSql, MaximumQValue)).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering proteins...", ++stepsCompleted, null)))
                return;
            string filterProteinsSql =
                @"CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT);
                  INSERT INTO FilteredProtein SELECT pro.*
                  FROM PeptideSpectrumMatch psm
                  JOIN FilteredSpectrum s ON psm.Spectrum = s.Id
                  JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                  JOIN Protein pro ON pi.Protein = pro.Id
                  JOIN SpectrumSource ss ON s.Source = ss.Id
                  WHERE psm.Rank = 1 AND {2} >= psm.QValue
                  GROUP BY pi.Protein
                  HAVING {0} <= COUNT(DISTINCT psm.Peptide) AND
                         {1} <= COUNT(DISTINCT psm.Spectrum);";
            session.CreateSQLQuery(String.Format(filterProteinsSql,
                                                 MinimumDistinctPeptidesPerProtein,
                                                 MinimumSpectraPerProtein,
                                                 MaximumQValue)).ExecuteUpdate();
            session.CreateSQLQuery("CREATE UNIQUE INDEX FilteredProtein_Accession ON FilteredProtein (Accession);").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide spectrum matches...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);
                                     INSERT INTO FilteredPeptideSpectrumMatch SELECT psm.*
                                     FROM FilteredProtein pro
                                     JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                     JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                     JOIN FilteredSpectrum s ON psm.Spectrum = s.Id
                                     JOIN SpectrumSource ss ON s.Source = ss.Id
                                     -- filter out ungrouped spectrum sources
                                     WHERE " + MaximumQValue + @" >= psm.QValue AND psm.Rank = 1
                                     GROUP BY psm.Id;
                                     CREATE INDEX FilteredPeptideSpectrumMatch_PeptideSpectrumAnalysis ON FilteredPeptideSpectrumMatch (Peptide, Spectrum, Analysis);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_SpectrumPeptideAnalysis ON FilteredPeptideSpectrumMatch (Spectrum, Peptide, Analysis);
                                    "
                                  ).ExecuteUpdate();

            if (MinimumSpectraPerDistinctMatch > 1)
                session.CreateSQLQuery(@"DELETE FROM FilteredPeptideSpectrumMatch
                                         WHERE " + DistinctMatchFormat.SqlExpression.Replace("psm.", "FilteredPeptideSpectrumMatch.") + @" IN
                                               (SELECT " + DistinctMatchFormat.SqlExpression + @"
                                                FROM FilteredPeptideSpectrumMatch psm
                                                GROUP BY " + DistinctMatchFormat.SqlExpression + @"
                                                HAVING " + MinimumSpectraPerDistinctMatch + @" > COUNT(DISTINCT psm.Spectrum))
                                        ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptides...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);
                                     INSERT INTO FilteredPeptide SELECT pep.*
                                     FROM FilteredPeptideSpectrumMatch psm
                                     JOIN Peptide pep ON psm.Peptide = pep.Id
                                     GROUP BY pep.Id " +
                                     (MinimumSpectraPerDistinctPeptide > 1 ? @"HAVING " + MinimumSpectraPerDistinctPeptide + @" <= COUNT(DISTINCT psm.Spectrum)"
                                                                           : @"")
                                    //"
                                  ).ExecuteUpdate();

            if (MinimumSpectraPerDistinctMatch + MinimumSpectraPerDistinctPeptide > 1)
                session.CreateSQLQuery(@"DELETE FROM FilteredPeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM FilteredPeptide);
                                        ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide instances...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);
                                     INSERT INTO FilteredPeptideInstance SELECT pi.*
                                     FROM FilteredPeptide pep
                                     JOIN PeptideInstance pi ON pep.Id = pi.Peptide
                                     JOIN FilteredProtein pro ON pi.Protein = pro.Id;
                                     CREATE INDEX FilteredPeptideInstance_PeptideProtein ON FilteredPeptideInstance (Peptide, Protein);
                                     CREATE INDEX FilteredPeptideInstance_ProteinOffsetLength ON FilteredPeptideInstance (Protein, Offset, Length);"
                                  ).ExecuteUpdate();
            #endregion

            #region Rename main tables to Unfiltered*
            session.CreateSQLQuery(@"ALTER TABLE Protein RENAME TO UnfilteredProtein;
                                     ALTER TABLE PeptideInstance RENAME TO UnfilteredPeptideInstance;
                                     ALTER TABLE Peptide RENAME TO UnfilteredPeptide;
                                     ALTER TABLE PeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch;
                                     ALTER TABLE Spectrum RENAME TO UnfilteredSpectrum
                                    ").ExecuteUpdate();
            #endregion

            #region Rename Filtered* tables to main tables
            session.CreateSQLQuery(@"ALTER TABLE FilteredProtein RENAME TO Protein;
                                     ALTER TABLE FilteredPeptideInstance RENAME TO PeptideInstance;
                                     ALTER TABLE FilteredPeptide RENAME TO Peptide;
                                     ALTER TABLE FilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;
                                     ALTER TABLE FilteredSpectrum RENAME TO Spectrum
                                    ").ExecuteUpdate();
            #endregion

            if (AssembleProteinGroups(session, ref stepsCompleted)) return;

            if (MaximumProteinGroupsPerPeptide > 0)
            {
                session.CreateSQLQuery(@"DELETE FROM Peptide WHERE Id IN
                                             (
                                              SELECT pi.Peptide
                                              FROM Protein pro
                                              JOIN PeptideInstance pi on pro.Id = pi.Protein
                                              GROUP BY pi.Peptide
                                              HAVING COUNT(DISTINCT ProteinGroup) > " + MaximumProteinGroupsPerPeptide + @"
                                             );
                                          DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                          DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);
                                          DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                          DELETE FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);
                                         ").ExecuteUpdate();
                --stepsCompleted;
                session.CreateSQLQuery("DROP INDEX Protein_ProteinGroup").ExecuteUpdate();

                // reapply protein-level filters after filtering out ambiguous PSMs
                session.CreateSQLQuery(@"DROP INDEX FilteredProtein_Accession;
                                         ALTER TABLE Protein RENAME TO TempProtein").ExecuteUpdate();
                filterProteinsSql = filterProteinsSql.Replace("Filtered", "").Replace("JOIN Protein", "JOIN TempProtein");
                session.CreateSQLQuery(String.Format(filterProteinsSql,
                                                     MinimumDistinctPeptidesPerProtein,
                                                     MinimumSpectraPerProtein,
                                                     MaximumQValue)).ExecuteUpdate();
                session.CreateSQLQuery(@"CREATE UNIQUE INDEX FilteredProtein_Accession ON Protein (Accession);
                                         DROP TABLE TempProtein").ExecuteUpdate();

                if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            }

            if (AssembleClusters(session, ref stepsCompleted)) return;
            if (ApplyAdditionalPeptidesFilter(session, ref stepsCompleted)) return;
            if (AssembleProteinCoverage(session, ref stepsCompleted)) return;
            if (AssembleDistinctMatches(session, ref stepsCompleted)) return;

            // assemble new protein groups after the additional peptides filter
            session.CreateSQLQuery("DROP INDEX Protein_ProteinGroup").ExecuteUpdate();
            if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            if (AssemblePeptideGroups(session, ref stepsCompleted)) return;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating summary statistics...", ++stepsCompleted, null)))
                return;

            TotalCounts = new TotalCounts(session);

            SaveFilter(session);

            if (useScopedTransaction)
                session.Transaction.Commit();

            if (AggregateQuantitationData(session, ref stepsCompleted)) return;
        }


        #region Implementation of basic filters

        /// <summary>
        /// Set ProteinGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssembleProteinGroups(NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling protein groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE ProteinGroups AS
                                     SELECT pro.Id AS ProteinId, GROUP_CONCAT(DISTINCT pi.Peptide) AS ProteinGroup
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein = pro.Id
                                     GROUP BY pi.Protein;

                                     -- ProteinGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempProtein AS
                                     SELECT ProteinId, Accession, IsDecoy, Cluster, pg2.rowid, Length
                                     FROM ProteinGroups pg
                                     JOIN ( 
                                           SELECT pg.ProteinGroup
                                           FROM ProteinGroups pg
                                           GROUP BY pg.ProteinGroup
                                          ) pg2 ON pg.ProteinGroup = pg2.ProteinGroup
                                     JOIN Protein pro ON pg.ProteinId = pro.Id;

                                     DELETE FROM Protein;
                                     INSERT INTO Protein SELECT * FROM TempProtein;
                                     CREATE INDEX Protein_ProteinGroup ON Protein (ProteinGroup);
                                     DROP TABLE ProteinGroups;
                                     DROP TABLE TempProtein;
                                    ").ExecuteUpdate();

            session.Clear();

            return false;
        }

        /// <summary>
        /// Set PeptideGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssemblePeptideGroups (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling peptide groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE PeptideGroups AS
                                     SELECT pep.Id AS PeptideId, GROUP_CONCAT(DISTINCT pi.Protein) AS PeptideGroup
                                     FROM PeptideInstance pi
                                     JOIN Peptide pep ON pi.Peptide=pep.Id
                                     GROUP BY pi.Peptide;

                                     -- PeptideGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempPeptide AS
                                     SELECT PeptideId, MonoisotopicMass, MolecularWeight, pg2.rowid, DecoySequence
                                     FROM PeptideGroups pg
                                     JOIN ( 
                                           SELECT pg.PeptideGroup
                                           FROM PeptideGroups pg
                                           GROUP BY pg.PeptideGroup
                                          ) pg2 ON pg.PeptideGroup = pg2.PeptideGroup
                                     JOIN Peptide pro ON pg.PeptideId = pro.Id;

                                     DELETE FROM Peptide;
                                     INSERT INTO Peptide SELECT * FROM TempPeptide;
                                     CREATE INDEX Peptide_PeptideGroup ON Peptide (PeptideGroup);
                                     DROP TABLE PeptideGroups;
                                     DROP TABLE TempPeptide;
                                    ").ExecuteUpdate();
            session.Clear();

            return false;
        }

        /// <summary>
        /// Calculate additional peptides per protein and filter out proteins that don't meet the minimum
        /// </summary>
        bool ApplyAdditionalPeptidesFilter (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (MinimumAdditionalPeptidesPerProtein == 0)
                return OnFilteringProgress(new FilteringProgressEventArgs("Skipping additional peptide filter...", stepsCompleted += 2, null));

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating additional peptide counts...", ++stepsCompleted, null)))
                return true;

            Map<long, long> additionalPeptidesByProteinId = CalculateAdditionalPeptides(session);

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS AdditionalMatches;
                                     CREATE TABLE AdditionalMatches (ProteinId INTEGER PRIMARY KEY, AdditionalMatches INT)
                                    ").ExecuteUpdate();

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AdditionalMatches VALUES (?, ?)";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<long, long>.MapPair itr in additionalPeptidesByProteinId)
            {
                parameters[0].Value = itr.Key;
                parameters[1].Value = itr.Value;
                cmd.ExecuteNonQuery();
            }

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering by additional peptide count...", ++stepsCompleted, null)))
                return true;

            // delete proteins that don't meet the additional matches filter
            // delete peptide instances whose protein is gone
            // delete peptides that no longer have any peptide instances
            // delete PSMs whose peptide is gone
            string additionalPeptidesDeleteSql = @"DELETE FROM Protein
                                                         WHERE Id IN (SELECT pro.Id
                                                                      FROM Protein pro
                                                                      JOIN AdditionalMatches am ON pro.Id = am.ProteinId
                                                                      WHERE am.AdditionalMatches < {0});
                                                   DELETE FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);
                                                   DELETE FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);
                                                   DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                                   DELETE FROM Spectrum WHERE Id NOT IN (SELECT Spectrum FROM PeptideSpectrumMatch);
                                                  ";

            session.CreateSQLQuery(String.Format(additionalPeptidesDeleteSql, MinimumAdditionalPeptidesPerProtein)).ExecuteUpdate();

            return false;
        }

        /// <summary>
        /// Calculate clusters (connected components) for proteins
        /// </summary>
        bool AssembleClusters (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein clusters...", ++stepsCompleted, null)))
                return true;

            Map<int, long> clusterByProteinGroup = calculateProteinClusters(session);

            if (OnFilteringProgress(new FilteringProgressEventArgs("Assigning proteins to clusters...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE Protein SET Cluster = ? WHERE ProteinGroup = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<int, long>.MapPair itr in clusterByProteinGroup)
            {
                parameters[0].Value = itr.Value;
                parameters[1].Value = itr.Key;
                cmd.ExecuteNonQuery();
            }
            cmd.ExecuteNonQuery("CREATE INDEX Protein_Cluster ON Protein (Cluster)");

            return false;
        }

        /// <summary>
        /// Calculate coverage and coverage masks for proteins
        /// </summary>
        bool AssembleProteinCoverage (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage...", ++stepsCompleted, null)))
                return true;
            
            session.CreateSQLQuery(@"DELETE FROM ProteinCoverage;
                                     INSERT INTO ProteinCoverage (Id, Coverage)
                                     SELECT pi.Protein, CAST(COUNT(DISTINCT i.Value) AS REAL) * 100 / pro.Length
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein;
                                    ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            // get non-zero coverage depths at each protein offset
            var coverageMaskRows = session.CreateSQLQuery(
                                   @"SELECT pi.Protein, pro.Length, i.Value, COUNT(i.Value)
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i 
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein, i.Value
                                     ORDER BY pi.Protein, i.Value;
                                    ").List().OfType<object[]>();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Updating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE ProteinCoverage SET CoverageMask = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();

            var proteinCoverageMaskUserType = new ProteinCoverageMaskUserType();
            long currentProteinId = 0;
            ushort[] currentProteinMask = null;

            foreach (object[] row in coverageMaskRows)
            {
                long proteinId = Convert.ToInt64(row[0]);
                int proteinLength = Convert.ToInt32(row[1]);

                // before moving on to the next protein, update the current one
                if (proteinId > currentProteinId)
                {
                    if (currentProteinMask != null)
                    {
                        parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                        parameters[1].Value = currentProteinId;
                        cmd.ExecuteNonQuery();
                    }

                    currentProteinId = proteinId;
                    currentProteinMask = new ushort[proteinLength];

                    // initialize all offsets to 0 (no coverage)
                    currentProteinMask.Initialize();
                }

                // set a covered offset to its coverage depth
                currentProteinMask[Convert.ToInt32(row[2])] = Convert.ToUInt16(row[3]);
            }

            // set the last protein's mask
            if (currentProteinMask != null)
            {
                parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                parameters[1].Value = currentProteinId;
                cmd.ExecuteNonQuery();
            }

            return false;
        }

        bool AssembleDistinctMatches (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling distinct matches...", ++stepsCompleted, null)))
                return true;
            string sql = String.Format(@"DROP TABLE IF EXISTS DistinctMatch;
                                         CREATE TABLE DistinctMatch (PsmId INTEGER PRIMARY KEY, DistinctMatchId INT, DistinctMatchKey TEXT);
                                         INSERT INTO DistinctMatch (PsmId, DistinctMatchKey)
                                            SELECT DISTINCT psm.Id, {0} FROM PeptideSpectrumMatch psm;
                                         CREATE TEMP TABLE GroupedDistinctMatch AS
                                            SELECT MIN(PsmId) AS RepresentativePsmId, DistinctMatchKey AS UniqueDistinctMatchKey FROM DistinctMatch GROUP BY DistinctMatchKey;
                                         CREATE UNIQUE INDEX GroupedDistinctMatch_DistinctMatchKey ON GroupedDistinctMatch (UniqueDistinctMatchKey);
                                         UPDATE DistinctMatch SET DistinctMatchId = (SELECT RepresentativePsmId FROM GroupedDistinctMatch WHERE UniqueDistinctMatchKey=DistinctMatchKey);
                                         DROP TABLE GroupedDistinctMatch;
                                         CREATE INDEX DistinctMatch_DistinctMatchId ON DistinctMatch (DistinctMatchId);
                                        ", DistinctMatchFormat.SqlExpression);
            session.CreateSQLQuery(sql).ExecuteUpdate();
            return false;
        }

        public void RecalculateAggregateQuantitationData(NHibernate.ISession session)
        {
            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            int stepsCompleted = new FilteringProgressEventArgs("", 0, null).TotalFilters - 1;
            AggregateQuantitationData(session, ref stepsCompleted);

            if (useScopedTransaction)
                session.Transaction.Commit();
        }

        bool AggregateQuantitationData(NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Aggregating quantitation data...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"DELETE FROM PeptideQuantitation;
                                     INSERT INTO PeptideQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT psm.Peptide, DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY psm.Peptide;
                                    ").ExecuteUpdate();

            session.CreateSQLQuery(@"DELETE FROM DistinctMatchQuantitation;
                                     INSERT INTO DistinctMatchQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT dm.DistinctMatchKey, DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN DistinctMatch dm ON psm.Id=dm.PsmId
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY dm.DistinctMatchKey;
                                    ").ExecuteUpdate();

            session.CreateSQLQuery(@"DELETE FROM ProteinQuantitation;
                                     INSERT INTO ProteinQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT pi.Protein, DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY pi.Protein;
                                    ").ExecuteUpdate();
            return false;
        }


        /// <summary>
        /// Calculates (by a greedy algorithm) how many additional results each protein group explains.
        /// </summary>
        static Map<long, long> CalculateAdditionalPeptides(NHibernate.ISession session)
        {
            var resultSetByProteinIdByCluster = new Map<int, Map<long, Set<Set<long>>>>();
            var proteinGroupByProteinId = new Dictionary<long, int>();
            var proteinSetByProteinGroup = new Map<int, Set<long>>();
            var sharedResultsByProteinId = new Map<long, long>();

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS SpectrumResults;
                                     CREATE TEMP TABLE SpectrumResults AS
                                     SELECT psm.Spectrum AS Spectrum, GROUP_CONCAT(DISTINCT psm.Peptide) AS Peptides, COUNT(DISTINCT pi.Protein) AS SharedResultCount
                                     FROM PeptideSpectrumMatch psm
                                     JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                                     GROUP BY psm.Spectrum
                                    ").ExecuteUpdate();

            var queryByProtein = session.CreateSQLQuery(@"SELECT pro.Id, pro.ProteinGroup, pro.Cluster, SUM(sr.SharedResultCount)
                                                          FROM Protein pro
                                                          JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                          JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                          JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                          WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                            AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                          GROUP BY pro.Id
                                                          ORDER BY pro.Cluster");

            // For each protein, get the list of peptides evidencing it;
            // an ambiguous spectrum will show up as a nested list of peptides
            var queryByResult = session.CreateSQLQuery(@"SELECT pro.Id, pro.Cluster, GROUP_CONCAT(sr.Peptides)
                                                         FROM Protein pro
                                                         JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                         JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                         JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                         WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                           AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                         GROUP BY pro.Id, sr.Peptides");

            // construct the result set for each protein
            foreach (var queryRow in queryByResult.List<object[]>())
            {
                long proteinId = (long)queryRow[0];
                int cluster = (int)queryRow[1];
                string resultIds = (string)queryRow[2];
                string[] resultIdTokens = resultIds.Split(',');
                Set<long> resultIdSet = new Set<long>(resultIdTokens.Select(o => Convert.ToInt64(o)));
                resultSetByProteinIdByCluster[cluster][proteinId].Add(resultIdSet);
            }

            var additionalPeptidesByProteinId = new Map<long, long>();

            Action<int> loopBody = (cluster) =>
            {
                // keep track of the proteins that explain the most results
                Set<long> maxProteinIds = new Set<long>();
                int maxExplainedCount = 0;
                long minSharedResults = 0;
                var resultSetByProteinId = resultSetByProteinIdByCluster[cluster];

                // find the proteins that explain the most results for this cluster
                Action findMaxProteins = () =>
                {
                    foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                    {
                        long proteinId = itr.Key;
                        Set<Set<long>> explainedResults = itr.Value;
                        long sharedResults = sharedResultsByProteinId[proteinId];

                        if (explainedResults.Count > maxExplainedCount)
                        {
                            maxProteinIds.Clear();
                            maxProteinIds.Add(proteinId);
                            maxExplainedCount = explainedResults.Count;
                            minSharedResults = sharedResults;
                        }
                        else if (explainedResults.Count == maxExplainedCount)
                        {
                            if (sharedResults < minSharedResults)
                            {
                                maxProteinIds.Clear();
                                maxProteinIds.Add(proteinId);
                                minSharedResults = sharedResults;
                            }
                            else if (sharedResults == minSharedResults)
                                maxProteinIds.Add(proteinId);
                        }
                    }
                };

                // find the proteins that explain the most results for this cluster
                findMaxProteins();

                // loop until the resultSetByProteinId map is empty
                while (resultSetByProteinId.Count > 0)
                {
                    // the set of results explained by the max. proteins
                    Set<Set<long>> maxExplainedResults = null;

                    // remove max. proteins from the resultSetByProteinId map
                    foreach (long maxProteinId in maxProteinIds)
                    {
                        if (maxExplainedResults == null)
                            maxExplainedResults = resultSetByProteinId[maxProteinId];
                        else
                            maxExplainedResults.Union(resultSetByProteinId[maxProteinId]);

                        resultSetByProteinId.Remove(maxProteinId);
                        additionalPeptidesByProteinId[maxProteinId] = maxExplainedCount;
                    }

                    // subtract the max. proteins' results from the remaining proteins
                    foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                        itr.Value.Subtract(maxExplainedResults);

                    maxProteinIds.Clear();
                    maxExplainedCount = 0;
                    minSharedResults = 0;

                    // find the proteins that explain the most results for this cluster
                    findMaxProteins();

                    // all remaining proteins present no additional evidence, so break the loop
                    if (maxExplainedCount == 0)
                    {
                        foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                            additionalPeptidesByProteinId[itr.Key] = 0;
                        break;
                    }
                }
            };

            int lastCluster = 0;
            foreach (var queryRow in queryByProtein.List<object[]>())
            {
                long proteinId = (long)queryRow[0];
                int proteinGroup = (int)queryRow[1];
                int cluster = (int)queryRow[2];

                if (lastCluster > 0 && cluster != lastCluster)
                {
                    loopBody(lastCluster);

                    sharedResultsByProteinId.Clear();
                    proteinGroupByProteinId.Clear();
                    proteinSetByProteinGroup.Clear();
                }

                lastCluster = cluster;
                sharedResultsByProteinId[proteinId] = (long)queryRow[3];
                proteinGroupByProteinId[proteinId] = proteinGroup;
                proteinSetByProteinGroup[proteinGroup].Add(proteinId);
            }

            if (lastCluster > 0)
                loopBody(lastCluster);

            return additionalPeptidesByProteinId;
        }

        Map<int, long> calculateProteinClusters(NHibernate.ISession session)
        {
            var spectrumSetByProteinGroup = new Map<int, Set<long>>();
            var proteinGroupSetBySpectrumId = new Map<long, Set<int>>();

            var query = session.CreateQuery("SELECT pro.ProteinGroup, psm.Spectrum.id " +
                                            GetFilteredQueryString(FromProtein, ProteinToPeptideSpectrumMatch));

            foreach (var queryRow in query.List<object[]>())
            {
                int proteinGroup = Convert.ToInt32(queryRow[0]);
                long spectrumId = (long)queryRow[1];

                spectrumSetByProteinGroup[proteinGroup].Add(spectrumId);
                proteinGroupSetBySpectrumId[spectrumId].Add(proteinGroup);
            }

            var clusterByProteinGroup = new Map<int, long>();
            int clusterId = 0;
            var clusterStack = new Stack<KeyValuePair<int, Set<long>>>();

            foreach (var pair in spectrumSetByProteinGroup)
            {
                int proteinGroup = pair.Key;

                if (clusterByProteinGroup.Contains(proteinGroup))
                    continue;

                // for each protein without a cluster assignment, make a new cluster
                ++clusterId;
                clusterStack.Push(new KeyValuePair<int, Set<long>>(proteinGroup, spectrumSetByProteinGroup[proteinGroup]));
                while (clusterStack.Count > 0)
                {
                    var kvp = clusterStack.Pop();

                    // add all "cousin" proteins to the current cluster
                    foreach (long spectrumId in kvp.Value)
                        foreach (var cousinProteinGroup in proteinGroupSetBySpectrumId[spectrumId])
                        {
                            var insertResult = clusterByProteinGroup.Insert(cousinProteinGroup, clusterId);
                            if (!insertResult.WasInserted)
                                continue;

                            clusterStack.Push(new KeyValuePair<int, Set<long>>(cousinProteinGroup, spectrumSetByProteinGroup[cousinProteinGroup]));
                        }
                }
            }

            return clusterByProteinGroup;
        }
        #endregion

        #region Definitions for common HQL strings
        public static readonly string FromProtein = "Protein pro";
        public static readonly string FromPeptide = "Peptide pep";
        public static readonly string FromPeptideSpectrumMatch = "PeptideSpectrumMatch psm";
        public static readonly string FromPeptideInstance = "PeptideInstance pi";
        public static readonly string FromPeptideModification = "PeptideModification pm";
        public static readonly string FromModification = "Modification mod";
        public static readonly string FromAnalysis = "Analysis a";
        public static readonly string FromSpectrum = "Spectrum s";
        public static readonly string FromSpectrumSource = "SpectrumSource ss";
        public static readonly string FromSpectrumSourceGroupLink = "SpectrumSourceGroupLink ssgl";
        public static readonly string FromSpectrumSourceGroup = "SpectrumSourceGroup ssg";

        public static readonly string ProteinToPeptideInstance = "JOIN pro.Peptides pi";
        public static readonly string ProteinToPeptide = ProteinToPeptideInstance + ";JOIN pi.Peptide pep";
        public static readonly string ProteinToPeptideSpectrumMatch = ProteinToPeptide + ";JOIN pep.Matches psm";
        public static readonly string ProteinToPeptideModification = ProteinToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string ProteinToModification = ProteinToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string ProteinToAnalysis = ProteinToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string ProteinToSpectrum = ProteinToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string ProteinToSpectrumSource = ProteinToSpectrum + ";JOIN s.Source ss";
        public static readonly string ProteinToSpectrumSourceGroupLink = ProteinToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string ProteinToSpectrumSourceGroup = ProteinToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideToPeptideInstance = "JOIN pep.Instances pi";
        public static readonly string PeptideToPeptideSpectrumMatch = "JOIN pep.Matches psm";
        public static readonly string PeptideToProtein = PeptideToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideToPeptideModification = PeptideToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideToModification = PeptideToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideToAnalysis = PeptideToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string PeptideToSpectrum = PeptideToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string PeptideToSpectrumSource = PeptideToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideToSpectrumSourceGroupLink = PeptideToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideToSpectrumSourceGroup = PeptideToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideSpectrumMatchToPeptide = "JOIN psm.Peptide pep";
        public static readonly string PeptideSpectrumMatchToAnalysis = "JOIN psm.Analysis a";
        public static readonly string PeptideSpectrumMatchToSpectrum = "JOIN psm.Spectrum s";
        public static readonly string PeptideSpectrumMatchToPeptideModification = "LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideSpectrumMatchToPeptideInstance = PeptideSpectrumMatchToPeptide + ";JOIN pep.Instances pi";
        public static readonly string PeptideSpectrumMatchToProtein = PeptideSpectrumMatchToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideSpectrumMatchToModification = PeptideSpectrumMatchToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideSpectrumMatchToSpectrumSource = PeptideSpectrumMatchToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroupLink = PeptideSpectrumMatchToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroup = PeptideSpectrumMatchToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string SpectrumToSpectrumSource = "JOIN s.Source ss";
        public static readonly string SpectrumToSpectrumSourceGroupLink = SpectrumToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string SpectrumToSpectrumSourceGroup = SpectrumToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";
        #endregion

        #region Query methods

        // pick the optimal source table and join order for the current view filters
        public void optimizeQueryOrder(ref string fromTable, ref string[] joinTables)
        {
            if (fromTable == FromPeptideSpectrumMatch && HasProteinFilter && !HasSpectrumFilter)
            {
                fromTable = FromProtein;
                var newJoinTables = new List<string> { ProteinToPeptideSpectrumMatch };
                if (joinTables.Contains(PeptideSpectrumMatchToPeptide)) newJoinTables.Add(ProteinToPeptide);
                if (joinTables.Contains(PeptideSpectrumMatchToAnalysis)) newJoinTables.Add(ProteinToAnalysis);
                if (joinTables.Contains(PeptideSpectrumMatchToSpectrum)) newJoinTables.Add(ProteinToSpectrum);
                if (joinTables.Contains(PeptideSpectrumMatchToPeptideModification)) newJoinTables.Add(ProteinToPeptideModification);
                if (joinTables.Contains(PeptideSpectrumMatchToModification)) newJoinTables.Add(ProteinToModification);
                if (joinTables.Contains(PeptideSpectrumMatchToSpectrumSource)) newJoinTables.Add(ProteinToSpectrumSource);
                if (joinTables.Contains(PeptideSpectrumMatchToSpectrumSourceGroupLink)) newJoinTables.Add(ProteinToSpectrumSourceGroupLink);
                if (joinTables.Contains(PeptideSpectrumMatchToSpectrumSourceGroup)) newJoinTables.Add(ProteinToSpectrumSourceGroup);
                joinTables = newJoinTables.ToArray();
            }
            else if (fromTable == FromProtein && (HasSpectrumFilter || HasModificationFilter) && !HasProteinFilter)
            {
                fromTable = FromPeptideSpectrumMatch;
                var newJoinTables = new List<string> { PeptideSpectrumMatchToProtein };
                if (joinTables.Contains(ProteinToPeptide)) newJoinTables.Add(PeptideSpectrumMatchToPeptide);
                if (joinTables.Contains(ProteinToAnalysis)) newJoinTables.Add(PeptideSpectrumMatchToAnalysis);
                if (joinTables.Contains(ProteinToSpectrum)) newJoinTables.Add(PeptideSpectrumMatchToSpectrum);
                if (joinTables.Contains(ProteinToPeptideModification)) newJoinTables.Add(PeptideSpectrumMatchToPeptideModification);
                if (joinTables.Contains(ProteinToModification)) newJoinTables.Add(PeptideSpectrumMatchToModification);
                if (joinTables.Contains(ProteinToSpectrumSource)) newJoinTables.Add(PeptideSpectrumMatchToSpectrumSource);
                if (joinTables.Contains(ProteinToSpectrumSourceGroupLink)) newJoinTables.Add(PeptideSpectrumMatchToSpectrumSourceGroupLink);
                if (joinTables.Contains(ProteinToSpectrumSourceGroup)) newJoinTables.Add(PeptideSpectrumMatchToSpectrumSourceGroup);
                joinTables = newJoinTables.ToArray();
            }
        }

        public string GetBasicQueryStringSQL()
        {
            return String.Format("FROM PeptideSpectrumMatch psm " +
                                 "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide " +
                                 "WHERE psm.QValue <= {0} " +
                                 "GROUP BY pi.Protein " +
                                 "HAVING {1} <= COUNT(DISTINCT (psm.Peptide || ' ' || psm.MonoisotopicMass || ' ' || psm.Charge)) AND " +
                                 "       {2} <= COUNT(DISTINCT psm.Spectrum)",
                                 MaximumQValue,
                                 MinimumDistinctPeptidesPerProtein,
                                 MinimumSpectraPerProtein);
        }

        public string GetBasicQueryString (string fromTable, params string[] joinTables)
        {
            var joins = new Map<int, object>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    joins.Add(joins.Count, branch);

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            return query.ToString();
        }

        public string GetFilteredQueryString (string fromTable, params string[] joinTables)
        {
            optimizeQueryOrder(ref fromTable, ref joinTables);

            var joins = new Map<int, object>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    joins.Add(joins.Count, branch);

            // these different condition sets are AND'd together, but within each set (except for mods) they are OR'd
            var proteinConditions = new List<string>();
            var clusterConditions = new List<string>();
            var peptideConditions = new List<string>();
            var spectrumConditions = new List<string>();
            var modConditions = new List<string>();
            var otherConditions = new List<string>();

            if (fromTable == FromProtein)
            {
                if (!Peptide.IsNullOrEmpty() || !AminoAcidOffset.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!PeptideGroup.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                // if filtered on modification, the modification joins should come first
                if (HasModificationFilter)
                    foreach (var branch in ProteinToPeptideModification.Split(';').Reverse())
                        joins.Add((joins.Count == 0 ? 0 : joins.Min.Key) - 1, branch);

                if (!DistinctMatchKey.IsNullOrEmpty() || !Analysis.IsNullOrEmpty() ||
                    !Spectrum.IsNullOrEmpty() || !Charge.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptideSpectrumMatch.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSource.IsNullOrEmpty())
                    foreach (var branch in ProteinToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSourceGroup.IsNullOrEmpty())
                    foreach (var branch in ProteinToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }
            else if (fromTable == FromPeptideSpectrumMatch)
            {
                if (!Cluster.IsNullOrEmpty() || !ProteinGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToProtein.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!Protein.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!AminoAcidOffset.IsNullOrEmpty())
                {
                    // MaxValue indicates any peptide at a protein C-terminus,
                    // so the Protein table must be joined to access the Length column
                    string path = AminoAcidOffset.Contains(Int32.MaxValue) ? PeptideSpectrumMatchToProtein : PeptideSpectrumMatchToPeptideInstance;
                    foreach (var branch in path.Split(';'))
                        joins.Add(joins.Count, branch);
                }

                if (!PeptideGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                // if filtered on modification, the modification joins should come first
                if (HasModificationFilter)
                    foreach (var branch in PeptideSpectrumMatchToPeptideModification.Split(';').Reverse())
                        joins.Add((joins.Count == 0 ? 0 : joins.Min.Key) - 1, branch);

                if (!SpectrumSource.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSourceGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }

            // if filtered on modification, the modification joins should be inner joins instead of outer joins
            if (HasModificationFilter)
                foreach (var pair in joins)
                    pair.Value = ((string) pair.Value).Replace("LEFT JOIN psm.Mod", "JOIN psm.Mod")
                                                      .Replace("LEFT JOIN pm.Mod", "JOIN pm.Mod");

            if (!Cluster.IsNullOrEmpty())
                clusterConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()).ToArray())));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()).ToArray())));

            if (!Protein.IsNullOrEmpty())
            {
                string column = fromTable == FromProtein || joins.Any(o => ((string) o.Value).EndsWith(" pro")) ? "pro.id" : "pi.Protein.id";
                proteinConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Protein.Select(o => o.Id.ToString()).ToArray())));
            }

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()).ToArray())));

            if (!Peptide.IsNullOrEmpty())
            {
                string column = joins.Any(o => ((string) o.Value).EndsWith(" pi")) ? "pi.Peptide.id" : "psm.Peptide.id";
                peptideConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Peptide.Select(o => o.Id.ToString()).ToArray())));
            }

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("psm.DistinctMatchKey IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o=> o.Key).ToArray())));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()).ToArray())));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification.id IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()).ToArray())));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()).ToArray())));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis.id IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()).ToArray())));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.id IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.Source.id IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group.id IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()).ToArray())));

            if (!AminoAcidOffset.IsNullOrEmpty())
            {
                var offsetConditions = new List<string>();
                foreach (int offset in AminoAcidOffset)
                {
                    if (offset <= 0)
                        offsetConditions.Add("pi.Offset = 0"); // protein N-terminus
                    else if (offset == Int32.MaxValue)
                        offsetConditions.Add("pi.Offset+pi.Length = pro.Length"); // protein C-terminus
                    else
                        offsetConditions.Add(String.Format("(pi.Offset <= {0} AND pi.Offset+pi.Length > {0})", offset));
                }

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions.ToArray()) + ")");
            }

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions.ToArray()) + ")");
            if (clusterConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", clusterConditions.ToArray()) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions.ToArray()) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions.ToArray()) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions.ToArray()) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions.ToArray()) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        public string GetFilteredSqlWhereClause ()
        {
            // these different condition sets are AND'd together, but within each set (except for mods) they are OR'd
            var proteinConditions = new List<string>();
            var peptideConditions = new List<string>();
            var spectrumConditions = new List<string>();
            var modConditions = new List<string>();
            var otherConditions = new List<string>();

            if (!Cluster.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()).ToArray())));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()).ToArray())));

            if (!Protein.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pi.Protein IN ({0})", String.Join(",", Protein.Select(o => o.Id.ToString()).ToArray())));

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()).ToArray())));

            if (!Peptide.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pi.Peptide IN ({0})", String.Join(",", Peptide.Select(o => o.Id.ToString()).ToArray())));

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("IFNULL(dm.DistinctMatchKey, " +
                                                    DistinctMatchFormat.SqlExpression +
                                                    ") IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o => o.Key).ToArray())));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()).ToArray())));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()).ToArray())));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()).ToArray())));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()).ToArray())));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("s.Source IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group_ IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()).ToArray())));

            if (!AminoAcidOffset.IsNullOrEmpty())
            {
                var offsetConditions = new List<string>();
                foreach (int offset in AminoAcidOffset)
                {
                    if (offset <= 0)
                        offsetConditions.Add("pi.Offset = 0"); // protein N-terminus
                    else if (offset == Int32.MaxValue)
                        offsetConditions.Add("pi.Offset+pi.Length = pro.Length"); // protein C-terminus
                    else
                        offsetConditions.Add(String.Format("(pi.Offset <= {0} AND pi.Offset+pi.Length > {0})", offset));
                }

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions.ToArray()) + ")");
            }

            var query = new StringBuilder();

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions.ToArray()) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions.ToArray()) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions.ToArray()) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions.ToArray()) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions.ToArray()) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }
        #endregion
    }

    /// <summary>
    /// Read-only wrapper of DataFilter that is safe to use as a Dictionary or Hashtable key.
    /// </summary>
    public class DataFilterKey
    {
        private DataFilter DataFilter { get; set; }

        public DataFilterKey (DataFilter dataFilter) { DataFilter = new DataFilter(dataFilter); }

        public override bool Equals (object obj)
        {
            DataFilterKey other = obj as DataFilterKey;
            if (other == null)
                return false;
            return DataFilter.Equals(other.DataFilter);
        }

        public override int GetHashCode ()
        {
            return DataFilter.MaximumQValue.GetHashCode() ^
                   DataFilter.MinimumDistinctPeptidesPerProtein.GetHashCode() ^
                   DataFilter.MinimumSpectraPerProtein.GetHashCode() ^
                   DataFilter.MinimumAdditionalPeptidesPerProtein.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctMatch.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctPeptide.GetHashCode() ^
                   DataFilter.MaximumProteinGroupsPerPeptide.GetHashCode() ^
                   NullSafeHashCode(DataFilter.Cluster) ^
                   NullSafeHashCode(DataFilter.ProteinGroup) ^
                   NullSafeHashCode(DataFilter.PeptideGroup) ^
                   NullSafeHashCode(DataFilter.Protein) ^
                   NullSafeHashCode(DataFilter.Peptide) ^
                   NullSafeHashCode(DataFilter.DistinctMatchKey) ^
                   NullSafeHashCode(DataFilter.Modifications) ^
                   NullSafeHashCode(DataFilter.ModifiedSite) ^
                   NullSafeHashCode(DataFilter.Charge) ^
                   NullSafeHashCode(DataFilter.Analysis) ^
                   NullSafeHashCode(DataFilter.Spectrum) ^
                   NullSafeHashCode(DataFilter.SpectrumSource) ^
                   NullSafeHashCode(DataFilter.SpectrumSourceGroup) ^
                   NullSafeHashCode(DataFilter.AminoAcidOffset) ^
                   NullSafeHashCode(DataFilter.Composition);
        }

        private static int NullSafeHashCode<T> (IEnumerable<T> obj)
        {
            if (obj == null)
                return 0;

            int hash = 0;
            foreach(T item in obj)
                hash ^= item.GetHashCode();
            return hash;
        }
    }

    public class TotalCounts
    {
        public TotalCounts()
        {
        }

        public TotalCounts(NHibernate.ISession session)
        {
            var proteinLevelSummary = session.CreateSQLQuery(@"SELECT IFNULL(COUNT(DISTINCT pro.Cluster), 0),
                                                                      IFNULL(COUNT(DISTINCT pro.ProteinGroup), 0),
                                                                      IFNULL(COUNT(DISTINCT pro.Id), 0),
                                                                      IFNULL(SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END), 0)
                                                               FROM Protein pro
                                                              ").UniqueResult<object[]>();
            Clusters = Convert.ToInt32(proteinLevelSummary[0]);
            ProteinGroups = Convert.ToInt32(proteinLevelSummary[1]);
            Proteins = Convert.ToInt32(proteinLevelSummary[2]);
            double decoyProteins = Convert.ToDouble(proteinLevelSummary[3]);
            ProteinFDR = 2 * decoyProteins / Proteins;

            DistinctPeptides = Convert.ToInt32(session.CreateSQLQuery("SELECT COUNT(*) FROM Peptide").UniqueResult());
            DistinctMatches = Convert.ToInt32(session.CreateSQLQuery("SELECT COUNT(DISTINCT DistinctMatchId) FROM DistinctMatch").UniqueResult());
            FilteredSpectra = Convert.ToInt32(session.CreateSQLQuery("SELECT COUNT(*) FROM Spectrum").UniqueResult());

            // get the count of peptides that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
            var peptideLevelDecoys = session.CreateSQLQuery(@"SELECT COUNT(Peptide)
                                                              FROM (SELECT pep.Id AS Peptide,
                                                                           COUNT(DISTINCT pro.Id) AS Proteins,
                                                                           SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys,
                                                                           CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy
                                                                    FROM Peptide pep
                                                                    JOIN PeptideInstance pi ON pep.Id=pi.Peptide
                                                                    JOIN Protein pro ON pi.Protein=pro.Id
                                                                    GROUP BY pep.Id
                                                                    HAVING Proteins=Decoys OR Decoys=0
                                                                   )
                                                              GROUP BY IsDecoy
                                                              ORDER BY IsDecoy
                                                             ").List<long>();
            PeptideFDR = 2.0 * peptideLevelDecoys[1] / peptideLevelDecoys.Sum();

            // get the count of spectra that are unambiguously targets or decoys (# of Proteins = # of Decoys OR # of Decoys = 0)
            var spectrumLevelDecoys = session.CreateSQLQuery(@"SELECT COUNT(Spectrum)
                                                               FROM (SELECT psm.Spectrum,
                                                                            COUNT(DISTINCT pro.Id) AS Proteins,
                                                                            SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) AS Decoys,
                                                                            CASE WHEN SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS IsDecoy
                                                                     FROM PeptideSpectrumMatch psm
                                                                     JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide
                                                                     JOIN Protein pro ON pi.Protein=pro.Id
                                                                     GROUP BY psm.Spectrum
                                                                     HAVING Proteins=Decoys OR Decoys=0
                                                                    )
                                                               GROUP BY IsDecoy
                                                               ORDER BY IsDecoy
                                                              ").List<long>();
            SpectrumFDR = 2.0 * spectrumLevelDecoys[1] / spectrumLevelDecoys.Sum();
        }

        public int Clusters { get; private set; }
        public int ProteinGroups { get; private set; }
        public int Proteins { get; private set; }
        public int DistinctPeptides { get; private set; }
        public int DistinctMatches { get; private set; }
        public int FilteredSpectra { get; private set; }
        public double ProteinFDR { get; private set; }
        public double PeptideFDR { get; private set; }
        public double SpectrumFDR { get; private set; }
    }

    public class ViewFilterEventArgs : EventArgs
    {
        public ViewFilterEventArgs(DataFilter viewFilter) { ViewFilter = viewFilter; }
        public DataFilter ViewFilter { get; private set; }
    }
}
