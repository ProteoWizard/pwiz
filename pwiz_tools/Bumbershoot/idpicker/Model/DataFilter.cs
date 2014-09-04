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
        public virtual int MinimumDistinctPeptides { get; set; }
        public virtual int MinimumSpectra { get; set; }
        public virtual int MinimumAdditionalPeptides { get; set; }
        public virtual bool GeneLevelFiltering { get; set; }
        public virtual DistinctMatchFormat DistinctMatchFormat { get; set; }
        public virtual int MinimumSpectraPerDistinctMatch { get; set; }
        public virtual int MinimumSpectraPerDistinctPeptide { get; set; }
        public virtual int MaximumProteinGroupsPerPeptide { get; set; }

        public virtual TotalCounts TotalCounts { get; set; }


        public PersistentDataFilter() { }

        public PersistentDataFilter(PersistentDataFilter other)
        {
            MaximumQValue = other.MaximumQValue;
            MinimumDistinctPeptides = other.MinimumDistinctPeptides;
            MinimumSpectra = other.MinimumSpectra;
            MinimumAdditionalPeptides = other.MinimumAdditionalPeptides;
            GeneLevelFiltering = other.GeneLevelFiltering;
            DistinctMatchFormat = other.DistinctMatchFormat;
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
                       MinimumDistinctPeptides == other.MinimumDistinctPeptides &&
                       MinimumSpectra == other.MinimumSpectra &&
                       MinimumAdditionalPeptides == other.MinimumAdditionalPeptides &&
                       GeneLevelFiltering == other.GeneLevelFiltering &&
                       DistinctMatchFormat == other.DistinctMatchFormat &&
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
        public class FilteringProgressEventArgs : UpdateMessageProxy
        {
            public FilteringProgressEventArgs() {}

            public FilteringProgressEventArgs(string stage, int stageIndex, int stagesTotal, Exception ex)
            {
                CompletedFilters = stageIndex;
                TotalFilters = stagesTotal;
                FilteringStage = stage;
                FilteringException = ex;
            }

            public int CompletedFilters { get { return IterationIndex; } protected set { IterationIndex = value; } }
            public int TotalFilters { get { return IterationCount; } protected set { IterationCount = value; } }

            public string FilteringStage { get { return Message; } protected set { Message = value; } }
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
            MinimumDistinctPeptides = Properties.Settings.Default.DefaultMinDistinctPeptides;
            MinimumSpectra = Properties.Settings.Default.DefaultMinSpectra;
            MinimumAdditionalPeptides = Properties.Settings.Default.DefaultMinAdditionalPeptides;
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

            GeneGroup = other.GeneGroup == null ? null : new List<int>(other.GeneGroup);
            Gene = other.Gene == null ? null : new List<string>(other.Gene);
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
        public int MinimumDistinctPeptides { get { return PersistentDataFilter.MinimumDistinctPeptides; } set { PersistentDataFilter.MinimumDistinctPeptides = value; } }
        public int MinimumSpectra { get { return PersistentDataFilter.MinimumSpectra; } set { PersistentDataFilter.MinimumSpectra = value; } }
        public int MinimumAdditionalPeptides { get { return PersistentDataFilter.MinimumAdditionalPeptides; } set { PersistentDataFilter.MinimumAdditionalPeptides = value; } }
        public bool GeneLevelFiltering { get { return PersistentDataFilter.GeneLevelFiltering; } set { PersistentDataFilter.GeneLevelFiltering = value; } }
        public DistinctMatchFormat DistinctMatchFormat { get { return PersistentDataFilter.DistinctMatchFormat; } set { PersistentDataFilter.DistinctMatchFormat = value; } }
        public int MinimumSpectraPerDistinctMatch { get { return PersistentDataFilter.MinimumSpectraPerDistinctMatch; } set { PersistentDataFilter.MinimumSpectraPerDistinctMatch = value; } }
        public int MinimumSpectraPerDistinctPeptide { get { return PersistentDataFilter.MinimumSpectraPerDistinctPeptide; } set { PersistentDataFilter.MinimumSpectraPerDistinctPeptide = value; } }
        public int MaximumProteinGroupsPerPeptide { get { return PersistentDataFilter.MaximumProteinGroupsPerPeptide; } set { PersistentDataFilter.MaximumProteinGroupsPerPeptide = value; } }

        public IList<int> GeneGroup { get; set; }
        public IList<string> Gene { get; set; }
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
            get
            {
                return !GeneGroup.IsNullOrEmpty() || !Gene.IsNullOrEmpty() || !Cluster.IsNullOrEmpty() ||
                       !ProteinGroup.IsNullOrEmpty() || !Protein.IsNullOrEmpty() || !AminoAcidOffset.IsNullOrEmpty();
            }
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
                   NullSafeSequenceEqual(GeneGroup, other.GeneGroup) &&
                   NullSafeSequenceEqual(Gene, other.Gene) &&
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
            newFilter.GeneGroup = NullSafeSequenceUnion(newFilter.GeneGroup, rhs.GeneGroup);
            newFilter.Gene = NullSafeSequenceUnion(newFilter.Gene, rhs.Gene);
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
            toStringHelper("Gene group", GeneGroup, result);
            toStringHelper("Gene", Gene, result);
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
                                 "Distinct peptides per {7} ≥ {1}; " +
                                 "Spectra per {7} ≥ {2}; " +
                                 "Additional peptides per {7} ≥ {3}" +
                                 "Spectra per distinct match ≥ {4}; " +
                                 "Spectra per distinct peptide ≥ {5}; ",
                                 "Protein groups per peptide ≤ {6}",
                                 MaximumQValue,
                                 MinimumDistinctPeptides,
                                 MinimumSpectra,
                                 MinimumAdditionalPeptides,
                                 MinimumSpectraPerDistinctMatch,
                                 MinimumSpectraPerDistinctPeptide,
                                 MaximumProteinGroupsPerPeptide,
                                 GeneLevelFiltering ? "gene" : "protein");
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
            /*var existingFilter = session.Query<PersistentDataFilter>().ToList().SingleOrDefault(o => o.Equals(PersistentDataFilter));
            if (existingFilter != null)
            {
                session.Delete(existingFilter);
                session.Flush();
            }

            // increment Id and save the filter as the most recent one
            PersistentDataFilter.Id = Convert.ToInt64(session.CreateSQLQuery("SELECT IFNULL(MAX(Id), 0)+1 FROM FilterHistory").UniqueResult());
            session.Save(PersistentDataFilter);*/
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

        public void ApplyBasicFilters(NHibernate.ISession session)
        {
            // free up memory
            session.Clear();

            var filter = new Filterer();
            filter.Config.MaxFDRScore = MaximumQValue;
            filter.Config.MinDistinctPeptides = MinimumDistinctPeptides;
            filter.Config.MinSpectra = MinimumSpectra;
            filter.Config.MinAdditionalPeptides = MinimumAdditionalPeptides;
            filter.Config.GeneLevelFiltering = GeneLevelFiltering;
            filter.Config.DistinctMatchFormat.IsAnalysisDistinct = DistinctMatchFormat.IsAnalysisDistinct;
            filter.Config.DistinctMatchFormat.IsChargeDistinct = DistinctMatchFormat.IsChargeDistinct;
            filter.Config.DistinctMatchFormat.AreModificationsDistinct = DistinctMatchFormat.AreModificationsDistinct;
            filter.Config.DistinctMatchFormat.ModificationMassRoundToNearest = Convert.ToDouble(DistinctMatchFormat.ModificationMassRoundToNearest.GetValueOrDefault(1.0m));
            filter.Config.MinSpectraPerDistinctMatch = MinimumSpectraPerDistinctMatch;
            filter.Config.MinSpectraPerDistinctPeptide = MinimumSpectraPerDistinctPeptide;
            filter.Config.MaxProteinGroupsPerPeptide = MaximumProteinGroupsPerPeptide;

            var ilr = new pwiz.CLI.util.IterationListenerRegistry();
            ilr.addListener(new IterationListenerProxy<FilteringProgressEventArgs>(FilteringProgress), 1);

            try
            {
                filter.Filter(session.Connection.GetDataSource(), ilr);

                TotalCounts = new TotalCounts(session);
            }
            catch(Exception e)
            {
                OnFilteringProgress(new FilteringProgressEventArgs("", 0, 0, e));
            }
        }


        #region Implementation of basic filters

        public void RecalculateAggregateQuantitationData(NHibernate.ISession session)
        {
            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            AggregateQuantitationData(session);

            if (useScopedTransaction)
                session.Transaction.Commit();
        }

        bool AggregateQuantitationData(NHibernate.ISession session)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Aggregating quantitation data...", 1, 1, null)))
                return true;

            session.CreateSQLQuery(@"DELETE FROM PeptideQuantitation;
                                     INSERT INTO PeptideQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT psm.Peptide, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY psm.Peptide;
                                    ").ExecuteUpdate();

            session.CreateSQLQuery(@"DELETE FROM DistinctMatchQuantitation;
                                     INSERT INTO DistinctMatchQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT dm.DistinctMatchKey, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN DistinctMatch dm ON psm.Id=dm.PsmId
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY dm.DistinctMatchKey;
                                    ").ExecuteUpdate();

            session.CreateSQLQuery(@"DELETE FROM ProteinQuantitation;
                                     INSERT INTO ProteinQuantitation (Id, iTRAQ_ReporterIonIntensities, TMT_ReporterIonIntensities, PrecursorIonIntensity)
                                        SELECT pi.Protein, DISTINCT_DOUBLE_ARRAY_SUM(iTRAQ_ReporterIonIntensities), DISTINCT_DOUBLE_ARRAY_SUM(TMT_ReporterIonIntensities), SUM(PrecursorIonIntensity)
                                        FROM PeptideSpectrumMatch psm
                                        JOIN PeptideInstance pi ON psm.Peptide=pi.Peptide
                                        JOIN SpectrumQuantitation sq ON psm.Spectrum=sq.Id
                                        GROUP BY pi.Protein;
                                    ").ExecuteUpdate();
            return false;
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
                                 MinimumDistinctPeptides,
                                 MinimumSpectra);
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
                if (!Cluster.IsNullOrEmpty() || !ProteinGroup.IsNullOrEmpty() || !GeneGroup.IsNullOrEmpty() || !Gene.IsNullOrEmpty())
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

            if (!GeneGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.GeneGroup IN ({0})", String.Join(",", GeneGroup.Select(o => o.ToString()))));

            if (!Gene.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.GeneId IN ('{0}')", String.Join("','", Gene)));

            if (!Cluster.IsNullOrEmpty())
                clusterConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()))));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()))));

            if (!Protein.IsNullOrEmpty())
            {
                string column = fromTable == FromProtein || joins.Any(o => ((string) o.Value).EndsWith(" pro")) ? "pro.id" : "pi.Protein.id";
                proteinConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Protein.Select(o => o.Id.ToString()))));
            }

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()))));

            if (!Peptide.IsNullOrEmpty())
            {
                string column = joins.Any(o => ((string) o.Value).EndsWith(" pi")) ? "pi.Peptide.id" : "psm.Peptide.id";
                peptideConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Peptide.Select(o => o.Id.ToString()))));
            }

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("psm.DistinctMatchKey IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o=> o.Key))));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()))));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification.id IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()))));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()))));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis.id IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()))));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.id IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()))));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.Source.id IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()))));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group.id IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()))));

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

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions) + ")");
            }

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions) + ")");
            if (clusterConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", clusterConditions) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions) + ")");

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

            if (!GeneGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.GeneGroup IN ({0})", String.Join(",", GeneGroup.Select(o => o.ToString()))));

            if (!Gene.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.GeneId IN ('{0}')", String.Join("','", Gene)));

            if (!Cluster.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()))));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()))));

            if (!Protein.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pi.Protein IN ({0})", String.Join(",", Protein.Select(o => o.Id.ToString()))));

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()))));

            if (!Peptide.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pi.Peptide IN ({0})", String.Join(",", Peptide.Select(o => o.Id.ToString()))));

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("IFNULL(dm.DistinctMatchKey, " +
                                                    DistinctMatchFormat.SqlExpression +
                                                    ") IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o => o.Key))));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()))));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()))));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()))));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()))));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()))));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("s.Source IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()))));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group_ IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()))));

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

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions) + ")");
            }

            var query = new StringBuilder();

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions));
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
                   DataFilter.MinimumDistinctPeptides.GetHashCode() ^
                   DataFilter.MinimumSpectra.GetHashCode() ^
                   DataFilter.MinimumAdditionalPeptides.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctMatch.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctPeptide.GetHashCode() ^
                   DataFilter.MaximumProteinGroupsPerPeptide.GetHashCode() ^
                   NullSafeHashCode(DataFilter.GeneGroup) ^
                   NullSafeHashCode(DataFilter.Gene) ^
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
                                                                      IFNULL(COUNT(DISTINCT pro.GeneGroup), 0),
                                                                      IFNULL(COUNT(DISTINCT pro.GeneId), 0),
                                                                      IFNULL(SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END), 0)
                                                               FROM Protein pro
                                                              ").UniqueResult<object[]>();
            int column = -1;
            Clusters = Convert.ToInt32(proteinLevelSummary[++column]);
            ProteinGroups = Convert.ToInt32(proteinLevelSummary[++column]);
            Proteins = Convert.ToInt32(proteinLevelSummary[++column]);
            GeneGroups = Convert.ToInt32(proteinLevelSummary[++column]);
            Genes = Convert.ToInt32(proteinLevelSummary[++column]);
            double decoyProteins = Convert.ToDouble(proteinLevelSummary[++column]);
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
            // without both targets and decoys, FDR can't be calculated
            if (peptideLevelDecoys.Count == 2)
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
            // without both targets and decoys, FDR can't be calculated
            if(spectrumLevelDecoys.Count == 2)
                SpectrumFDR = 2.0 * spectrumLevelDecoys[1] / spectrumLevelDecoys.Sum();
        }

        public int Clusters { get; private set; }
        public int ProteinGroups { get; private set; }
        public int Proteins { get; private set; }
        public int GeneGroups { get; private set; }
        public int Genes { get; private set; }
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
