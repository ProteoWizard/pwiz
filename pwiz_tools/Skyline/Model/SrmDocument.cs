/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
//TODO transitions and transition group
// Move stuff to refinement

// Note to those extending the document model:
// All objects participating in the document model must be immutable.
// An immutable document has many advantages, primary among those are 
// simplifying synchronization in a multi-threaded system, allowing
// eventual consistency, and maintaining a history of entire documents
// at a cost of only the depth of the change in the document tree, with
// many documents sharing the majority of their in memory objects.  This
// allows undo/redo that simply points to a document in the history.
//
// Simple immutable objects may have only a constructor and property
// getters with private setters.  More complex objects should derive
// from the class Immutable.  The should still have only property getters
// and private setters, and should only change at 3 times:
//     1. In a constructor
//     2. During deserialization (immediately after construction)
//     3. In a Change<property>() method, using ImClone()
// Directly modifying an existing object in memory after it has been
// fully constructed, will break undo/redo, since the object may be
// referenced by many documents in the history.
//
// More complex objects should also consider implementing IValidating
// to ensure that the class remains valid in all three of the cases
// described above.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Exposes get/set methods for the container of an immutable <see cref="SrmDocument"/>.
    /// </summary>
    public interface IDocumentContainer
    {
        /// <summary>
        /// Get the current contained document.
        /// </summary>
        SrmDocument Document { get; }

        /// <summary>
        /// Get the path to the current save location of the document, or
        /// null if the document is not saved.
        /// </summary>
        string DocumentFilePath { get; }

        /// <summary>
        /// Set the contained document, if the original document is the same as the
        /// current document.
        /// </summary>
        /// <param name="docNew">A new version of the current document</param>
        /// <param name="docOriginal">The version from which the new version was derived</param>
        /// <returns>True if the document was successfully set</returns>
        bool SetDocument(SrmDocument docNew, SrmDocument docOriginal);

        /// <summary>
        /// Adds an event handler to the container's document changed event. The
        /// event handler must be thread safe, as it may be called on any thread.
        /// </summary>
        /// <param name="listener">The event handler to add</param>
        void Listen(EventHandler<DocumentChangedEventArgs> listener);

        /// <summary>
        /// Removes an event handler from the container's document changed event.
        /// </summary>
        /// <param name="listener">The event handler to remove</param>
        void Unlisten(EventHandler<DocumentChangedEventArgs> listener);

        /// <summary>
        /// Returns true if the container is in teardown, and we should not attempt any document changes.
        /// </summary>
        bool IsClosing { get; }

        /// <summary>
        /// Tracking active background loaders for a container - helps in test harness SkylineWindow teardown
        /// </summary>
        IEnumerable<BackgroundLoader> BackgroundLoaders { get; }
        void AddBackgroundLoader(BackgroundLoader loader);
        void RemoveBackgroundLoader(BackgroundLoader loader);
    }

    /// <summary>
    /// Exposes get and event registering methods for the container of an
    /// immutable <see cref="SrmDocument"/>, for use in UI thread components.
    /// </summary>
    public interface IDocumentUIContainer : IDocumentContainer
    {
        /// <summary>
        /// Get the current document for display in the UI.
        /// </summary>
        SrmDocument DocumentUI { get; }

        /// <summary>
        /// Adds an event handler to the container's document UI changed event. The
        /// event handler must be thread safe, as it may be called on any thread.
        /// </summary>
        /// <param name="listener">The event handler to add</param>
        void ListenUI(EventHandler<DocumentChangedEventArgs> listener);

        /// <summary>
        /// Removes an event handler from the container's document UI changed event.
        /// </summary>
        /// <param name="listener">The event handler to remove</param>
        void UnlistenUI(EventHandler<DocumentChangedEventArgs> listener);

        /// <summary>
        /// Returns focus to the main document UI
        /// </summary>
        void FocusDocument();

        /// <summary>
        /// True if the UI is in the middle of an undo/redo operation
        /// </summary>
        bool InUndoRedo { get; }
    }

    /// <summary>
    /// EventArgs supplied with the <see cref="SkylineWindow.DocumentUIChangedEvent"/>.
    /// The previous document is supplied to allow localized modifications based
    /// on a diff between the two documents.
    /// </summary>
    public class DocumentChangedEventArgs : EventArgs
    {
        public DocumentChangedEventArgs(SrmDocument documentPrevious, bool isOpeningFile = false, bool inSelUpdateLock = false)
        {
            DocumentPrevious = documentPrevious;
            IsInSelUpdateLock = inSelUpdateLock;
            IsOpeningFile = isOpeningFile;
        }

        public SrmDocument DocumentPrevious { get; private set; }

        /// <summary>
        /// True when SequenceTree.IsInUpdateLock is set, which means the selection
        /// cannot be trusted as reflecting the current document.
        /// </summary>
        public bool IsInSelUpdateLock { get; private set; }

        /// <summary>
        /// True when the document change is caused by opening a file
        /// </summary>
        public bool IsOpeningFile { get; private set; }
    }

    /// <summary>
    /// Root <see cref="Identity"/> class for a document.
    /// </summary>
    public class SrmDocumentId : Identity
    {
    }

    /// <summary>
    /// The <see cref="SrmDocument"/> class and all of the model objects it includes
    /// are entirely immutable.  This means a reference to document is always entirely
    /// complete, and requires no synchronization to use with multiple threads.
    /// <para>
    /// All changes produce a new document with an incremented <see cref="RevisionIndex"/>.
    /// On first consideration of this model, it may sound incredibly expensive, but,
    /// in fact, the model is used in source code control systems like Subversion,
    /// where changing a single file produces a new repository revision without
    /// necessarily copying every file in the tree.  Only the path from the new
    /// immutable child to the root need be modified.
    /// </para><para>
    /// The model for modifying a document within a multi-threaded system, then
    /// becomes:
    /// <list type="number">
    /// <item><description>Acquire a reference to the current document</description></item>
    /// <item><description>Create a modified document based on the revision acquired</description></item>
    /// <item><description>Use <see cref="Interlocked.CompareExchange(ref object,object,object)"/>
    ///                    to set the master reference, if it is still equal to the one acquired</description></item>
    /// <item><description>If the attempt to set fails, return to the first step.</description></item>
    /// </list>
    /// </para><para>
    /// This also allows the undo/redo stacks to become a simple history of documents,
    /// rather than a record of actions taken to modify a mutable document.
    /// </para>
    /// </summary>
    [XmlRoot(@"srm_settings")]
    public class SrmDocument : DocNodeParent, IXmlSerializable
    {
        /// <summary>
        /// Document extension on disk
        /// </summary>
        public const string EXT = ".sky";

        public static string FILTER_DOC
        {
            get { return TextUtil.FileDialogFilter(Resources.SrmDocument_FILTER_DOC_Skyline_Documents, EXT); }
        }

		public static string FILTER_DOC_AND_SKY_ZIP
        {
            // Used only in the open file dialog.
            get
            {
                return TextUtil.FileDialogFilter(Resources.SrmDocument_FILTER_DOC_AND_SKY_ZIP_Skyline_Files, EXT,
                                                 SrmDocumentSharing.EXT_SKY_ZIP, SkypFile.EXT);
            }    
        }

        public static readonly DocumentFormat FORMAT_VERSION = DocumentFormat.CURRENT;

        public const int MAX_PEPTIDE_COUNT = 200 * 1000;
        public const int MAX_TRANSITION_COUNT = 5 * 1000 * 1000;

        public static int _maxTransitionCount = Install.Is64Bit ? MAX_TRANSITION_COUNT : MAX_TRANSITION_COUNT/5;   // To keep from running out of memory on 32-bit

        public static int MaxTransitionCount
        {
            get { return _maxTransitionCount; }
        }

        /// <summary>
        /// For testing to avoid needing to create 5,000,000 transitions to test transition count limits
        /// </summary>
        public static void SetTestMaxTransitonCount(int max)
        {
            _maxTransitionCount = max;
        }

        // Version of this document in deserialized XML

        public SrmDocument(SrmSettings settings)
            : base(new SrmDocumentId(), Annotations.EMPTY, new PeptideGroupDocNode[0], false)
        {
            FormatVersion = FORMAT_VERSION;
            Settings = settings;
            AuditLog = new AuditLogList();
            SetDocumentType(); // Note proteomics vs  molecule vs mixed (as we're empty, will be set to none)
        }

        private SrmDocument(SrmDocument doc, SrmSettings settings, Action<SrmDocument> changeProps = null)
            : base(doc.Id, Annotations.EMPTY, doc.Children, false)
        {
            FormatVersion = doc.FormatVersion;
            RevisionIndex = doc.RevisionIndex;
            UserRevisionIndex = doc.UserRevisionIndex;
            Settings = doc.UpdateHasHeavyModifications(settings);
            AuditLog = doc.AuditLog;
            DocumentHash = doc.DocumentHash;
            DeferSettingsChanges = doc.DeferSettingsChanges;
            DocumentType = doc.DocumentType;

            if (changeProps != null)
                changeProps(this);
        }

        /// <summary>
        /// Notes document contents type: proteomic, small molecule, or mixed (empty reports as proteomic),
        /// which allows for quick discrimination between needs for proteomic and small molecule behavior.
        /// N.B. For construction time and <see cref="OnChangingChildren"/> only!!! Mustn't break immutabilty contract.
        ///
        /// </summary>
        private void SetDocumentType()
        {
            var hasPeptides = false;
            var hasSmallMolecules = false;
            foreach (var tg in MoleculeTransitionGroups)
            {
                hasPeptides |= !tg.IsCustomIon;
                hasSmallMolecules |= tg.IsCustomIon;
            }

            if (hasSmallMolecules && hasPeptides)
            {
                DocumentType = DOCUMENT_TYPE.mixed;
            }
            else if (hasSmallMolecules)
            {
                DocumentType = DOCUMENT_TYPE.small_molecules;
            }
            else if (hasPeptides)
            {
                DocumentType = DOCUMENT_TYPE.proteomic;
            }
            else
            {
                DocumentType = DOCUMENT_TYPE.none;
            }
            Settings = UpdateHasHeavyModifications(Settings);
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { 
            get { throw new InvalidOperationException();}
        }

        public DocumentFormat FormatVersion { get; private set; }

        /// <summary>
        /// Monotonically increasing index, incremented each time a modified
        /// document is created.  Works much like the revision count in Subversion.
        /// And the immutable document architecture itself may have its roots in
        /// source code control.
        /// 
        /// This index is in memory only, and is started at zero each time the
        /// document is loaded from disk.
        /// 
        /// Also, this index is not included in the document content equality
        /// functions, because doing so would break the main use case for document
        /// equality: unit testing.
        /// </summary>
        public int RevisionIndex { get; private set; }

        /// <summary>
        /// Much like RevisionIndex, only it is incremented each time the user
        /// changes the document. i.e. any time an Undo/Redo record is created.
        /// </summary>
        public int UserRevisionIndex { get; private set; }

        /// <summary>
        /// Document-wide settings information
        /// </summary>
        public SrmSettings Settings { get; private set; }

        /// <summary>
        /// Document hash that gets updated when the document is opened/saved
        /// </summary>
        public string DocumentHash { get; private set; }

        public AuditLogList AuditLog { get; private set; }

        public Targets Targets { get { return new Targets(this);} }

        public bool DeferSettingsChanges { get; private set; }

        /// <summary>
        /// Convenience access to the <see cref="MeasuredResults"/> for easier debugging.
        /// </summary>
        public MeasuredResults MeasuredResults { get { return Settings.MeasuredResults; } }

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { MoleculeGroups, Molecules, TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int MoleculeGroupCount { get { return GetCount((int)Level.MoleculeGroups); } }
        public int MoleculeCount { get { return GetCount((int)Level.Molecules); } }
        public int MoleculeTransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int MoleculeTransitionCount { get { return GetCount((int)Level.Transitions); } }

        // Convenience functions for ignoring non-proteomic (CustomIon) nodes - that is, getting only peptides
        public int PeptideGroupCount { get { return PeptideGroups.Count(); } } 
        public int PeptideCount { get { return Peptides.Count(); } } 
        public int PeptideTransitionGroupCount { get { return PeptideTransitionGroups.Count(); } } 
        public int PeptideTransitionCount { get { return PeptideTransitions.Count(); } }

        // Convenience functions for ignoring proteomic nodes - that is, getting only custom ions
        public int CustomIonCount { get { return CustomMolecules.Count(); } } 

        /// <summary>
        /// Quick access to document type proteomic/small_molecules/mixed, based on the assumption that 
        /// TransitionGroups are purely proteomic or small molecule, but the document is not.
        /// 
        /// Empty documents report as none.
        ///
        /// These enum names are used on persisted settings for UI mode, so don't rename them as
        /// it will confuse existing installations.
        /// 
        /// </summary>
        public enum DOCUMENT_TYPE
        {
            proteomic,  
            small_molecules,
            mixed,
            none // empty documents return this
        };
        public DOCUMENT_TYPE DocumentType { get; private set; }
        public bool IsEmptyOrHasPeptides { get { return DocumentType != DOCUMENT_TYPE.small_molecules; } }
        public bool HasPeptides { get { return DocumentType == DOCUMENT_TYPE.proteomic || DocumentType == DOCUMENT_TYPE.mixed; } }
        public bool HasSmallMolecules { get { return DocumentType == DOCUMENT_TYPE.small_molecules || DocumentType == DOCUMENT_TYPE.mixed; } }

        /// <summary>
        /// Return all <see cref="PeptideGroupDocNode"/>s of any kind
        /// </summary>
        public IEnumerable<PeptideGroupDocNode> MoleculeGroups
        {
            get 
            {
                return Children.Cast<PeptideGroupDocNode>();
            }
        }

        /// <summary>
        /// Return all <see cref="PeptideGroupDocNode"/>s that contain peptides
        /// </summary>
        public IEnumerable<PeptideGroupDocNode> PeptideGroups 
        {
            get
            {
                return MoleculeGroups.Where(p => p.IsProteomic);
            }
        }

        /// <summary>
        /// Return all <see cref="PeptideDocNode"/> of any kind
        /// </summary>
        public IEnumerable<PeptideDocNode> Molecules
        {
            get 
            {
                return MoleculeGroups.SelectMany(node => node.Molecules);
            }
        }

        /// <summary>
        /// Return all <see cref="PeptideDocNode"/> that are actual peptides
        /// </summary>
        public IEnumerable<PeptideDocNode> Peptides
        {
            get
            {
                return Molecules.Where(p => !p.Peptide.IsCustomMolecule);
            }
        }

        /// <summary>
        /// Return all <see cref="PeptideDocNode"/> that are custom molecules
        /// </summary>
        public IEnumerable<PeptideDocNode> CustomMolecules
        {
            get
            {
                return Molecules.Where(p => p.Peptide.IsCustomMolecule);
            }
        }

        /// <summary>
        /// Return all <see cref="TransitionGroupDocNode"/> of any kind
        /// </summary>
        public IEnumerable<TransitionGroupDocNode> MoleculeTransitionGroups
        {
            get 
            {
                return Molecules.SelectMany(node => node.Children.Cast<TransitionGroupDocNode>());
            }
        }

        /// <summary>
        /// Return all <see cref="TransitionGroupDocNode"/> whose members are peptide precursors
        /// </summary>
        public IEnumerable<TransitionGroupDocNode> PeptideTransitionGroups
        {
            get
            {
                return MoleculeTransitionGroups.Where(t => !t.TransitionGroup.Peptide.IsCustomMolecule);
            }
        }

        public IEnumerable<PeptidePrecursorPair> MoleculePrecursorPairs
        {
            get
            {
                return Molecules.SelectMany(
                    node => node.TransitionGroups.Select(nodeGroup => new PeptidePrecursorPair(node, nodeGroup)));
            }
        }

        public IEnumerable<PeptidePrecursorPair> PeptidePrecursorPairs
        {
            get
            {
                return Peptides.SelectMany(
                    node => node.TransitionGroups.Select(nodeGroup => new PeptidePrecursorPair(node, nodeGroup)));
            }
        }

        public IEnumerable<LibKey> MoleculeLibKeys
        {
            get
            {
                return Molecules.SelectMany(
                    node => node.TransitionGroups.Select(nodeGroup => nodeGroup.GetLibKey(Settings, node)));
            }
        }

        /// <summary>
        /// Return a list of <see cref="TransitionDocNode"/> of any kind
        /// </summary>
        public IEnumerable<TransitionDocNode> MoleculeTransitions
        {
            get
            {
                return MoleculeTransitionGroups.SelectMany(node => node.Children.Cast<TransitionDocNode>());
            }
        }

        /// <summary>
        /// Return a list of <see cref="TransitionDocNode"/> that are in peptides
        /// </summary>
        public IEnumerable<TransitionDocNode> PeptideTransitions
        {
            get
            {
                return MoleculeTransitions.Where(t => !t.Transition.Group.IsCustomIon);
            }
        }

        public HashSet<Target> GetRetentionTimeStandards()
        {
            try
            {
                return GetRetentionTimeStandardsOrThrow();
            }
            catch (Exception)
            {
                return new HashSet<Target>();
            }
        }

        public bool HasAllRetentionTimeStandards()
        {
            try
            {
                GetRetentionTimeStandardsOrThrow();
                return true;
            }
            catch (Exception)
            {
                return false;
           }
        }

        private HashSet<Target> GetRetentionTimeStandardsOrThrow()
        {
            var rtRegression = Settings.PeptideSettings.Prediction.RetentionTime;
            if (rtRegression == null || rtRegression.Calculator == null)
                return new HashSet<Target>();

            var regressionPeps = rtRegression.Calculator.GetStandardPeptides(Peptides.Select(
                nodePep => Settings.GetModifiedSequence(nodePep)));
            return new HashSet<Target>(regressionPeps);
        }

        /// <summary>
        /// True when any PeptideGroupDocNodes lack complete protein metadata
        /// </summary>
        public bool IsProteinMetadataPending { get; private set; }

        /// <summary>
        /// True if the parts of a Skyline document affected by a Save As command are loaded
        /// </summary>
        public bool IsSavable
        {
            get
            {
                // Results cache file must be fully created before a Save As, since it has
                // the same base name as the document
                return (!Settings.HasResults || Settings.MeasuredResults.IsLoaded) &&
                    // Document libraries also have the same base name as the document and must be copied
                       (!Settings.HasLibraries || !Settings.HasDocumentLibrary || Settings.PeptideSettings.Libraries.IsLoaded);
            }
        }

        /// <summary>
        /// Returns non-localized strings describing any unloadedness in the document
        /// TODO: there are still issues with this, like errors importing results
        /// </summary>
        public IEnumerable<string> NonLoadedStateDescriptions
        {
            get
            {
                string whyNot;
                if (Settings.HasResults && (whyNot = Settings.MeasuredResults.IsNotLoadedExplained) != null)
                    yield return @"Settings.MeasuredResults " + whyNot;
                if (Settings.HasLibraries && (whyNot = Settings.PeptideSettings.Libraries.IsNotLoadedExplained)!=null)
                    yield return @"Settings.PeptideSettings.Libraries: " + whyNot;
                if ((whyNot = IrtDbManager.IsNotLoadedDocumentExplained(this)) != null)
                    yield return whyNot;
                if ((whyNot = OptimizationDbManager.IsNotLoadedDocumentExplained(this)) != null)
                    yield return whyNot;
                if ((whyNot = DocumentRetentionTimes.IsNotLoadedExplained(Settings)) != null)
                    yield return whyNot;
                if ((whyNot = IonMobilityLibraryManager.IsNotLoadedDocumentExplained(this)) != null)
                    yield return whyNot;
                // BackgroundProteome?
            }
        }

        public IEnumerable<string> NonLoadedStateDescriptionsFull
        {
            get
            {
                foreach (var desc in NonLoadedStateDescriptions)
                    yield return desc;

                string whyNot;
                var pepSet = Settings.PeptideSettings;
                if ((whyNot = BackgroundProteomeManager.IsNotLoadedExplained(pepSet, pepSet.BackgroundProteome, true)) != null)
                    yield return whyNot;
            }
        }

        /// <summary>
        /// True if all parts of the document loaded by background loaders have completed loading
        /// TODO: there are still issues with this, like errors importing results
        /// </summary>
        public bool IsLoaded
        {
            get { return !NonLoadedStateDescriptions.Any(); }
        }

        public PeptideGroupDocNode FindPeptideGroup(PeptideGroup fastaSequence)
        {
            foreach (var peptideGroup in PeptideGroups)
            {
                if (peptideGroup.PeptideGroup.Sequence == fastaSequence.Sequence)
                    return peptideGroup;
            }
            return null;
        }

        public IdentityPath LastNodePath
        {
            get
            {
                DocNodeParent parent = this;
                IdentityPath path = IdentityPath.ROOT;
                while (parent != null && parent.Children.Count > 0)
                {
                    path = new IdentityPath(path, parent.Children[parent.Children.Count - 1].Id);

                    parent = parent.Children[parent.Children.Count - 1] as DocNodeParent;
                }
                return path;
            }
        }

        public SrmDocument ChangeDocumentHash(string hash)
        {
            return ChangeProp(ImClone(this), im => im.DocumentHash = hash);
        }

        public SrmDocument ChangeAuditLog(AuditLogList log)
        {
            return ChangeProp(ImClone(this), im => im.AuditLog = log);
        }

        public SrmDocument ChangeAuditLog(AuditLogEntry entries)
        {
            return ChangeAuditLog(new AuditLogList(entries));
        }

        private string GetMoleculeGroupId(string baseId)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (PeptideGroupDocNode nodeGroup in Children)
                ids.Add(nodeGroup.Name);

            int i = 1;
            while (ids.Contains(baseId + i))
                i++;
            return baseId + i;
        }

        public string GetSmallMoleculeGroupId()
        {
            return GetMoleculeGroupId(Resources.SrmDocument_GetSmallMoleculeGroupId_molecules);
        }

        public string GetPeptideGroupId(bool peptideList)
        {
            string baseId = peptideList
                ? Resources.SrmDocument_GetPeptideGroupId_peptides 
                : Resources.SrmDocument_GetPeptideGroupId_sequence;
            return GetMoleculeGroupId(baseId);
        }

        public bool CanTrigger(int? replicateIndex)
        {
            return Molecules.All(p => p.CanTrigger(replicateIndex));
        }

        public bool IsMixedPolarity()
        {
            return MoleculeTransitionGroups.Any(tg => tg.TransitionGroup.PrecursorCharge < 0) &&
                   MoleculeTransitionGroups.Any(tg => tg.TransitionGroup.PrecursorCharge > 0);
        }

        public bool CanSchedule(bool singleWindow)
        {
            return Settings.PeptideSettings.Prediction.CanSchedule(this, singleWindow
                        ? PeptidePrediction.SchedulingStrategy.single_window
                        : PeptidePrediction.SchedulingStrategy.all_variable_window);
        }

        private bool CalcIsProteinMetadataPending()
        {
            // Non proteomic molecules never do protein metadata searches
            var unsearched = (from pg in PeptideGroups where pg.ProteinMetadata.NeedsSearch() select pg);
            return unsearched.Any();
        }

        public SrmDocument IncrementUserRevisionIndex()
        {
            return ChangeProp(ImClone(this), im => im.UserRevisionIndex++);
        }

        /// <summary>
        /// Make sure every new copy of a document gets an incremented value
        /// for <see cref="RevisionIndex"/>.
        /// </summary>
        /// <param name="clone">The new copy of the document</param>
        /// <param name="indexReplaced">Index to a single replaced node, if that is why the children are changing</param>
        protected override IList<DocNode> OnChangingChildren(DocNodeParent clone, int indexReplaced)
        {
            if (ReferenceEquals(clone, this))
                return Children;

            SrmDocument docClone = (SrmDocument)clone;
            docClone.RevisionIndex = RevisionIndex + 1;

            if (!DeferSettingsChanges)
            {
                // Make sure peptide standards lists are up to date
                docClone.Settings = docClone.Settings.CachePeptideStandards(Children, docClone.Children);
            }

            // Note protein metadata readiness
            docClone.IsProteinMetadataPending = docClone.CalcIsProteinMetadataPending();

            // If iRT standards have changed, reset auto-calculated conversion to make sure they are
            // updated on a background thread
            if (!ReferenceEquals(Settings.GetPeptideStandards(StandardType.IRT),
                docClone.Settings.GetPeptideStandards(StandardType.IRT)) &&
                docClone.Settings.PeptideSettings.Prediction.RetentionTime != null)
            {
                docClone.Settings = docClone.Settings.ChangePeptidePrediction(p =>
                    p.ChangeRetentionTime(p.RetentionTime.ForceRecalculate()));
            }

            // Note document contents type: proteomic, small molecule, or mixed (empty reports as proteomic)
            if (!DeferSettingsChanges)
            {
                docClone.SetDocumentType();
            }
            
            // If this document has associated results, update the results
            // for any peptides that have changed.
            if (!Settings.HasResults || DeferSettingsChanges)
                return docClone.Children;

            // Store indexes to previous results in a dictionary for lookup
            var dictPeptideIdPeptide = new Dictionary<int, PeptideDocNode>();
            // Unless the normalization standards have changed, which require recalculating of all ratios
            if (ReferenceEquals(Settings.GetPeptideStandards(StandardType.GLOBAL_STANDARD),
                docClone.Settings.GetPeptideStandards(StandardType.GLOBAL_STANDARD)))
            {
                foreach (var nodePeptide in Molecules)
                {
                    if (nodePeptide != null)    // Or previous peptides were freed during command-line peak picking
                        dictPeptideIdPeptide.Add(nodePeptide.Peptide.GlobalIndex, nodePeptide);
                }
            }

            return docClone.UpdateResultsSummaries(docClone.Children, dictPeptideIdPeptide);
        }

        /// <summary>
        /// Update results for the changed peptides.  This needs to start
        /// at the peptide level, because peptides have useful peak picking information
        /// like predicted retention time, and multiple measured precursors.
        /// </summary>
        private IList<DocNode> UpdateResultsSummaries(IList<DocNode> children, IDictionary<int, PeptideDocNode> dictPeptideIdPeptide)
        {
            // Perform main processing for peptides in parallel
            var diffResults = new SrmSettingsDiff(Settings, true);
            var moleculeGroupPairs = GetMoleculeGroupPairs(children);
            var moleculeNodes = new PeptideDocNode[moleculeGroupPairs.Length];
            ParallelEx.For(0, moleculeGroupPairs.Length, i =>
            {
                var pair = moleculeGroupPairs[i];
                var nodePep = pair.NodeMolecule;
                int index = nodePep.Peptide.GlobalIndex;

                PeptideDocNode nodeExisting;
                if (dictPeptideIdPeptide.TryGetValue(index, out nodeExisting) &&
                        ReferenceEquals(nodeExisting, nodePep))
                    moleculeNodes[i] = nodePep;
                else
                    moleculeNodes[i] = nodePep.ChangeSettings(Settings, diffResults);
            });

            return RegroupMolecules(children, moleculeNodes);
        }

        /// <summary>
        /// Returns a flat list of <see cref="MoleculeGroupPair"/> in the document for use in parallel
        /// processing of all molecules in the document.
        /// </summary>
        public MoleculeGroupPair[] GetMoleculeGroupPairs()
        {
            return GetMoleculeGroupPairs(Children, MoleculeCount);
        }

        /// <summary>
        /// Returns a flat list of <see cref="MoleculeGroupPair"/>, given a list of <see cref="PeptideGroupDocNode"/>
        /// children, for use in parallel processing of all molecules in the children.
        /// </summary>
        private static MoleculeGroupPair[] GetMoleculeGroupPairs(IList<DocNode> children)
        {
            return GetMoleculeGroupPairs(children, children.Cast<PeptideGroupDocNode>().Sum(g => g.MoleculeCount));
        }

        /// <summary>
        /// Returns a flat list of <see cref="MoleculeGroupPair"/>, given a list of <see cref="PeptideGroupDocNode"/>
        /// children and the total number of molecules they contain, for use in parallel processing of all molecules
        /// in the children.
        /// </summary>
        private static MoleculeGroupPair[] GetMoleculeGroupPairs(IList<DocNode> children, int moleculeCount)
        {
            var result = new MoleculeGroupPair[moleculeCount];
            int currentMolecule = 0;
            foreach (PeptideGroupDocNode nodeMoleculeGroup in children)
            {
                foreach (var nodeMolecule in nodeMoleculeGroup.Molecules)
                {
                    result[currentMolecule++] = new MoleculeGroupPair(nodeMoleculeGroup, nodeMolecule);
                }
            }
            return result;
        }

        /// <summary>
        /// Regroup a flat list of molecules produced by iterating over the results of
        /// <see cref="GetMoleculeGroupPairs"/>
        /// </summary>
        /// <param name="children">A starting children of <see cref="PeptideGroupDocNode"/> objects</param>
        /// <param name="moleculeNodes">A flat list of <see cref="PeptideDocNode"/> objects</param>
        /// <param name="rankChildren">Function to rank peptides in their final list</param>
        /// <returns>A list of <see cref="PeptideGroupDocNode"/> objects with the original structure and the new <see cref="PeptideDocNode"/> objects</returns>
        private static IList<DocNode> RegroupMolecules(IList<DocNode> children, PeptideDocNode[] moleculeNodes,
            Func<PeptideGroupDocNode, IList<DocNode>, IList<DocNode>> rankChildren = null)
        {
            var newMoleculeGroups = new DocNode[children.Count];
            int moleculeNodeIndex = 0;
            for (int i = 0; i < newMoleculeGroups.Length; i++)
            {
                var nodeGroup = (PeptideGroupDocNode)children[i];
                IList<DocNode> newChildren = new DocNode[nodeGroup.Children.Count];
                for (int childIndex = 0; childIndex < newChildren.Count; childIndex++)
                {
                    newChildren[childIndex] = moleculeNodes[moleculeNodeIndex++];
                }
                if (rankChildren != null)
                    newChildren = rankChildren(nodeGroup, newChildren);
                newMoleculeGroups[i] = nodeGroup.ChangeChildrenChecked(newChildren);
            }
            if (ArrayUtil.ReferencesEqual(children, newMoleculeGroups))
                return children;
            return newMoleculeGroups;
        }

        /// <summary>
        /// Struct that pairs <see cref="PeptideGroupDocNode"/> with <see cref="PeptideDocNode"/> for
        /// use in a flat list that enables parallel processing.
        /// </summary>
        public struct MoleculeGroupPair
        {
            public MoleculeGroupPair(PeptideGroupDocNode nodeMoleculeGroup, PeptideDocNode nodeMolecule)
                : this()
            {
                NodeMoleculeGroup = nodeMoleculeGroup;
                NodeMolecule = nodeMolecule;
            }

            public PeptideGroupDocNode NodeMoleculeGroup { get; private set; }
            public PeptideDocNode NodeMolecule { get; private set; }

            public PeptideDocNode ReleaseMolecule()
            {
                var nodeMol = NodeMolecule;
                NodeMolecule = null;
                return nodeMol;
            }
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value, updating the <see cref="DocNode"/> hierarchy to reflect the change.
        /// </summary>
        /// <param name="settingsNew">New settings value</param>
        /// <param name="progressMonitor">Progress monitor for long settings change operations</param>
        /// <returns>A new document revision</returns>
        public SrmDocument ChangeSettings(SrmSettings settingsNew, SrmSettingsChangeMonitor progressMonitor = null)
        {
            // Preserve measured results.  Call ChangeMeasureResults to change the
            // MeasuredResults property on the SrmSettings.
            if (!ReferenceEquals(Settings.MeasuredResults, settingsNew.MeasuredResults))
                settingsNew = settingsNew.ChangeMeasuredResults(Settings.MeasuredResults);
            return ChangeSettingsInternal(settingsNew, progressMonitor);
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value, wihtout updating the <see cref="DocNode"/> hierarchy to reflect the change.
        /// </summary>
        /// <param name="settingsNew">New settings value</param>
        /// <returns>A new document revision</returns>
        public SrmDocument ChangeSettingsNoDiff(SrmSettings settingsNew)
        {
            return new SrmDocument(this, settingsNew, doc =>
            {
                doc.RevisionIndex++;
                doc.IsProteinMetadataPending = doc.CalcIsProteinMetadataPending();
            });
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value, which is itself a clone of the previous settings with a new
        /// <see cref="MeasuredResults"/> value.
        /// </summary>
        /// <param name="results">New <see cref="MeasuredResults"/> instance to associate with this document</param>
        /// <param name="progressMonitor">Progress monitor for long settings change operations</param>
        /// <returns>A new document revision</returns>
        public SrmDocument ChangeMeasuredResults(MeasuredResults results, SrmSettingsChangeMonitor progressMonitor = null)
        {
            return ChangeSettingsInternal(Settings.ChangeMeasuredResults(results), progressMonitor);
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value.
        /// </summary>
        /// <param name="settingsNew">New settings value</param>
        /// <param name="progressMonitor">Progress monitor for long settings change operations</param>
        /// <returns>A new document revision</returns>
        private SrmDocument ChangeSettingsInternal(SrmSettings settingsNew, SrmSettingsChangeMonitor progressMonitor = null)
        {
            try
            {
                return ChangeSettingsInternalOrThrow(settingsNew, progressMonitor);
            }
            catch (Exception)
            {
                if (progressMonitor != null && progressMonitor.IsCanceled())
                {
                    throw new OperationCanceledException();
                }
                throw;
            }
        }
        private SrmDocument ChangeSettingsInternalOrThrow(SrmSettings settingsNew, SrmSettingsChangeMonitor progressMonitor)
        {
            settingsNew = UpdateHasHeavyModifications(settingsNew);
            // First figure out what changed.
            SrmSettingsDiff diff = new SrmSettingsDiff(Settings, settingsNew);
            if (progressMonitor != null)
            {
                progressMonitor.GroupCount = MoleculeGroupCount;
                if (!diff.DiffPeptides)
                    progressMonitor.MoleculeCount = MoleculeCount;
                diff.Monitor = progressMonitor;
            }

            // If there were no changes that require DocNode tree updates
            if (DeferSettingsChanges || !diff.RequiresDocNodeUpdate)
                return ChangeSettingsNoDiff(settingsNew);
            else
            {
                IList<DocNode> childrenNew;
                if (diff.DiffPeptides)
                {
                    // Changes on peptides need to be done on the peptide groups, which
                    // may not achieve that great parallelism, if there is a very large
                    // peptide group, like Decoys
                    var childrenParallel = new DocNode[Children.Count];
                    var settingsParallel = settingsNew;
                    int currentPeptide = 0;
                    int totalPeptides = Children.Count;

                    // If we are looking at peptide uniqueness against a background proteome,
                    // it's faster to do those checks with a comprehensive list of peptides of 
                    // potential interest rather than taking them one by one.
                    // So we'll precalculate the peptides using any other filter settings
                    // before we go on to apply the uniqueness check.
                    var uniquenessPrecheckChildren = new List<PeptideDocNode>[Children.Count];
                    Dictionary<Target, bool> uniquenessDict = null;
                    if (settingsNew.PeptideSettings.Filter.PeptideUniqueness != PeptideFilter.PeptideUniquenessConstraint.none &&
                        !settingsNew.PeptideSettings.NeedsBackgroundProteomeUniquenessCheckProcessing)
                    {
                        // Generate the peptide docnodes with no uniqueness filter
                        var settingsNoUniquenessFilter =
                            settingsNew.ChangePeptideSettings(
                                settingsNew.PeptideSettings.ChangeFilter(
                                    settingsNew.PeptideSettings.Filter.ChangePeptideUniqueness(
                                        PeptideFilter.PeptideUniquenessConstraint.none)));
                        uniquenessPrecheckChildren = new List<PeptideDocNode>[Children.Count];
                        totalPeptides *= 2; // We have to run the list twice
                        ParallelEx.For(0, Children.Count, i =>
                        {
                            if (progressMonitor != null)
                            {
                                var percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref currentPeptide, totalPeptides);
                                if (percentComplete.HasValue && percentComplete.Value < 100)
                                    progressMonitor.ChangeProgress(status => status.ChangePercentComplete(percentComplete.Value));
                            }
                            var nodeGroup = (PeptideGroupDocNode)Children[i];
                            uniquenessPrecheckChildren[i] = nodeGroup.GetPeptideNodes(settingsNoUniquenessFilter, true).ToList();
                        });
                        var uniquenessPrecheckPeptidesOfInterest = new List<Target>(uniquenessPrecheckChildren.SelectMany(u => u.Select(p => p.Peptide.Target)));
                        // Update cache for uniqueness checks against the background proteome while we have worker threads available
                        uniquenessDict = settingsNew.PeptideSettings.Filter.CheckPeptideUniqueness(settingsNew, uniquenessPrecheckPeptidesOfInterest, progressMonitor);
                    }

                    // Now perform or complete the peptide selection
                    ParallelEx.For(0, Children.Count, i =>
                    {
                        if (progressMonitor != null)
                        {
                            if (progressMonitor.IsCanceled())
                                throw new OperationCanceledException();
                            var percentComplete = ProgressStatus.ThreadsafeIncementPercent(ref currentPeptide, totalPeptides);
                            if (percentComplete.HasValue && percentComplete.Value < 100)
                                progressMonitor.ChangeProgress(status => status.ChangePercentComplete(percentComplete.Value));
                        }
                        var nodeGroup = (PeptideGroupDocNode)Children[i];
                        childrenParallel[i] = nodeGroup.ChangeSettings(settingsParallel, diff,
                           new DocumentSettingsContext(uniquenessPrecheckChildren[i], uniquenessDict)); 
                    });
                    childrenNew = childrenParallel;
                }
                else
                {
                    // Changes that do not change the peptides can be done quicker with
                    // parallel enumeration of the peptides
                    var moleculeGroupPairs = GetMoleculeGroupPairs(Children);
                    var resultsHandler = settingsNew.PeptideSettings.Integration.ResultsHandler;
                    if (resultsHandler != null && resultsHandler.FreeImmutableMemory)
                    {
                        // Break immutability (command-line only!) and release the peptides (children of the children)
                        // so that their memory is freed after they have been processed
                        foreach (DocNodeParent child in Children)
                            child.ReleaseChildren();
                    }
                    var moleculeNodes = new PeptideDocNode[moleculeGroupPairs.Length];
                    var settingsParallel = settingsNew;
                    int currentMoleculeGroupPair = 0;
                    ParallelEx.For(0, moleculeGroupPairs.Length, i =>
                    {
                        if (progressMonitor != null)
                        {
                            if (progressMonitor.IsCanceled())
                                throw new OperationCanceledException();
                            var percentComplete =
                                ProgressStatus.ThreadsafeIncementPercent(ref currentMoleculeGroupPair,
                                    moleculeGroupPairs.Length);
                            if (percentComplete.HasValue && percentComplete.Value < 100)
                                progressMonitor.ChangeProgress(status =>
                                    status.ChangePercentComplete(percentComplete.Value));
                        }

                        var nodePep = moleculeGroupPairs[i].ReleaseMolecule();
                        moleculeNodes[i] = nodePep.ChangeSettings(settingsParallel, diff);
                    });

                    childrenNew = RegroupMolecules(Children, moleculeNodes,
                        (nodeGroup, children) => nodeGroup.RankChildren(settingsParallel, children));
                }

                // Results handler changes for re-integration last only long enough
                // to change the children
                if (settingsNew.PeptideSettings.Integration.ResultsHandler != null)
                    settingsNew = settingsNew.ChangePeptideIntegration(i => i.ChangeResultsHandler(null));

                if (settingsNew.MeasuredResults != null)
                {
                    var updatedImportTimes = settingsNew.MeasuredResults.UpdateImportTimes();
                    if (!ReferenceEquals(updatedImportTimes, settingsNew.MeasuredResults))
                    {
                        settingsNew = settingsNew.ChangeMeasuredResults(updatedImportTimes);
                    }
                }
                
                // Don't change the children, if the resulting list contains
                // only reference equal children of the same length and in the
                // same order.
                if (ArrayUtil.ReferencesEqual(childrenNew, Children))
                    return ChangeSettingsNoDiff(settingsNew);

                return (SrmDocument)new SrmDocument(this, settingsNew).ChangeChildren(childrenNew);
            }
        }

        private SrmSettings UpdateHasHeavyModifications(SrmSettings settings)
        {
            bool hasHeavyModifications = settings.PeptideSettings.Modifications.GetHeavyModifications()
                .Any(mods => mods.Modifications.Count > 0);
            if (!hasHeavyModifications && HasSmallMolecules)
            {
                foreach (var molecule in Molecules)
                {
                    if (molecule.TransitionGroups.Any(group =>
                        !ReferenceEquals(group.TransitionGroup.LabelType, IsotopeLabelType.light)))
                    {
                        hasHeavyModifications = true;
                        break;
                    }
                }
            }
            if (hasHeavyModifications == settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                return settings;
            }
            return settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(
                settings.PeptideSettings.Modifications.ChangeHasHeavyModifications(hasHeavyModifications)));
        }

        public SrmDocument ImportDocumentXml(TextReader reader,
                                             string filePath,
                                             MeasuredResults.MergeAction resultsAction,
                                             bool mergePeptides,
                                             PeptideLibraries.FindLibrary findLibrary,
                                             MappedList<string, StaticMod> staticMods,
                                             MappedList<string, StaticMod> heavyMods,
                                             IdentityPath to,
                                             out IdentityPath firstAdded,
                                             out IdentityPath nextAdd,
                                             bool pasteToPeptideList)
        {
            try
            {
                PeptideModifications.SetSerializationContext(Settings.PeptideSettings.Modifications);

                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                SrmDocument docImport = (SrmDocument) ser.Deserialize(reader);

                // Add import modifications to default modifications.
                docImport.Settings.UpdateDefaultModifications(false);
                
                var docNew = this;
                var settingsNew = docNew.Settings;
                var settingsOld = docImport.Settings;
                if (settingsOld.MeasuredResults != null)
                    settingsOld = settingsOld.ChangeMeasuredResults(settingsOld.MeasuredResults.ClearDeserialized());

                // Merge results from import document with current document.
                MeasuredResults resultsBase;
                MeasuredResults resultsNew = MeasuredResults.MergeResults(settingsNew.MeasuredResults,
                    settingsOld.MeasuredResults, filePath, resultsAction, out resultsBase);

                if (!ReferenceEquals(resultsNew, settingsNew.MeasuredResults))
                    settingsNew = settingsNew.ChangeMeasuredResults(resultsNew);
                if (!ReferenceEquals(resultsBase, settingsOld.MeasuredResults))
                    settingsOld = settingsOld.ChangeMeasuredResults(resultsBase);

                // Merge library specs from import document with current document.
                settingsNew = settingsNew.ChangePeptideLibraries(lib =>
                    lib.MergeLibrarySpecs(docImport.Settings.PeptideSettings.Libraries, findLibrary));

                if(!Equals(settingsNew, docNew.Settings))
                {
                    // Use internal settings change to preserve any changes to the measured results
                    docNew = docNew.ChangeSettingsInternal(settingsNew);                    
                }

                var settingsDiff = new SrmSettingsDiff(settingsOld, settingsNew, true);

                IList<PeptideGroupDocNode> peptideGroups = docImport.MoleculeGroups.ToList();
                if (pasteToPeptideList)
                {
                    PeptideGroupDocNode peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), null, null, new PeptideDocNode[0]);
                    IList<DocNode> peptides = docImport.Molecules.Cast<DocNode>().ToList();
                    peptideGroupDocNode = (PeptideGroupDocNode) peptideGroupDocNode.ChangeChildren(peptides);
                    peptideGroups = new List<PeptideGroupDocNode> {peptideGroupDocNode};
                }
                // Create new explicit modifications for peptides and set auto-manage children
                // when necessary for nodes pasted in from the clipboard. 
                IList<PeptideGroupDocNode> peptideGroupsNew = new List<PeptideGroupDocNode>();
                foreach (PeptideGroupDocNode nodePepGroup in peptideGroups)
                {
                    // Set explicit modifications first, since it may impact which
                    // children will be present.
                    IList<DocNode> peptidesNew = new List<DocNode>();
                    foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                    {
                        PeptideDocNode nodePepModified = nodePep.EnsureMods(
                            docImport.Settings.PeptideSettings.Modifications,
                            // ReSharper disable once PossibleNullReferenceException
                            docNew.Settings.PeptideSettings.Modifications,
                            staticMods, heavyMods);
                        if (nodePepModified.GlobalStandardType != null)
                        {
                            // Try to keep settings change from changing the children of standards being imported
                            nodePepModified = (PeptideDocNode)nodePepModified.ChangeAutoManageChildren(false)
                                .ChangeChildrenChecked(nodePepModified.TransitionGroups.Select(nodeGroup => nodeGroup.ChangeAutoManageChildren(false)).ToArray());
                        }
                        peptidesNew.Add(nodePepModified);
                    }
                    var nodePepGroupNew = (PeptideGroupDocNode)nodePepGroup.ChangeChildrenChecked(peptidesNew.ToArray());
                    nodePepGroupNew = nodePepGroupNew.EnsureChildren(docNew.Settings, pasteToPeptideList);
                    // Change settings to update everything in the peptide group to the settings of the
                    // new document, including results and peak integration
                    nodePepGroupNew = nodePepGroupNew.ChangeSettings(docNew.Settings, settingsDiff);
                    peptideGroupsNew.Add(nodePepGroupNew);
                }
                if (mergePeptides)
                    docNew = docNew.MergeMatchingPeptidesUserInfo(peptideGroupsNew);
                docNew = docNew.AddPeptideGroups(peptideGroupsNew, pasteToPeptideList, to, out firstAdded, out nextAdd);
                var modsNew = docNew.Settings.PeptideSettings.Modifications.DeclareExplicitMods(docNew,
                    staticMods, heavyMods);
                if (!ReferenceEquals(modsNew, docNew.Settings.PeptideSettings.Modifications))
                    docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => modsNew));                
                return docNew;
            }
            finally
            {
                PeptideModifications.SetSerializationContext(null);
            }
        }

        /// <summary>
        /// Inspect a file to see if it's Skyline XML data or not
        /// </summary>
        /// <param name="path">file to inspect</param>
        /// <param name="explained">explanation of problem with file if it's not a Skyline document</param>
        /// <returns>true iff file exists and has XML header that appears to be the start of Skyline document.</returns>
        public static bool IsSkylineFile(string path, out string explained)
        {
            explained = string.Empty;
            if (File.Exists(path))
            {
                try
                {
                    // We have no idea what kind of file this might be, so even reading the first "line" might take a long time. Read a chunk instead.
                    using (var probeFile = File.OpenRead(path))
                    {
                        var CHUNKSIZE = 500; // Should be more than adequate to check for "?xml version="1.0" encoding="utf-8"?>< srm_settings format_version = "4.12" software_version = "Skyline (64-bit) " >"
                        var probeBuf = new byte[CHUNKSIZE];
                        probeFile.Read(probeBuf, 0, CHUNKSIZE);
                        probeBuf[CHUNKSIZE - 1] = 0;
                        var probeString = Encoding.UTF8.GetString(probeBuf);
                        if (!probeString.Contains(@"<srm_settings"))
                        {
                            explained = string.Format(
                                Resources.SkylineWindow_OpenFile_The_file_you_are_trying_to_open____0____does_not_appear_to_be_a_Skyline_document__Skyline_documents_normally_have_a___1___or___2___filename_extension_and_are_in_XML_format_,
                                path, EXT, SrmDocumentSharing.EXT_SKY_ZIP);
                        }
                    }
                }
                catch (Exception e)
                {
                    explained = e.Message;
                }
            }
            else
            {
                explained = Resources.ToolDescription_RunTool_File_not_found_; // "File not found"
            }

            return string.IsNullOrEmpty(explained);
        }

        /// <summary>
        /// Tries to find a .sky file for a .skyd or .skyl etc file
        /// </summary>
        /// <param name="path">Path to file which may have a sibling .sky file</param>
        /// <returns>Input path with extension changed to .sky, if such a file exists and appears to be a Skyline file</returns>
        public static string FindSiblingSkylineFile(string path)
        {
            var index = path.LastIndexOf(EXT, StringComparison.Ordinal);
            if (index > 0 && index == path.Length - (EXT.Length + 1))
            {
                // Looks like user picked a .skyd or .skyl etc
                var likelyPath = path.Substring(0, index + EXT.Length);
                if (File.Exists(likelyPath) && IsSkylineFile(likelyPath, out _))
                {
                    return likelyPath;
                }
            }

            return path;
        }


        private SrmDocument MergeMatchingPeptidesUserInfo(IList<PeptideGroupDocNode> peptideGroupsNew)
        {
            var setMerge = new HashSet<PeptideModKey>();
            var dictPeptidesModified = new Dictionary<PeptideModKey, PeptideDocNode>();
            foreach(var nodePep in peptideGroupsNew.SelectMany(nodePepGroup => nodePepGroup.Children)
                                                   .Cast<PeptideDocNode>())
            {
                var key = nodePep.Key;
                setMerge.Add(key);
                if (!nodePep.IsUserModified)
                    continue;
                if (dictPeptidesModified.ContainsKey(key))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.SrmDocument_MergeMatchingPeptidesUserInfo_The_peptide__0__was_found_multiple_times_with_user_modifications,
                                      nodePep.RawTextIdDisplay));
                }
                dictPeptidesModified.Add(key, nodePep);
            }

            var diff = new SrmSettingsDiff(Settings, true);
            var setMerged = new HashSet<PeptideModKey>();
            var listPeptideGroupsMerged = new List<DocNode>();
            foreach (var nodePepGroup in MoleculeGroups)
            {
                var listPeptidesMerged = new List<DocNode>();
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    // If the peptide has no match in the set to be merged, then just add it.
                    if (!setMerge.Contains(nodePep.Key))
                        listPeptidesMerged.Add(nodePep);
                    else
                    {
                        // Keep track of the matching peptides
                        setMerged.Add(nodePep.Key);

                        PeptideDocNode nodePepMatch;
                        // If it is not modified, it doesn't really need to be merged.
                        if (!dictPeptidesModified.TryGetValue(nodePep.Key, out nodePepMatch))
                            listPeptidesMerged.Add(nodePep);
                        else
                            listPeptidesMerged.Add(nodePep.MergeUserInfo(nodePepMatch, Settings, diff));
                    }
                }
                listPeptideGroupsMerged.Add(nodePepGroup.ChangeChildrenChecked(listPeptidesMerged));
            }
            // Update the list of peptide groups to add based on what got merged
            foreach (var nodePepGroup in peptideGroupsNew.ToArray())
            {
                var listPeptidesUnmerged = new List<DocNode>();
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    if (!setMerged.Contains(nodePep.Key))
                        listPeptidesUnmerged.Add(nodePep);
                }
                if (listPeptidesUnmerged.Count == 0)
                    peptideGroupsNew.Remove(nodePepGroup);
                else
                {
                    peptideGroupsNew[peptideGroupsNew.IndexOfReference(nodePepGroup)] =
                        (PeptideGroupDocNode) nodePepGroup.ChangeChildrenChecked(listPeptidesUnmerged);
                }
            }
            return (SrmDocument) ChangeChildrenChecked(listPeptideGroupsMerged);
        }

        public SrmDocument ImportFasta(TextReader reader, bool peptideList,
                IdentityPath to, out IdentityPath firstAdded)
        {
            int emptiesIgnored;
            return ImportFasta(reader, null, -1, peptideList, to, out firstAdded, out emptiesIgnored);
        }

        public SrmDocument ImportFasta(TextReader reader, IProgressMonitor progressMonitor, long lines, bool peptideList,
                IdentityPath to, out IdentityPath firstAdded, out int emptyPeptideGroups)
        {
            FastaImporter importer = new FastaImporter(this, peptideList);
            IdentityPath nextAdd;
            IEnumerable<PeptideGroupDocNode> imported = importer.Import(reader, progressMonitor, lines);
            emptyPeptideGroups = importer.EmptyPeptideGroupCount;
            return AddPeptideGroups(imported, peptideList, to, out firstAdded, out nextAdd);
        }

        public SrmDocument ImportFasta(TextReader reader, IProgressMonitor progressMonitor, long lines, 
            ModificationMatcher matcher, IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdded, out int emptiesIgnored)
        {
            if (matcher == null)
            {
                nextAdded = null;
                return ImportFasta(reader, progressMonitor, lines, false, to, out firstAdded, out emptiesIgnored);
            }

            FastaImporter importer = new FastaImporter(this, matcher);
            IEnumerable<PeptideGroupDocNode> imported = importer.Import(reader, progressMonitor, lines);
            emptiesIgnored = importer.EmptyPeptideGroupCount;
            return AddPeptideGroups(imported, true, to, out firstAdded, out nextAdded);
        }

        public SrmDocument ImportMassList(MassListInputs inputs,
            MassListImporter importer, 
            IdentityPath to, 
            out IdentityPath firstAdded,
            List<string> columnPositions = null,
            bool hasHeaders = true)
        {
            List<MeasuredRetentionTime> irtPeptides;
            List<SpectrumMzInfo> librarySpectra;
            List<TransitionImportErrorInfo> errorList;
            List<PeptideGroupDocNode> peptideGroups;
            return ImportMassList(inputs, importer, null, to, out firstAdded, out irtPeptides, out librarySpectra, out errorList, out peptideGroups, columnPositions, DOCUMENT_TYPE.none, hasHeaders);
        }

        public SrmDocument ImportMassList(MassListInputs inputs,
                                          IdentityPath to,
                                          out IdentityPath firstAdded,
                                          out List<MeasuredRetentionTime> irtPeptides,
                                          out List<SpectrumMzInfo> librarySpectra,
                                          out List<TransitionImportErrorInfo> errorList,
                                          List<string> columnPositions = null)
        {
            List<PeptideGroupDocNode> peptideGroups;
            return ImportMassList(inputs, null, null, to, out firstAdded, out irtPeptides, out librarySpectra, out errorList, out peptideGroups, columnPositions);
        }

        public SrmDocument ImportMassList(MassListInputs inputs, 
                                          MassListImporter importer,
                                          IProgressMonitor progressMonitor,
                                          IdentityPath to,
                                          out IdentityPath firstAdded,
                                          out List<MeasuredRetentionTime> irtPeptides,
                                          out List<SpectrumMzInfo> librarySpectra,
                                          out List<TransitionImportErrorInfo> errorList,
                                          out List<PeptideGroupDocNode> peptideGroups,
                                          List<string> columnPositions = null,
                                          DOCUMENT_TYPE radioType = DOCUMENT_TYPE.none,
                                          bool hasHeaders = true, Dictionary<string, FastaSequence> dictNameSeq = null)
        {
            irtPeptides = new List<MeasuredRetentionTime>();
            librarySpectra = new List<SpectrumMzInfo>();
            peptideGroups = new List<PeptideGroupDocNode>();
            errorList = new List<TransitionImportErrorInfo>();

            var docNew = this;
            firstAdded = null;

            // Is this a small molecule transition list, or trying to be?
            if (((importer != null && importer.InputType == DOCUMENT_TYPE.small_molecules) && radioType == DOCUMENT_TYPE.none) || radioType == DOCUMENT_TYPE.small_molecules)
            {
                IList<string> lines = null;
                try
                {
                    lines = inputs.ReadLines(progressMonitor);
                    var reader = new SmallMoleculeTransitionListCSVReader(lines, columnPositions, hasHeaders);
                    docNew = reader.CreateTargets(this, to, out firstAdded);
                    foreach (var error in reader.ErrorList)
                    {
                        var lineIndex  = error.Line + (hasHeaders ? 1 : 0); // Account for parser not including header in its line count
                        var line =
                            ((lineIndex >= 0 && lineIndex < lines.Count) ? lines[lineIndex] : null)?.Replace(
                                TextUtil.SEPARATOR_TSV_STR, @" ");
                        errorList.Add(new TransitionImportErrorInfo(error.Message, error.Column, lineIndex + 1, line)); // Show line number as 1 based
                    }
                }
                catch (LineColNumberedIoException x)
                {
                    var line = (lines != null && x.LineNumber >=0 && x.LineNumber < lines.Count ? lines[(int)x.LineNumber] : null)?.
                        Replace(TextUtil.SEPARATOR_TSV_STR, @" ");
                    errorList.Add(new TransitionImportErrorInfo(x.PlainMessage, x.ColumnIndex, x.LineNumber + 1, line));  // CONSIDER: worth the effort to pull row and column info from error message?
                }
            }
            else
            {
                try
                {
                    if (importer == null)
                        importer = PreImportMassList(inputs, progressMonitor, false);
                    if (importer != null)
                    {
                        IdentityPath nextAdd;
                        //peptideGroups = importer.Import(progressMonitor, out irtPeptides, out librarySpectra, out errorList).ToList();
                        if (dictNameSeq == null)
                        {
                            dictNameSeq = new Dictionary<string, FastaSequence>();
                        }

                        var imported = importer.DoImport(progressMonitor, dictNameSeq, irtPeptides, librarySpectra, errorList);
                        if (progressMonitor != null && progressMonitor.IsCanceled)
                        {
                            return this;
                        }
                        peptideGroups = (List<PeptideGroupDocNode>) imported;
                        docNew = AddPeptideGroups(peptideGroups, false, to, out firstAdded, out nextAdd);
                        var pepModsNew = importer.GetModifications(docNew);
                        if (!ReferenceEquals(pepModsNew, Settings.PeptideSettings.Modifications))
                        {
                            docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                            docNew.Settings.UpdateDefaultModifications(false);
                        }
                    }
                }
                catch (LineColNumberedIoException x)
                {
                    throw new InvalidDataException(x.Message, x);
                }
            }
            return docNew;
        }

        /// <summary>
        /// Return a mass list import if the progress monitor is not cancelled and we are able to read the document
        /// </summary>
        /// <param name="inputs">Input to be imported</param>
        /// <param name="progressMonitor">Cancellable progress monitor</param>
        /// <param name="tolerateErrors">Should we tolerate errors when creating a row reader</param>
        /// <param name="inputType">"None" means "don't know if it's peptides or small molecules, go figure it out".</param>
        /// <param name="rowReadRequired">Is it necessary to create a row reader to import this mass list</param>
        /// <param name="defaultDocumentType">The type we should default to if we cannot tell if the transition list is proteomics or small molecule</param>
        /// <returns></returns>
        public MassListImporter PreImportMassList(MassListInputs inputs, IProgressMonitor progressMonitor, bool tolerateErrors, 
            DOCUMENT_TYPE inputType = DOCUMENT_TYPE.none, // "None" means "don't know if it's peptides or small molecules, go figure it out".
            bool rowReadRequired = false, DOCUMENT_TYPE defaultDocumentType = DOCUMENT_TYPE.none) 
        {
            var importer = new MassListImporter(this, inputs,  inputType);
            if (importer.PreImport(progressMonitor, null, tolerateErrors, rowReadRequired, defaultDocumentType))
            {
                return importer;
            }
            return null;
        }

        public SrmDocument AddIrtPeptides(List<DbIrtPeptide> irtPeptides, bool overwriteExisting, IProgressMonitor progressMonitor)
        {
            var retentionTimeRegression = Settings.PeptideSettings.Prediction.RetentionTime;
            if (retentionTimeRegression == null || !(retentionTimeRegression.Calculator is RCalcIrt))
            {
                throw new InvalidDataException(Resources.SrmDocument_AddIrtPeptides_Must_have_an_active_iRT_calculator_to_add_iRT_peptides);
            }
            var calculator = (RCalcIrt) retentionTimeRegression.Calculator;
            string dbPath = calculator.DatabasePath;
            IrtDb db = File.Exists(dbPath) ? IrtDb.GetIrtDb(dbPath, null) : IrtDb.CreateIrtDb(dbPath);
            var oldPeptides = db.GetPeptides().Select(p => new DbIrtPeptide(p)).ToList();
            IList<DbIrtPeptide.Conflict> conflicts;
            var peptidesCombined = DbIrtPeptide.FindNonConflicts(oldPeptides, irtPeptides, progressMonitor, out conflicts);
            if (peptidesCombined == null)
                return null;
            foreach (var conflict in conflicts)
            {
                // If old and new peptides are a library entry and a standards entry, throw an error
                // The same peptide must not appear in both places
                if (conflict.NewPeptide.Standard ^ conflict.ExistingPeptide.Standard)
                {
                    throw new InvalidDataException(string.Format(Resources.SkylineWindow_AddIrtPeptides_Imported_peptide__0__with_iRT_library_value_is_already_being_used_as_an_iRT_standard_,
                                                    conflict.NewPeptide.ModifiedTarget));
                }
            }
            // Peptides that were already present in the database can be either kept or overwritten 
            peptidesCombined.AddRange(conflicts.Select(conflict => overwriteExisting ? conflict.NewPeptide  : conflict.ExistingPeptide));
            db = db.UpdatePeptides(peptidesCombined, oldPeptides);
            calculator = calculator.ChangeDatabase(db);
            retentionTimeRegression = retentionTimeRegression.ChangeCalculator(calculator);
            var srmSettings = Settings.ChangePeptidePrediction(pred => pred.ChangeRetentionTime(retentionTimeRegression));
            if (ReferenceEquals(srmSettings, Settings))
                return this;
            return ChangeSettings(srmSettings);
        }

        public static bool IsConvertedFromProteomicTestDocNode(DocNode node)
        {
            // Is this a node that was created for test purposes by transforming an existing peptide doc?
            return (node != null && node.Annotations.Note != null &&
                    node.Annotations.Note.Contains(RefinementSettings.TestingConvertedFromProteomic));
        }
        
        public SrmDocument AddPeptideGroups(IEnumerable<PeptideGroupDocNode> peptideGroupsNew,
            bool peptideList, IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdd)
        {
            // For multiple add operations, make the next addtion at the same location by default
            nextAdd = to;
            var peptideGroupsAdd = peptideGroupsNew.ToList();

            // If there are no new groups to add, as in the case where already added
            // FASTA sequences are pasted, just return this, and a null path.  Callers
            // must handle this case gracefully, e.g. not adding an undo record.
            if (peptideGroupsAdd.Count == 0)
            {
                firstAdded = null;
                return this;
            }
            firstAdded = new IdentityPath(peptideGroupsAdd[0].Id);

            // Add to the end, if no insert node
            if (to == null || to.Depth < (int)Level.MoleculeGroups)
                return (SrmDocument) AddAll(peptideGroupsAdd);
            
            IdentityPath pathGroup = to.GetPathTo((int)Level.MoleculeGroups);

            // Precalc depth of last identity in the path
            int last = to.Length - 1;

            // If it is a peptide list, allow pasting to children to existing peptide list.
            if (peptideList && !(to.GetIdentity((int)Level.MoleculeGroups) is FastaSequence))
            {
                // PeptideGroupDocNode nodeGroup = (PeptideGroupDocNode) FindNode(pathGroup);

                // Add only peptides not already in this group
                // With explicit modifications, there is now reason to add duplicates,
                // when multiple modified forms are desired.
                HashSet<Peptide> setPeptides = new HashSet<Peptide>();
                // foreach (PeptideDocNode nodePeptide in nodeGroup.Children)
                //    setPeptides.Add(nodePeptide.Peptide);
                List<DocNode> listAdd = new List<DocNode>();
                foreach (PeptideDocNode nodePeptide in peptideGroupsAdd[0].Children)
                {
                    if (!setPeptides.Contains(nodePeptide.Peptide))
                    {
                        listAdd.Add(nodePeptide);
                        setPeptides.Add(nodePeptide.Peptide);
                    }
                }

                // No modification necessary, if no unique peptides
                if (listAdd.Count == 0)
                {
                    firstAdded = null;
                    return this;
                }

                // If no peptide was in the selection path, add to the end of the list
                DocNode docNew;
                if (last < (int)Level.Molecules)
                    docNew = AddAll(to, listAdd);
                    // If one of the peptides was selected, insert before it
                else if (last == (int)Level.Molecules)
                    docNew = InsertAll(to, listAdd);
                    // Otherise, insert after the peptide of the child that was selected
                else
                {
                    nextAdd = FindNextInsertNode(to, (int) Level.Molecules);
                    docNew = InsertAll(to.GetPathTo((int)Level.Molecules), listAdd, true);
                }

                // Change the selection path to point to the first peptide pasted.
                firstAdded = new IdentityPath(pathGroup, listAdd[0].Id);
                return (SrmDocument)docNew;
            }
                // Insert the new groups before a selected group
            else if (last == (int)Level.MoleculeGroups)
                return (SrmDocument)InsertAll(pathGroup, peptideGroupsAdd);
                // Or after, if a group child is selected
            else
            {
                nextAdd = FindNextInsertNode(to, (int)Level.MoleculeGroups);
                return (SrmDocument)InsertAll(pathGroup, peptideGroupsAdd, true);
            }
        }

        private IdentityPath FindNextInsertNode(IdentityPath identityPath, int depth)
        {
            if (identityPath == null)
                return null;

            // Get the path to the desired level
            while (identityPath.Depth > depth)
                identityPath = identityPath.Parent;

            // Get the index to the node at that level and add 1
            int iNode = FindNodeIndex(identityPath) + 1;
            // If the next node exists, get the path to it
            IdentityPath identityPathNext = null;
            if (iNode < GetCount(depth))
                identityPathNext = GetPathTo(depth, iNode);
            // If no next node was available, or the next node belongs to a new parent
            // return the parent, or null if at the root.
            if (identityPathNext == null || !Equals(identityPath.Parent, identityPathNext.Parent))
                return (depth != 0 ? identityPath.Parent : null);
            // Return the path to the next node.
            return identityPathNext;
        }

        public bool IsValidMove(IdentityPath from, IdentityPath to)
        {
            int lastFrom = from.Length - 1;
            // Peptide groups can always be moved
            if (lastFrom == (int)Level.MoleculeGroups)
                return true;
            // Peptides can be moved, if going from a peptide list to a peptide list
            else if (to != null && lastFrom == (int)Level.Molecules &&
                    !(from.GetIdentity((int)Level.MoleculeGroups) is FastaSequence) &&
                    !(to.GetIdentity((int)Level.MoleculeGroups) is FastaSequence))
                return true;
            return false;
        }

        public SrmDocument MoveNode(IdentityPath from, IdentityPath to, out IdentityPath newLocation)
        {
            DocNode nodeFrom = FindNode(from);
            if (nodeFrom == null)
                throw new IdentityNotFoundException(from.Child);

            int lastFrom = from.Length - 1;
            int lastTo = (to == null ? -1 : to.Length - 1);

            // Figure out where actually to put the moving node.
            if (lastFrom == (int)Level.MoleculeGroups)
            {
                SrmDocument document = (SrmDocument)RemoveChild(nodeFrom);
                // If no good target, append
                if (to == null || lastTo == -1)
                    document = (SrmDocument)document.Add(nodeFrom);
                // If dropped over a group, insert before
                else if (lastTo == (int)Level.MoleculeGroups)
                    document = (SrmDocument)document.Insert(to, nodeFrom);
                // If over the child of a group, insert after
                else
                    document = (SrmDocument)document.Insert(to.GetPathTo((int)Level.MoleculeGroups), nodeFrom, true);
                newLocation = new IdentityPath(nodeFrom.Id);
                return document;
            }
            // If moving a peptide that comes from a peptide list
            else if (lastFrom == (int)Level.Molecules)
            {
                if (from.GetIdentity((int)Level.MoleculeGroups) is FastaSequence)
                    throw new InvalidOperationException(Resources.SrmDocument_MoveNode_Invalid_move_source);
                if (to == null || to.GetIdentity((int)Level.MoleculeGroups) is FastaSequence)
                    throw new InvalidOperationException(Resources.SrmDocument_MoveNode_Invalid_move_target);

                SrmDocument document = (SrmDocument)RemoveChild(from.Parent, nodeFrom);
                // If dropped over a group, add to the end
                if (lastTo == (int)Level.MoleculeGroups)
                    document = (SrmDocument) document.Add(to, nodeFrom);
                // If over a peptide, insert before
                else if (lastTo == (int)Level.Molecules)
                    document = (SrmDocument) document.Insert(to, nodeFrom);
                // If over the child of a peptide, insert after
                else
                    document = (SrmDocument) document.Insert(to.GetPathTo((int)Level.Molecules), nodeFrom, true);
                newLocation = new IdentityPath(to.GetPathTo((int)Level.MoleculeGroups), nodeFrom.Id);
                return document;
            }
            throw new InvalidOperationException(Resources.SrmDocument_MoveNode_Invalid_move_source);
        }

        public SrmDocument AddPrecursorResultsAnnotations(IdentityPath groupPath, ChromFileInfoId fileId,
                                                          Dictionary<string, string> annotations)
        {
            var groupNode = (TransitionGroupDocNode) FindNode(groupPath);
            var groupNodeNew = groupNode.AddPrecursorAnnotations(fileId, annotations);
            if (ReferenceEquals(groupNode, groupNodeNew))
                return this;
            return (SrmDocument) ReplaceChild(groupPath.Parent, groupNodeNew);
        }

        public SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, MsDataFileUri filePath,
            Identity tranId, double retentionTime, UserSet userSet)
        {
            return ChangePeak(groupPath, nameSet, filePath, false,
                (node, info, tol, iSet, fileId, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, fileId, reg, tranId, retentionTime, userSet));
        }

        public SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, MsDataFileUri filePath,
            Transition transition, double? startTime, double? endTime, UserSet userSet, PeakIdentification? identified, bool preserveMissingPeaks)
        {
            // If start or end time is null, just assign an arbitrary value to identified -- peak will be deleted anyway
            if (!startTime.HasValue || !endTime.HasValue)
                identified = PeakIdentification.FALSE;
            // If a null identification is passed in (currently only happens from the PeakBoundaryImport function),
            // look up the identification status directly
            if (!identified.HasValue)
            {
                IdentityPath peptidePath = groupPath.Parent;
                var nodePep = (PeptideDocNode) FindNode(peptidePath);
                var nodeGroup = (TransitionGroupDocNode) FindNode(groupPath);
                if (nodeGroup == null)
                    throw new IdentityNotFoundException(groupPath.Child);
                var lookupSequence = nodePep.SourceUnmodifiedTarget;
                var lookupMods = nodePep.SourceExplicitMods;
                IsotopeLabelType labelType;
                double[] retentionTimes;
                Settings.TryGetRetentionTimes(lookupSequence, nodeGroup.TransitionGroup.PrecursorAdduct, lookupMods,
                                              filePath, out labelType, out retentionTimes);
                if(ContainsTime(retentionTimes, startTime.Value, endTime.Value))
                {
                    identified = PeakIdentification.TRUE;
                }
                else
                {
                    var alignedRetentionTimes = Settings.GetAlignedRetentionTimes(filePath,
                        lookupSequence, lookupMods);
                    identified = ContainsTime(alignedRetentionTimes, startTime.Value, endTime.Value)
                        ? PeakIdentification.ALIGNED
                        : PeakIdentification.FALSE;
                }
            }
            return ChangePeak(groupPath, nameSet, filePath, true,
                (node, info, tol, iSet, fileId, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, fileId, reg, transition, startTime, 
                                    endTime, identified.Value, userSet, preserveMissingPeaks));
        }

        private bool ContainsTime(double[] times, double startTime, double endTime)
        {
            return times != null && times.Any(time => startTime <= time && time <= endTime);
        }

        private delegate DocNode ChangeNodePeak(TransitionGroupDocNode nodeGroup,
            ChromatogramGroupInfo chromInfoGroup, double mzMatchTolerance, int indexSet,
            ChromFileInfoId indexFile, OptimizableRegression regression);

        private SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, MsDataFileUri filePath, bool loadPoints,
            ChangeNodePeak change)
        {
            var groupId = groupPath.Child;
            var nodePep = (PeptideDocNode) FindNode(groupPath.Parent);
            if (nodePep == null)
                throw new IdentityNotFoundException(groupId);
            var nodeGroup = (TransitionGroupDocNode)nodePep.FindNode(groupId);
            if (nodeGroup == null)
                throw new IdentityNotFoundException(groupId);
            // Get the chromatogram set containing the chromatograms of interest
            int indexSet;
            ChromatogramSet chromatograms;
            if (!Settings.HasResults || !Settings.MeasuredResults.TryGetChromatogramSet(nameSet, out chromatograms, out indexSet))
                throw new ArgumentOutOfRangeException(string.Format(Resources.SrmDocument_ChangePeak_No_replicate_named__0__was_found, nameSet));
            // Calculate the file index that supplied the chromatograms
            ChromFileInfoId fileId = chromatograms.FindFile(filePath);
            if (fileId == null)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(Resources.SrmDocument_ChangePeak_The_file__0__was_not_found_in_the_replicate__1__,
                                  filePath, nameSet));
            }
            // Get all chromatograms for this transition group
            double mzMatchTolerance = Settings.TransitionSettings.Instrument.MzMatchTolerance;
            ChromatogramGroupInfo[] arrayChromInfo;
            if (!Settings.MeasuredResults.TryLoadChromatogram(chromatograms, nodePep, nodeGroup,
                (float) mzMatchTolerance, out arrayChromInfo))
            {
                throw new ArgumentOutOfRangeException(string.Format(
                    Resources.SrmDocument_ChangePeak_No_results_found_for_the_precursor__0__in_the_replicate__1__,
                    TransitionGroupTreeNode.GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz, string.Empty),
                    nameSet));
            }
            // Get the chromatograms for only the file of interest
            int indexInfo = arrayChromInfo.IndexOf(info => Equals(filePath, info.FilePath));
            if (indexInfo == -1)
            {
                throw new ArgumentOutOfRangeException(string.Format(Resources.SrmDocument_ChangePeak_No_results_found_for_the_precursor__0__in_the_file__1__,
                                                                    TransitionGroupTreeNode.GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz, string.Empty), filePath));
            }
            var chromInfoGroup = arrayChromInfo[indexInfo];
            var nodeGroupNew = change(nodeGroup, chromInfoGroup, mzMatchTolerance, indexSet, fileId,
                chromatograms.OptimizationFunction);
            if (ReferenceEquals(nodeGroup, nodeGroupNew))
                return this;
            return (SrmDocument)ReplaceChild(groupPath.Parent, nodeGroupNew);
        }

        public SrmDocument ChangePeptideMods(IdentityPath peptidePath, ExplicitMods mods,
            IList<StaticMod> listGlobalStaticMods, IList<StaticMod> listGlobalHeavyMods)
        {
            return ChangePeptideMods(peptidePath, mods, false, listGlobalStaticMods, listGlobalHeavyMods);
        }

        public SrmDocument ChangePeptideMods(IdentityPath peptidePath, ExplicitMods mods, bool createCopy,
            IList<StaticMod> listGlobalStaticMods, IList<StaticMod> listGlobalHeavyMods)
        {
            var docResult = this;
            var pepMods = docResult.Settings.PeptideSettings.Modifications;

            var nodePeptide = (PeptideDocNode)FindNode(peptidePath);
            if (nodePeptide == null)
                throw new IdentityNotFoundException(peptidePath.Child);
            // Make sure modifications are in synch with global values
            if (mods != null)
            {
                mods = mods.ChangeGlobalMods(listGlobalStaticMods, listGlobalHeavyMods,
                    pepMods.GetHeavyModificationTypes().ToArray());
            }
            // If modifications have changed, update the peptide.
            var modsPep = nodePeptide.ExplicitMods;
            if (createCopy || !Equals(mods, modsPep))
            {
                // Update the peptide to the new explicit modifications
                // Change the explicit modifications, and force a settings update through the peptide
                // to all of its children.
                // CONSIDER: This is not really the right SrmSettings object to be using for this
                //           update, but constructing the right one currently depends on the
                //           peptide being added to the document.  Doesn't seem like the potential
                //           changes would have any impact on this operation, though.
                if (createCopy)
                {
                    nodePeptide = new PeptideDocNode((Peptide)nodePeptide.Peptide.Copy(),
                                                        Settings,
                                                        nodePeptide.ExplicitMods,
                                                        nodePeptide.SourceKey,
                                                        nodePeptide.GlobalStandardType,
                                                        nodePeptide.Rank,
                                                        nodePeptide.ExplicitRetentionTime,
                                                        Annotations.EMPTY,
                                                        null,   // Results
                                                        nodePeptide.Children.ToList().ConvertAll(node => (TransitionGroupDocNode)node).ToArray(),
                                                        nodePeptide.AutoManageChildren);
                    nodePeptide = nodePeptide.ChangeExplicitMods(mods).ChangeSettings(Settings, SrmSettingsDiff.ALL);
                    docResult = (SrmDocument)docResult.Insert(peptidePath, nodePeptide, true);
                }
                else
                {
                    nodePeptide = nodePeptide.ChangeExplicitMods(mods).ChangeSettings(Settings, SrmSettingsDiff.ALL);
                    docResult = (SrmDocument)docResult.ReplaceChild(peptidePath.Parent, nodePeptide);
                }

                // Turn off auto-manage children for the peptide group if it is a FASTA sequence,
                // because the child lists the FASTA sequence will create will not contain this manually
                // altered peptide.
                var nodePepGroup = (PeptideGroupDocNode)docResult.FindNode(peptidePath.Parent);
                if (!nodePepGroup.IsPeptideList)
                {
                    // Make sure peptides are ranked correctly
                    var childrenNew = PeptideGroup.RankPeptides(nodePepGroup.Children, docResult.Settings, false);
                    docResult = (SrmDocument)docResult.ReplaceChild(nodePepGroup
                        .ChangeAutoManageChildren(false)
                        .ChangeChildrenChecked(childrenNew));
                }
            }

            var pepModsNew = pepMods.DeclareExplicitMods(docResult, listGlobalStaticMods, listGlobalHeavyMods);
            if (Equals(pepModsNew, pepMods))
                return docResult;

            // Make sure any newly included modifications are added to the settings
            var settings = docResult.Settings.ChangePeptideModifications(m => pepModsNew);
            return docResult.ChangeSettings(settings);
        }

        public IdentityPath SearchDocumentForString(IdentityPath identityPath, string text, DisplaySettings settings, bool reverse, bool caseSensitive)
        {
            var findOptions = new FindOptions()
                .ChangeText(text)
                .ChangeForward(!reverse)
                .ChangeCaseSensitive(caseSensitive);
            var findResult = SearchDocument(new Bookmark(identityPath), findOptions, settings);
            if (findResult == null)
            {
                return null;
            }
            return findResult.Bookmark.IdentityPath;
        }

        public FindResult SearchDocument(Bookmark startPath, FindOptions findOptions, DisplaySettings settings)
        {
            var bookmarkEnumerator = new BookmarkEnumerator(this, startPath) {Forward = findOptions.Forward};
            return FindNext(bookmarkEnumerator, findOptions, settings);
        }

        private static FindResult FindNext(BookmarkEnumerator bookmarkEnumerator, FindOptions findOptions, DisplaySettings settings)
        {
            var findPredicate = new FindPredicate(findOptions, settings);
            return findPredicate.FindNext(bookmarkEnumerator);
        }

        public SrmDocument ChangeStandardType(StandardType standardType, IEnumerable<IdentityPath> selPaths)
        {
            SrmDocument doc = this;
            var replacements = new List<NodeReplacement>();
            foreach (IdentityPath nodePath in selPaths)
            {
                var nodePep = doc.FindNode(nodePath) as PeptideDocNode;
                if (nodePep == null || nodePep.IsDecoy || Equals(standardType, nodePep.GlobalStandardType))
                    continue;
                replacements.Add(new NodeReplacement(nodePath.Parent, nodePep.ChangeStandardType(standardType)));
            }
            doc = (SrmDocument) doc.ReplaceChildren(replacements);
            return doc;
        }

        public IEnumerable<PeptideDocNode> GetSurrogateStandards()
        {
            return Molecules.Where(mol => Equals(mol.GlobalStandardType, StandardType.SURROGATE_STANDARD));
        }

        public SrmDocument BeginDeferSettingsChanges()
        {
            return ChangeProp(ImClone(this), im => im.DeferSettingsChanges = true);
        }

        public SrmDocument EndDeferSettingsChanges(SrmDocument originalDocument, SrmSettingsChangeMonitor progressMonitor)
        {
            var docWithOriginalSettings = (SrmDocument) ChangeProp(ImClone(this), im =>
            {
                im.Settings = originalDocument.Settings;
                im.DeferSettingsChanges = false;
            }).ChangeChildren(originalDocument.Children);
            var doc = docWithOriginalSettings
                .ChangeSettings(Settings, progressMonitor)
                .ChangeMeasuredResults(Settings.MeasuredResults, progressMonitor);
            doc = (SrmDocument) doc.ChangeChildren(Children.ToArray());
            return doc;
        }
        public IEnumerable<ChromatogramSet> GetSynchronizeIntegrationChromatogramSets()
        {
            if (!Settings.HasResults)
                yield break;

            if (Settings.TransitionSettings.Integration.SynchronizedIntegrationAll)
            {
                // Synchronize all
                foreach (var chromSet in MeasuredResults.Chromatograms)
                    yield return chromSet;
                yield break;
            }

            var targets = Settings.TransitionSettings.Integration.SynchronizedIntegrationTargets?.ToHashSet();
            if (targets == null || targets.Count == 0)
            {
                // Synchronize none
                yield break;
            }

            var groupBy = Settings.TransitionSettings.Integration.SynchronizedIntegrationGroupBy;
            if (string.IsNullOrEmpty(groupBy))
            {
                // Synchronize individual replicates
                foreach (var chromSet in MeasuredResults.Chromatograms.Where(chromSet => targets.Contains(chromSet.Name)))
                    yield return chromSet;
                yield break;
            }

            // Synchronize by annotation
            var replicateValue = ReplicateValue.FromPersistedString(Settings, groupBy);
            var annotationCalculator = new AnnotationCalculator(this);
            foreach (var chromSet in MeasuredResults.Chromatograms)
            {
                var value = replicateValue.GetValue(annotationCalculator, chromSet);
                if (targets.Contains(Convert.ToString(value ?? string.Empty, CultureInfo.InvariantCulture)))
                    yield return chromSet;
            }
        }

        private object _referenceId = new object();
        /// <summary>
        /// Value which is unique to this instance of the SrmDocument.
        /// This enables you to determine whether another SrmDocument is ReferenceEquals to this, without
        /// having to hold onto a reference to this.
        /// <see cref="pwiz.Skyline.Model.Databinding.CachedValue{T}"/>
        /// </summary>
        public object ReferenceId { get { return _referenceId; } }
        protected override object ImmutableClone()
        {
            SrmDocument document = (SrmDocument) base.ImmutableClone();
            document._referenceId = new object();
            return document;
        }

        #region Implementation of IXmlSerializable
        /// <summary>
        /// For deserialization
        /// </summary>
// ReSharper disable UnusedMember.Local
        private SrmDocument()
// ReSharper restore UnusedMember.Local
            : base(new SrmDocumentId())
        {            
        }

        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            if (Settings != null)
            {
                throw new InvalidOperationException();
            }
            var documentReader = new DocumentReader();
            documentReader.ReadXml(reader);
            FormatVersion = documentReader.FormatVersion;
            Settings = documentReader.Settings;

            if (documentReader.Children == null)
                SetChildren(new PeptideGroupDocNode[0]);
            else
            {
                var children = documentReader.Children;

                // Make sure peptide standards lists are up to date
                Settings = Settings.CachePeptideStandards(new PeptideGroupDocNode[0], children);

                SetChildren(UpdateResultsSummaries(children, new Dictionary<int, PeptideDocNode>()));

                IsProteinMetadataPending = CalcIsProteinMetadataPending(); // Background loaders are about to kick in, they need this info.
            }

            SetDocumentType(); // Note proteomic vs small_molecules vs mixed

            AuditLog = AuditLog ?? new AuditLogList();
        }

        public SrmDocument ReadAuditLog(string documentPath, string expectedSkylineDocumentHash, Func<AuditLogEntry> getDefaultEntry)
        {
            var auditLog = new AuditLogList();
            var auditLogPath = GetAuditLogPath(documentPath);
            if (File.Exists(auditLogPath))
            {
                if (AuditLogList.ReadFromFile(auditLogPath, out var auditLogList))
                {
                    auditLog = auditLogList;

                    if (expectedSkylineDocumentHash != auditLogList.DocumentHash.HashString)
                    {
                        var entry = getDefaultEntry() ?? AuditLogEntry.CreateUndocumentedChangeEntry();
                        auditLog = new AuditLogList(entry.ChangeParent(auditLog.AuditLogEntries));
                    }
                }
            }

            return ChangeDocumentHash(expectedSkylineDocumentHash).ChangeAuditLog(auditLog);
        }

        public void WriteXml(XmlWriter writer)
        {
            SerializeToXmlWriter(writer, SkylineVersion.CURRENT, null, null);
        }

        public void SerializeToXmlWriter(XmlWriter writer, SkylineVersion skylineVersion, IProgressMonitor progressMonitor,
            IProgressStatus progressStatus)
        {
            var document = DocumentAnnotationUpdater.UpdateAnnotations(this, progressMonitor, progressStatus);
            var documentWriter = new DocumentWriter(document, skylineVersion);
            if (progressMonitor != null)
            {
                int transitionsWritten = 0;
                int totalTransitionCount = MoleculeTransitionCount;
                documentWriter.WroteTransitions += count =>
                {
                    transitionsWritten += count;
                    progressStatus = progressStatus.UpdatePercentCompleteProgress(progressMonitor, transitionsWritten, totalTransitionCount);
                };
            }
            documentWriter.WriteXml(writer);
        }

        public static string GetAuditLogPath(string docPath)
        {
            if (string.IsNullOrEmpty(docPath))
                return docPath;

            var directory = Path.GetDirectoryName(docPath);

            if (directory == null)
                return null;

            var fileName = Path.GetFileNameWithoutExtension(docPath) + AuditLogList.EXT;
            return Path.Combine(directory, fileName);
        }

        public void SerializeToFile(string tempName, string displayName, SkylineVersion skylineVersion, IProgressMonitor progressMonitor)
        {
            string hash;
            using (var writer = new XmlTextWriter(HashingStream.CreateWriteStream(tempName), Encoding.UTF8)
            {
                Formatting = Formatting.Indented
            })
            {
                hash = Serialize(writer, displayName, skylineVersion, progressMonitor);
            }

            var auditLogPath = GetAuditLogPath(displayName);

            if (Settings.DataSettings.AuditLogging && AuditLog != null)
            {
                var auditLog = AuditLog.RecomputeEnExtraInfos()
                    .RecalculateHashValues(skylineVersion.SrmDocumentVersion, hash);
                auditLog.WriteToFile(auditLogPath, hash, skylineVersion.SrmDocumentVersion);
            }
            else if (File.Exists(auditLogPath))
                Helpers.TryTwice(() => File.Delete(auditLogPath));
        }

        public string Serialize(XmlTextWriter writer, string displayName, SkylineVersion skylineVersion, IProgressMonitor progressMonitor)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(@"srm_settings");
            SerializeToXmlWriter(writer, skylineVersion, progressMonitor, new ProgressStatus(Path.GetFileName(displayName)));
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            return ((HashingStream) writer.BaseStream)?.Done();
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ValidateResults()
        {
            foreach (PeptideDocNode nodePep in Molecules)
            {
                ValidateChromInfo(Settings, nodePep.Results);
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    ValidateChromInfo(Settings, nodeGroup.Results);
                    foreach (TransitionDocNode nodeTran in nodeGroup.Transitions)
                    {
                        ValidateChromInfo(Settings, nodeTran.Results);
                    }
                }
            }
        }

        private static void ValidateChromInfo<TInfo>(SrmSettings settings, Results<TInfo> results)
            where TInfo : ChromInfo
        {
            if (!settings.HasResults)
            {
                if (results != null)
                    throw new InvalidDataException(Resources.SrmDocumentValidateChromInfoResults_found_in_document_with_no_replicates);
                return;
            }
            // This check was a little too agressive.
            // If a node's transition count is zero, then it can still have null for results.
//            if (results == null)
//                throw new InvalidDataException("DocNode missing results in document with replicates.");
            if (results != null)
                results.Validate(settings);
        }

        public double GetCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, int step)
        {
            return GetCollisionEnergy(Settings, nodePep, nodeGroup, nodeTran,
                                      Settings.TransitionSettings.Prediction.CollisionEnergy, step);
        }

        public static double GetCollisionEnergy(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, CollisionEnergyRegression regression, int step)
        {
            var ce = GetExplicitCollisionEnergy(nodeGroup, nodeTran);
            if (regression != null)
            {
                // If still no explicit CE value found the CE is calculated using the provided regression, if any.
                if (!ce.HasValue)
                {
                    var charge = nodeGroup.TransitionGroup.PrecursorAdduct;
                    var mz = settings.GetRegressionMz(nodePep, nodeGroup);
                    ce = regression.GetCollisionEnergy(charge, mz);
                }
                return ce.Value + regression.StepSize * step;
            }
            return ce ?? 0.0;
        }

        private static double? GetExplicitCollisionEnergy(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            double? ce = null;
            if (nodeTran != null)
            {
                // Collision Energy explicitly declared at the transition level is taken to be the correct value.
                ce = nodeTran.ExplicitValues.CollisionEnergy;
            }
            else
            {
                // If we're only given a precursor, use the explicit CE of its children if they all agree.
                var ceValues = nodeGroup.Transitions.Select(node =>
                    node.ExplicitValues.CollisionEnergy).Distinct().ToArray();
                if (ceValues.Length == 1)
                {
                    ce = ceValues[0];
                }
            }
            // If no transition-level declaration then explicitly declared value at the precursor level is used.
            return ce ?? nodeGroup.ExplicitValues.CollisionEnergy;
        }

        public double? GetOptimizedCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            var prediction = Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var lib = prediction.OptimizedLibrary;
            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.collision_energy,
                    Settings.GetSourceTarget(nodePep), nodeGroup.PrecursorAdduct,
                    nodeTransition.FragmentIonName, nodeTransition.Transition.Adduct);
                if (optimization != null)
                {
                    return optimization.Value;
                }
            }

            if (prediction.OptimizedMethodType != OptimizedMethodType.None)
            {
                return OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                    nodePep, nodeGroup, nodeTransition, methodType, prediction.CollisionEnergy,
                    GetCollisionEnergy);
            }

            return null;
        }

        public double GetDeclusteringPotential(PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, int step)
        {
            return GetDeclusteringPotential(Settings, nodePep, nodeGroup, nodeTran,
                                            Settings.TransitionSettings.Prediction.DeclusteringPotential, step);
        }

        public static double GetDeclusteringPotential(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, DeclusteringPotentialRegression regression, int step)
        {
            if (ExplicitTransitionValues.Get(nodeTran).DeclusteringPotential.HasValue)
                return nodeTran.ExplicitValues.DeclusteringPotential.Value; // Explicitly set, overrides calculation
            if (regression == null)
                return 0;
            double mz = settings.GetRegressionMz(nodePep, nodeGroup);
            return regression.GetDeclustringPotential(mz) + regression.StepSize * step;
        }

        public double GetOptimizedDeclusteringPotential(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            var prediction = Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var regression = prediction.DeclusteringPotential;

            return OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, methodType, regression, GetDeclusteringPotential);
        }

        public IEnumerable<string> GetMissingCompensationVoltages(CompensationVoltageParameters.Tuning tuneLevel)
        {
            if (tuneLevel.Equals(CompensationVoltageParameters.Tuning.none))
                yield break;

            var lib = Settings.HasOptimizationLibrary
                ? Settings.TransitionSettings.Prediction.OptimizedLibrary
                : null;
            var optType = CompensationVoltageParameters.GetOptimizationType(tuneLevel);

            foreach (var nodePep in Molecules)
            {
                foreach (var nodeTranGroup in nodePep.TransitionGroups.Where(nodeGroup => nodeGroup.Children.Any()))
                {
                    if (nodeTranGroup.ExplicitValues.CompensationVoltage.HasValue)
                        break;

                    if (lib != null && !lib.IsNone && lib.GetOptimization(optType, Settings.GetSourceTarget(nodePep),
                            nodeTranGroup.PrecursorAdduct) != null)
                        break;

                    double? cov;
                    switch (tuneLevel)
                    {
                        case CompensationVoltageParameters.Tuning.fine:
                            cov = OptimizationStep<CompensationVoltageRegressionFine>.FindOptimizedValueFromResults(
                                Settings, nodePep, nodeTranGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageFine);
                            break;
                        case CompensationVoltageParameters.Tuning.medium:
                            cov = OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValueFromResults(
                                Settings, nodePep, nodeTranGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageMedium);
                            break;
                        default:
                            cov = OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValueFromResults(
                                Settings, nodePep, nodeTranGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageRough);
                            break;
                    }

                    if (!cov.HasValue)
                    {
                        // Check for CoV as an ion mobility parameter
                        var libKey = nodeTranGroup.GetLibKey(Settings, nodePep);
                        var imInfo = Settings.GetIonMobilities(new[] { libKey }, null);
                        var im = imInfo.GetLibraryMeasuredIonMobilityAndCCS(libKey, nodeTranGroup.PrecursorMz, null);
                        if (im.IonMobility.Units == eIonMobilityUnits.compensation_V)
                        {
                            cov = im.IonMobility.Mobility;
                        }
                    }

                    if (!cov.HasValue || cov.Value.Equals(0))
                    {
                        yield return nodeTranGroup.ToString();
                    }
                }
            }
        }

        public CompensationVoltageParameters.Tuning HighestCompensationVoltageTuning()
        {
            if (Settings.HasOptimizationLibrary)
            {
                // Optimization library may contain fine tune CoV values
                if (Settings.TransitionSettings.Prediction.OptimizedLibrary.HasType(OptimizationType.compensation_voltage_fine))
                    return CompensationVoltageParameters.Tuning.fine;
            }

            // Get highest tune level imported
            var highestTuneLevel = CompensationVoltageParameters.Tuning.none;
            if (Settings.HasResults)
            {
                foreach (var chromatogram in Settings.MeasuredResults.Chromatograms)
                {
                    if (chromatogram.OptimizationFunction == null)
                        continue;

                    var optType = chromatogram.OptimizationFunction.OptType;
                    if (OptimizationType.compensation_voltage_fine.Equals(optType))
                    {
                        return CompensationVoltageParameters.Tuning.fine;
                    }
                    else if (highestTuneLevel < CompensationVoltageParameters.Tuning.medium &&
                             OptimizationType.compensation_voltage_medium.Equals(optType))
                    {
                        highestTuneLevel = CompensationVoltageParameters.Tuning.medium;
                    }
                    else if (highestTuneLevel < CompensationVoltageParameters.Tuning.rough &&
                             OptimizationType.compensation_voltage_rough.Equals(optType))
                    {
                        highestTuneLevel = CompensationVoltageParameters.Tuning.rough;
                    }
                }
            }
            return highestTuneLevel;
        }

        public IEnumerable<string> GetPrecursorsWithoutTopRank(int primaryTransitionCount, int? schedulingReplicateIndex)
        {
            foreach (var seq in MoleculeGroups)
            {
                foreach (PeptideDocNode nodePep in seq.Children)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children.Where(nodeGroup => ((TransitionGroupDocNode)nodeGroup).TransitionCount > 1))
                    {
                        bool rankOne = false;
                        foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                        {
                            var groupPrimary = primaryTransitionCount > 0
                                ? nodePep.GetPrimaryResultsGroup(nodeGroup)
                                : null;
                            int? rank = nodeGroup.GetRank(groupPrimary, nodeTran, schedulingReplicateIndex);
                            if (rank.HasValue && rank == 1)
                            {
                                rankOne = true;
                                break;
                            }
                        }
                        if (!rankOne)
                        {
                            yield return nodeGroup.ToString();
                        }
                    }
                }
            }
        }

        public double GetCompensationVoltage(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, int step, CompensationVoltageParameters.Tuning tuneLevel)
        {
            var cov = Settings.TransitionSettings.Prediction.CompensationVoltage;
            switch (tuneLevel)
            {
                case CompensationVoltageParameters.Tuning.fine:
                    return GetCompensationVoltageFine(Settings, nodePep, nodeGroup, nodeTran, cov, step);   
                case CompensationVoltageParameters.Tuning.medium:
                    return GetCompensationVoltageMedium(Settings, nodePep, nodeGroup, nodeTran, cov, step);
                default:
                    return GetCompensationVoltageRough(Settings, nodePep, nodeGroup, nodeTran, cov, step);
            }
        }

        private static double GetCompensationVoltageRough(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            return (regression.MinCov + regression.MaxCov)/2 + regression.StepSizeRough*step;
        }

        private static double GetCompensationVoltageMedium(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            double? covRough = OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValueFromResults(settings,
                nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageRough);
            return covRough.HasValue ? covRough.Value + regression.StepSizeMedium*step : 0;
        }

        public static double GetCompensationVoltageFine(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            double? covMedium = OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValueFromResults(settings,
                nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageMedium);
            return covMedium.HasValue ? covMedium.Value + regression.StepSizeFine*step : 0;
        }

        public double? GetOptimizedCompensationVoltage(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, CompensationVoltageParameters.Tuning tuneLevel)
        {
            if (nodeGroup.ExplicitValues.CompensationVoltage.HasValue)
                return nodeGroup.ExplicitValues.CompensationVoltage.Value;

            var prediction = Settings.TransitionSettings.Prediction;
            var lib = prediction.OptimizedLibrary;

            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(CompensationVoltageParameters.GetOptimizationType(tuneLevel),
                    Settings.GetSourceTarget(nodePep), nodeGroup.PrecursorAdduct);
                if (optimization != null)
                    return optimization.Value;
            }

            var covMain = prediction.CompensationVoltage;
            if (covMain == null)
                return null;

            switch (tuneLevel)
            {
                case CompensationVoltageParameters.Tuning.fine:
                    return OptimizationStep<CompensationVoltageRegressionFine>.FindOptimizedValue(Settings, nodePep,
                        nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionFine,
                        GetCompensationVoltageFine);
                case CompensationVoltageParameters.Tuning.medium:
                    return OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValue(Settings, nodePep,
                        nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionMedium,
                        GetCompensationVoltageMedium);
                case CompensationVoltageParameters.Tuning.rough:
                    return OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValue(Settings, nodePep,
                        nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionRough,
                        GetCompensationVoltageRough);
            }
            return null;
        }


        #endregion

        /// <summary>
        /// Compares documents, returns null if equal, or a text diff if not
        /// </summary>
        public static string EqualsVerbose(SrmDocument expected, SrmDocument actual)
        {
            if (ReferenceEquals(null, expected))
            {
                return ReferenceEquals(null, actual) ? null : @"expected a null document";
            }
            if (ReferenceEquals(null, actual))
            {
                return @"expected a non-null document";
            }
            if (expected.Equals(actual))
            {
                return null;
            }

            string textExpected;
            using (var stringWriterExpected = new StringWriter())
            using (var xmlWriterExpected = new XmlTextWriter(stringWriterExpected){ Formatting = Formatting.Indented })
            {
                expected.Serialize(xmlWriterExpected, null, SkylineVersion.CURRENT, null);
                textExpected = stringWriterExpected.ToString();
            }
            string textActual;
            using (var stringWriterActual = new StringWriter())
            using (var xmlWriterActual = new XmlTextWriter(stringWriterActual) { Formatting = Formatting.Indented })
            {
                actual.Serialize(xmlWriterActual, null, SkylineVersion.CURRENT, null);
                textActual = stringWriterActual.ToString();
            }

            var linesExpected = textExpected.Split('\n');
            var linesActual = textActual.Split('\n');
            int lineNumber;
            for (lineNumber = 0; lineNumber < linesExpected.Length && lineNumber < linesActual.Length; lineNumber++)
            {
                var lineExpected = linesExpected[lineNumber];
                var lineActual = linesActual[lineNumber];
                if (!Equals(lineExpected, lineActual))
                {
                    return $@"Expected XML representation of document does not match actual at line {lineNumber}\n" +
                           $@"Expected line:\n{lineExpected}\n" +
                           $@"Actual line:\n{lineActual}\n" +
                           $@"Expected full document:\n{textExpected}\n" +
                           $@"Actual full document:\n{textActual}\n";
                }
            }
            if (lineNumber < linesExpected.Length || lineNumber < linesActual.Length)
            {
                return @"Expected XML representation of document is not the same length as actual\n"+
                       $@"Expected full document:\n{textExpected}\n"+
                       $@"Actual full document:\n{textActual}\n";
            }

            return @"Expected document does not match actual, but the difference does not appear in the XML representation. Difference may be in a library instead.";
        }

        #region object overrides

        public bool Equals(SrmDocument obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!base.Equals(obj))
                return false;
            if (!Equals(obj.Settings, Settings))
                return false;
            if (!Equals(obj.DeferSettingsChanges, DeferSettingsChanges))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as SrmDocument);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ Settings.GetHashCode();
            }
        }

        #endregion
    }


    public class SrmDocumentPair : ObjectPair<SrmDocument>
    {
        protected SrmDocumentPair(SrmDocument oldDoc, SrmDocument newDoc, SrmDocument.DOCUMENT_TYPE defaultDocumentTypeForAuditLog)
            : base(oldDoc, newDoc)
        {
            NewDocumentType = newDoc != null && newDoc.DocumentType != SrmDocument.DOCUMENT_TYPE.none 
                ? newDoc.DocumentType
                : defaultDocumentTypeForAuditLog;
            OldDocumentType = oldDoc != null && oldDoc.DocumentType != SrmDocument.DOCUMENT_TYPE.none 
                ? oldDoc.DocumentType
                : NewDocumentType;
        }

        public static SrmDocumentPair Create(SrmDocument oldDoc, SrmDocument newDoc, 
            SrmDocument.DOCUMENT_TYPE defaultDocumentTypeForLogging)
        {
            return new SrmDocumentPair(oldDoc, newDoc, defaultDocumentTypeForLogging);
        }

        public ObjectPair<object> ToObjectType()
        {
            return Transform(doc => (object) doc);
        }

        public SrmDocument OldDoc { get { return OldObject; } }
        public SrmDocument NewDoc { get { return NewObject; } }

        // Used for "peptide"->"molecule" translation cue in human readable logs
        public SrmDocument.DOCUMENT_TYPE OldDocumentType { get; private set; } // Useful when something in document is being removed, which might cause a change from mixed to proteomic but you want to log event as "molecule" rather than "peptide"
        public SrmDocument.DOCUMENT_TYPE NewDocumentType { get; private set; } // Useful when something is being added, which might cause a change from proteomic to mixed so you want to log event as "molecule" rather than "peptide"

    }

    public class Targets
    {
        private readonly SrmDocument _doc;
        public Targets(SrmDocument doc)
        {
            _doc = doc;
        }

        [TrackChildren(ignoreName:true)]
        public IList<DocNode> Children
        {
            get { return _doc.Children; }
        }
    }
}
