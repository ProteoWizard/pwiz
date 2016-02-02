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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Results;
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
        public DocumentChangedEventArgs(SrmDocument documentPrevious, bool inSelUpdateLock = false)
        {
            DocumentPrevious = documentPrevious;
            IsInSelUpdateLock = inSelUpdateLock;
        }

        public SrmDocument DocumentPrevious { get; private set; }

        /// <summary>
        /// True when SequenceTree.IsInUpdateLock is set, which means the selection
        /// cannot be trusted as reflecting the current document.
        /// </summary>
        public bool IsInSelUpdateLock { get; private set; }
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
    [XmlRoot("srm_settings")] // Not L10N
    public class SrmDocument : DocNodeParent, IXmlSerializable
    {
        /// <summary>
        /// Document extension on disk
        /// </summary>
        public const string EXT = ".sky"; // Not L10N

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
                                                 SrmDocumentSharing.EXT_SKY_ZIP);
            }    
        }

        public const double FORMAT_VERSION_0_1 = 0.1;
        public const double FORMAT_VERSION_0_2 = 0.2;
        public const double FORMAT_VERSION_0_8 = 0.8;
        public const double FORMAT_VERSION_1_2 = 1.2;   // Used briefly during development of v1.3
        public const double FORMAT_VERSION_1_3 = 1.3;
        public const double FORMAT_VERSION_1_4 = 1.4;
        public const double FORMAT_VERSION_1_5 = 1.5;
        public const double FORMAT_VERSION_1_6 = 1.6;   // Adds richer protein metadata
        public const double FORMAT_VERSION_1_7 = 1.7;   // Adds Ion Mobility handling
        public const double FORMAT_VERSION_1_8 = 1.8;   // Adds Reporter Ions and non proteomic transitions
        public const double FORMAT_VERSION_1_9 = 1.9;   // Adds sequence lookup key for decoys
        public const double FORMAT_VERSION_2_61 = 2.61;   // Adds drift time high energy offsets for Waters IMS
        public const double FORMAT_VERSION_2_62 = 2.62;   // Revised small molecule support
        public const double FORMAT_VERSION_3_1 = 3.1;   // Release format. No change from 2.62
        public const double FORMAT_VERSION_3_11 = 3.11; // Adds compensation voltage optimization support
        public const double FORMAT_VERSION_3_12 = 3.12; // Adds small molecule ion labels and multiple charge states
        public const double FORMAT_VERSION_3_5 = 3.5; // Release format
        public const double FORMAT_VERSION_3_51 = 3.51; // Adds document GUID and Panorama URI
        public const double FORMAT_VERSION_3_52 = 3.52; // Cleans up potential ambiguity around explicit vs calculated slens and cone voltage
        public const double FORMAT_VERSION = FORMAT_VERSION_3_52;

        public const int MAX_PEPTIDE_COUNT = 100*1000;
        public const int MAX_TRANSITION_COUNT = 6*MAX_PEPTIDE_COUNT; // Modern DIA experiments may often have 6 transitions per peptide

        // Version of this document in deserialized XML

        public SrmDocument(SrmSettings settings)
            : base(new SrmDocumentId(), Annotations.EMPTY, new PeptideGroupDocNode[0], false)
        {
            FormatVersion = FORMAT_VERSION;
            Settings = settings;
        }

        public SrmDocument(SrmDocument doc, SrmSettings settings, IList<DocNode> children)
            : base(doc.Id, Annotations.EMPTY, children, false)
        {
            FormatVersion = doc.FormatVersion;
            RevisionIndex = doc.RevisionIndex + 1;
            UserRevisionIndex = doc.UserRevisionIndex;
            Settings = settings;
            CheckIsProteinMetadataComplete();
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { 
            get { throw new InvalidOperationException();}
        }

        public double FormatVersion { get; private set; }

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
        public int CustomIonCount { get { return CustomIons.Count(); } } 

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
                return Molecules.Where(p => !p.Peptide.IsCustomIon);
            }
        }

        /// <summary>
        /// Return all <see cref="PeptideDocNode"/> that are custom ions
        /// </summary>
        public IEnumerable<PeptideDocNode> CustomIons
        {
            get
            {
                return Molecules.Where(p => p.Peptide.IsCustomIon);
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
                return MoleculeTransitionGroups.Where(t => !t.TransitionGroup.Peptide.IsCustomIon);
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

        public HashSet<string> GetRetentionTimeStandards()
        {
            try
            {
                return GetRetentionTimeStandardsOrThrow();
            }
            catch (Exception)
            {
                return new HashSet<string>();
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

        private HashSet<string> GetRetentionTimeStandardsOrThrow()
        {
            var rtRegression = Settings.PeptideSettings.Prediction.RetentionTime;
            if (rtRegression == null || rtRegression.Calculator == null)
                return new HashSet<string>();

            var regressionPeps = rtRegression.Calculator.GetStandardPeptides(Peptides.Select(
                nodePep => Settings.GetModifiedSequence(nodePep)));
            return new HashSet<string>(regressionPeps);
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
                    yield return "Settings.MeasuredResults " + whyNot; // Not L10N
                if (Settings.HasLibraries && (whyNot = Settings.PeptideSettings.Libraries.IsNotLoadedExplained)!=null)
                    yield return "Settings.PeptideSettings.Libraries: " + whyNot; // Not L10N
                if ((whyNot = IrtDbManager.IsNotLoadedDocumentExplained(this)) != null)
                    yield return whyNot; // Not L10N
                if ((whyNot = OptimizationDbManager.IsNotLoadedDocumentExplained(this)) != null)
                    yield return whyNot; // Not L10N
                if ((whyNot = DocumentRetentionTimes.IsNotLoadedExplained(Settings)) != null)
                    yield return whyNot; // Not L10N
                // BackgroundProteome?
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

        public string GetPeptideGroupId(bool peptideList)
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (PeptideGroupDocNode nodeGroup in Children)
                ids.Add(nodeGroup.Name);

            string baseId = peptideList
                ? Resources.SrmDocument_GetPeptideGroupId_peptides 
                : Resources.SrmDocument_GetPeptideGroupId_sequence; 
            int i = 1;
            while (ids.Contains(baseId + i))
                i++;
            return baseId + i;
        }

        public bool CanTrigger(int? replicateIndex)
        {
            return Molecules.All(p => p.CanTrigger(replicateIndex));
        }

        public bool CanSchedule(bool singleWindow)
        {
            return Settings.PeptideSettings.Prediction.CanSchedule(this, singleWindow
                        ? PeptidePrediction.SchedulingStrategy.single_window
                        : PeptidePrediction.SchedulingStrategy.all_variable_window);
        }

        private void CheckIsProteinMetadataComplete()
        {
            // Non proteomic molecules never do protein metadata searches
            var unsearched = (from pg in PeptideGroups where pg.ProteinMetadata.NeedsSearch() select pg);
            IsProteinMetadataPending = unsearched.Any();
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
        protected override IList<DocNode> OnChangingChildren(DocNodeParent clone)
        {
            if (ReferenceEquals(clone, this))
                return Children;

            SrmDocument docClone = (SrmDocument)clone;
            docClone.RevisionIndex = RevisionIndex + 1;

            // Make sure peptide standards lists are up to date
            docClone.Settings = Settings.CachePeptideStandards(Children, docClone.Children);

            // Note protein metadata readiness
            docClone.CheckIsProteinMetadataComplete();

            // If this document has associated results, update the results
            // for any peptides that have changed.
            if (!Settings.HasResults)
                return docClone.Children;

            // Store indexes to previous results in a dictionary for lookup
            var dictPeptideIdPeptide = new Dictionary<int, PeptideDocNode>();
            // Unless the normalization standards have changed, which require recalculating of all ratios
            if (ReferenceEquals(Settings.GetPeptideStandards(PeptideDocNode.STANDARD_TYPE_NORMALIZAITON),
                docClone.Settings.GetPeptideStandards(PeptideDocNode.STANDARD_TYPE_NORMALIZAITON)))
            {
                foreach (var nodePeptide in Molecules)
                    dictPeptideIdPeptide.Add(nodePeptide.Peptide.GlobalIndex, nodePeptide);
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
            return new SrmDocument(this, settingsNew, Children);
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
            if (!diff.RequiresDocNodeUpdate)
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
                    ParallelEx.For(0, Children.Count, i =>
                    {
                        var nodeGroup = (PeptideGroupDocNode) Children[i];
                        childrenParallel[i] = nodeGroup.ChangeSettings(settingsParallel, diff);
                    });
                    childrenNew = childrenParallel;
                }
                else
                {
                    // Changes that do not change the peptides can be done quicker with
                    // parallel enumeration of the peptides
                    var moleculeGroupPairs = GetMoleculeGroupPairs(Children);
                    var moleculeNodes = new PeptideDocNode[moleculeGroupPairs.Length];
                    var settingsParallel = settingsNew;
                    ParallelEx.For(0, moleculeGroupPairs.Length, i =>
                    {
                        var nodePep = moleculeGroupPairs[i].NodeMolecule;
                        moleculeNodes[i] = nodePep.ChangeSettings(settingsParallel, diff);
                    });

                    childrenNew = RegroupMolecules(Children, moleculeNodes,
                        (nodeGroup, children) => nodeGroup.RankChildren(settingsParallel, children));
                }

                // Don't change the children, if the resulting list contains
                // only reference equal children of the same length and in the
                // same order.
                if (ArrayUtil.ReferencesEqual(childrenNew, Children))
                    childrenNew = Children;

                // Results handler changes for re-integration last only long enough
                // to change the children
                if (settingsNew.PeptideSettings.Integration.ResultsHandler != null)
                    settingsNew = settingsNew.ChangePeptideIntegration(i => i.ChangeResultsHandler(null));
                return new SrmDocument(this, settingsNew, childrenNew);
            }
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
                            docNew.Settings.PeptideSettings.Modifications,
                            staticMods, heavyMods);
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
                                          IdentityPath to,
                                          out IdentityPath firstAdded)
        {
            List<MeasuredRetentionTime> irtPeptides;
            List<SpectrumMzInfo> librarySpectra;
            List<TransitionImportErrorInfo> errorList;
            List<PeptideGroupDocNode> peptideGroups;
            return ImportMassList(inputs, null, to, out firstAdded, out irtPeptides, out librarySpectra, out errorList, out peptideGroups);
        }

        public SrmDocument ImportMassList(MassListInputs inputs,
                                          IdentityPath to,
                                          out IdentityPath firstAdded,
                                          out List<MeasuredRetentionTime> irtPeptides,
                                          out List<SpectrumMzInfo> librarySpectra,
                                          out List<TransitionImportErrorInfo> errorList)
        {
            List<PeptideGroupDocNode> peptideGroups;
            return ImportMassList(inputs, null, to, out firstAdded, out irtPeptides, out librarySpectra, out errorList, out peptideGroups);
        }

        public SrmDocument ImportMassList(MassListInputs inputs, 
                                          IProgressMonitor progressMonitor,
                                          IdentityPath to,
                                          out IdentityPath firstAdded,
                                          out List<MeasuredRetentionTime> irtPeptides,
                                          out List<SpectrumMzInfo> librarySpectra,
                                          out List<TransitionImportErrorInfo> errorList,
                                          out List<PeptideGroupDocNode> peptideGroups)
        {
            MassListImporter importer = new MassListImporter(this, inputs);
            IdentityPath nextAdd;
            peptideGroups = importer.Import(progressMonitor, out irtPeptides, out librarySpectra, out errorList).ToList();
            var docNew = AddPeptideGroups(peptideGroups, false, to, out firstAdded, out nextAdd);
            var pepModsNew = importer.GetModifications(docNew);
            if (!ReferenceEquals(pepModsNew, Settings.PeptideSettings.Modifications))
            {
                docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                docNew.Settings.UpdateDefaultModifications(false);
            }
            return docNew;
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
                                                    conflict.NewPeptide.PeptideModSeq));
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

        // Note these lead with zzz in hopes of placing them last in any sorting tests
        public static string TestingNonProteomicBaseName = "zzzTestingNonProteomic";  // Not L10N
        public static string TestingNonProteomicMoleculeGroupName = TestingNonProteomicBaseName + "MoleculeGroup";  // Not L10N
        public static string TestingNonProteomicMoleculeName = TestingNonProteomicBaseName + "Molecule";  // Not L10N
        public static string TestingNonProteomicPrecursorName = TestingNonProteomicBaseName + "Precursor";  // Not L10N
        public static string TestingNonProteomicFragmentName = TestingNonProteomicBaseName + "Fragment";  // Not L10N
        public static string TestingNonProteomicFragment2Name = TestingNonProteomicBaseName + "Fragment2";  // Not L10N

        public static bool IsConvertedFromProteomicTestDocNode(DocNode node)
        {
            // Is this a node that was created for test purposes by transforming an existing peptide doc?
            return (node != null && node.Annotations.Note != null &&
                    node.Annotations.Note.Contains(RefinementSettings.TestingConvertedFromProteomic));
        }

        public static bool IsSpecialNonProteomicTestDocNode(DocNode node)
        {
            if (node != null && node.Annotations.Note != null && node.Annotations.Note.Contains(TestingNonProteomicBaseName))
                return true;
            var docNode = node as PeptideGroupDocNode;
            if (docNode != null)
            {
                return Equals(docNode.Name, TestingNonProteomicMoleculeGroupName);
            }
            else
            {
                var peptideDocNode = node as PeptideDocNode;
                if(peptideDocNode != null)
                {
                    var ion = peptideDocNode.Peptide.CustomIon;
                    return ion != null && Equals(ion.Name, TestingNonProteomicMoleculeName);
                }
                else
                {
                    var groupDocNode = node as TransitionGroupDocNode;
                    if (groupDocNode != null)
                    {
                        var ion = groupDocNode.TransitionGroup.CustomIon;
                        return ion != null && Equals(ion.Name, TestingNonProteomicMoleculeName);
                    }
                    else
                    {
                        var transitionDocNode = node as TransitionDocNode;
                        if (transitionDocNode != null)
                        {
                            var ion = transitionDocNode.Transition.CustomIon;
                            return (ion != null) ? (Equals(ion.Name, TestingNonProteomicFragmentName) || Equals(ion.Name, TestingNonProteomicFragment2Name) ): Equals(transitionDocNode.FragmentIonName, TestingNonProteomicPrecursorName);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For automated test of custom molecules in tests that aren't originally designed to test that at all
        /// Creates a peptide group with a custom molecule, and three transitions - precursor and custom fragments by formula and by mass
        /// Custom fragment mass needs to be large enough to avoid the default 50 mz lower cutoff in instrument settings
        /// </summary>
        public PeptideGroupDocNode CreateNonProteomicTestPeptideGroupDocNode(IEnumerable<PeptideGroupDocNode> existingPeptideGroups)
        {
            var pepGroup = new PeptideGroup();
            var note = Annotations.Merge(new Annotations(TestingNonProteomicBaseName, null, 0));  // Tag it as not needing/wanting canonical small molecule sort - these need to be at the doc end, always
            var pep = new Peptide(new DocNodeCustomIon("C16O4H4", TestingNonProteomicMoleculeName)); // Not L10N
            const int charge = -1; // Negative charge for maximum test value, since that's new too
            var tranGroup = new TransitionGroup(pep, pep.CustomIon, charge, IsotopeLabelType.light);
            var tranPrecursor = new Transition(tranGroup, IonType.precursor, 0, 0, charge, null, pep.CustomIon);
            // Specify formula
            var tranFragment = new Transition(tranGroup, charge, 0, new DocNodeCustomIon("C2H2O2", TestingNonProteomicFragmentName)); // Not L10N
            // Specify mass
            var tranFragment2 = new Transition(tranGroup, charge, 0, new DocNodeCustomIon(tranFragment.CustomIon.MonoisotopicMass, tranFragment.CustomIon.AverageMass, TestingNonProteomicFragment2Name)); // Not L10N

            var peptideGroupDocNodes = existingPeptideGroups as PeptideGroupDocNode[] ?? existingPeptideGroups.ToArray();
            bool autoManageChildren = (!peptideGroupDocNodes.Any()) || peptideGroupDocNodes.First().AutoManageChildren; // Try to look like any existing
            var hasPrecursorTransitions = (!peptideGroupDocNodes.Any()) || peptideGroupDocNodes.Any(n => n.Molecules.Any(p => p.TransitionGroups.Any(t => t.Transitions.Any(r => r.Transition.IsPrecursor())))); // Try to look like any existing

            // Use any existing isotope distribution info
            var transitionGroups =
                peptideGroupDocNodes.SelectMany(node => node.Children.Cast<PeptideDocNode>())
                    .SelectMany(node => node.Children.Cast<TransitionGroupDocNode>());
            var transitionGroupDocNodes = transitionGroups as TransitionGroupDocNode[] ?? transitionGroups.ToArray();
            TransitionIsotopeDistInfo isotopeDistInfo =
             ((transitionGroupDocNodes.Any() && transitionGroupDocNodes.First().HasIsotopeDist)) ?
                TransitionDocNode.GetIsotopeDistInfo(tranFragment, null, transitionGroupDocNodes.First().IsotopeDist) : null;
            if (isotopeDistInfo == null)
            {
                var nodeGroup = transitionGroupDocNodes.Any() ? new TransitionGroupDocNode(tranGroup, null, Settings, null, null, ExplicitTransitionGroupValues.EMPTY, null,
                                                                  null,
                                                                  autoManageChildren) :
                                                                  null;
                if (nodeGroup != null)
                    isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(tranPrecursor, null, nodeGroup.IsotopeDist);
            }

            var tranPrecursorNode = new TransitionDocNode(tranPrecursor, note, null,
                pep.CustomIon.GetMass(Settings.TransitionSettings.Prediction.FragmentMassType), isotopeDistInfo, null, null);
            var tranFragmentNode = new TransitionDocNode(tranFragment, note, null,
                tranFragment.CustomIon.GetMass(Settings.TransitionSettings.Prediction.FragmentMassType), null, null, null);
            var tranFragmentNode2 = new TransitionDocNode(tranFragment2, note, null,
                tranFragment2.CustomIon.GetMass(Settings.TransitionSettings.Prediction.FragmentMassType), null, null, null);
            var tranGroupNode = new TransitionGroupDocNode(tranGroup, note, Settings, null, null, ExplicitTransitionGroupValues.EMPTY, null,
                hasPrecursorTransitions ? new[] { tranPrecursorNode, tranFragmentNode, tranFragmentNode2 } : new[] { tranFragmentNode, tranFragmentNode2 }, 
                autoManageChildren);
            var pepNode = new PeptideDocNode(pep, Settings, null, null, null, new[] { tranGroupNode }, true);
            var metadata = new ProteinMetadata(TestingNonProteomicMoleculeGroupName, String.Empty).SetWebSearchCompleted(); 
            return new PeptideGroupDocNode(pepGroup, note, metadata, new[] { pepNode }, true);
            
        }

        public SrmDocument AddPeptideGroups(IEnumerable<PeptideGroupDocNode> peptideGroupsNew,
            bool peptideList, IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdd)
        {
            // For multiple add operations, make the next addtion at the same location by default
            nextAdd = to;
            var peptideGroupsAdd = peptideGroupsNew.ToList();

            // Code for the purpose of testing custom molecules - normally used only in automated test
            // Ensures that every non-empty document has a nonproteomic node in order to maximize testing of this new (as of Aug 2014) functionality
            if (Properties.Settings.Default.TestSmallMolecules &&  // Special test mode?
                (peptideGroupsAdd.Any() || Molecules.Any()) &&  // Will resulting document be non-empty?
                Molecules.All(p => p.IsProteomic) && // Does current doc lack non-proteomic nodes?
                !peptideGroupsAdd.Any(p => p.IsNonProteomic)) // Does list to be added lack non-proteomic nodes?
            {
                peptideGroupsAdd.Add(CreateNonProteomicTestPeptideGroupDocNode(peptideGroupsAdd));
            }

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
                string lookupSequence = nodePep.SourceUnmodifiedTextId;
                var lookupMods = nodePep.SourceExplicitMods;
                IsotopeLabelType labelType;
                double[] retentionTimes;
                Settings.TryGetRetentionTimes(lookupSequence, nodeGroup.TransitionGroup.PrecursorCharge, lookupMods,
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
                                                              (float) mzMatchTolerance, loadPoints, out arrayChromInfo))
            {
                throw new ArgumentOutOfRangeException(string.Format(Resources.SrmDocument_ChangePeak_No_results_found_for_the_precursor__0__in_the_replicate__1__,
                                                                    TransitionGroupTreeNode.GetLabel(nodeGroup.TransitionGroup, nodeGroup.PrecursorMz, string.Empty), nameSet));
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
            if (ReferenceEquals(pepModsNew, pepMods))
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

        #region Implementation of IXmlSerializable

        // ReSharper disable InconsistentNaming
        // Enum.ToString() was too slow for use in the document
        public static class EL
        {
            // v0.1 lists
            // ReSharper disable NonLocalizedString
            public const string selected_proteins = "selected_proteins";
            public const string selected_peptides = "selected_peptides";
            public const string selected_transitions = "selected_transitions";

            public const string protein = "protein";
            public const string note = "note";
            public const string annotation = "annotation";
            public const string alternatives = "alternatives";
            public const string alternative_protein = "alternative_protein";
            public const string sequence = "sequence";
            public const string peptide_list = "peptide_list";
            public const string peptide = "peptide";
            public const string explicit_modifications = "explicit_modifications";
            public const string explicit_static_modifications = "explicit_static_modifications";
            public const string explicit_heavy_modifications = "explicit_heavy_modifications";
            public const string explicit_modification = "explicit_modification";
            public const string variable_modifications = "variable_modifications";
            public const string variable_modification = "variable_modification";
            public const string implicit_modifications = "implicit_modifications";
            public const string implicit_modification = "implicit_modification";
            public const string implicit_static_modifications = "implicit_static_modifications";
            public const string implicit_heavy_modifications = "implicit_heavy_modifications";
            public const string lookup_modifications = "lookup_modifications";
            public const string losses = "losses";
            public const string neutral_loss = "neutral_loss";
            public const string peptide_results = "peptide_results";
            public const string peptide_result = "peptide_result";
            public const string precursor = "precursor";
            public const string precursor_results = "precursor_results";
            public const string precursor_peak = "precursor_peak";
            public const string transition = "transition";
            public const string transition_results = "transition_results";
            public const string transition_peak = "transition_peak";
            public const string transition_lib_info = "transition_lib_info";
            public const string precursor_mz = "precursor_mz";
            public const string product_mz = "product_mz";
            public const string collision_energy = "collision_energy";
            public const string declustering_potential = "declustering_potential";
            public const string start_rt = "start_rt";
            public const string stop_rt = "stop_rt";
            public const string molecule = "molecule";
            // ReSharper restore NonLocalizedString
        }
        // ReSharper restore InconsistentNaming

        // ReSharper disable InconsistentNaming
        // Enum.ToString() was too slow for use in the document
        public static class ATTR
        {
            // ReSharper disable NonLocalizedString
            public const string format_version = "format_version";
            public const string software_version = "software_version";
            public const string name = "name";
            public const string category = "category";
            public const string description = "description";
            public const string label_name = "label_name";  
            public const string label_description = "label_description";  
            public const string accession = "accession";
            public const string gene = "gene";
            public const string species = "species";
            public const string websearch_status = "websearch_status";
            public const string preferred_name = "preferred_name";
            public const string peptide_list = "peptide_list";
            public const string start = "start";
            public const string end = "end";
            public const string sequence = "sequence";
            public const string prev_aa = "prev_aa";
            public const string next_aa = "next_aa";
            public const string index_aa = "index_aa";
            public const string modification_name = "modification_name";
            public const string mass_diff = "mass_diff";
            public const string loss_index = "loss_index";
            public const string calc_neutral_pep_mass = "calc_neutral_pep_mass";
            public const string num_missed_cleavages = "num_missed_cleavages";
            public const string rt_calculator_score = "rt_calculator_score";
            public const string predicted_retention_time = "predicted_retention_time";
            public const string explicit_retention_time = "explicit_retention_time";
            public const string explicit_retention_time_window = "explicit_retention_time_window";
            public const string explicit_drift_time_msec = "explicit_drift_time_msec";
            public const string explicit_drift_time_high_energy_offset_msec = "explicit_drift_time_high_energy_offset_msec";
            public const string avg_measured_retention_time = "avg_measured_retention_time";
            public const string isotope_label = "isotope_label";
            public const string fragment_type = "fragment_type";
            public const string fragment_ordinal = "fragment_ordinal";
            public const string mass_index = "mass_index";
            public const string calc_neutral_mass = "calc_neutral_mass";
            public const string precursor_mz = "precursor_mz";
            public const string charge = "charge";
            public const string precursor_charge = "precursor_charge";   // backward compatibility with v0.1
            public const string product_charge = "product_charge";
            public const string rank = "rank";
            public const string intensity = "intensity";
            public const string auto_manage_children = "auto_manage_children";
            public const string decoy = "decoy";
            public const string decoy_mass_shift = "decoy_mass_shift";
            public const string isotope_dist_rank = "isotope_dist_rank";
            public const string isotope_dist_proportion = "isotope_dist_proportion";
            public const string ion_formula = "ion_formula";
            public const string custom_ion_name = "custom_ion_name";
            public const string mass_monoisotopic = "mass_monoisotopic";
            public const string mass_average = "mass_average";
            public const string modified_sequence = "modified_sequence";
            public const string lookup_sequence = "lookup_sequence";
            public const string cleavage_aa = "cleavage_aa";
            public const string loss_neutral_mass = "loss_neutral_mass";
            public const string collision_energy = "collision_energy";
            public const string explicit_collision_energy = "explicit_collision_energy";
            public const string explicit_s_lens = "explicit_s_lens";
            public const string s_lens_obsolete = "s_lens"; // Really should have been explicit_s_lens
            public const string explicit_cone_voltage = "explicit_cone_voltage";
            public const string cone_voltage_obsolete = "cone_voltage"; // Really should have been explicit_cone_voltage
            public const string declustering_potential = "declustering_potential";
            public const string explicit_declustering_potential = "explicit_declustering_potential";
            public const string explicit_compensation_voltage = "explicit_compensation_voltage";
            public const string standard_type = "standard_type";
            public const string measured_ion_name = "measured_ion_name";
            public const string concentration_multiplier = "concentration_multiplier";
            public const string internal_standard_concentration = "internal_standard_concentration";

            // Results
            public const string replicate = "replicate";
            public const string file = "file";
            public const string step = "step";
            public const string mass_error_ppm = "mass_error_ppm";
            public const string retention_time = "retention_time";
            public const string start_time = "start_time";
            public const string end_time = "end_time";
            public const string area = "area";
            public const string background = "background";
            public const string height = "height";
            public const string fwhm = "fwhm";
            public const string fwhm_degenerate = "fwhm_degenerate";
            public const string truncated = "truncated";
            public const string identified = "identified";
            public const string user_set = "user_set";
            public const string peak_count_ratio = "peak_count_ratio";
            public const string library_dotp = "library_dotp";
            public const string isotope_dotp = "isotope_dotp";
            // ReSharper restore NonLocalizedString
        }
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// For deserialization
        /// </summary>
// ReSharper disable UnusedMember.Local
        private SrmDocument()
// ReSharper restore UnusedMember.Local
            : base(new SrmDocumentId())
        {            
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

        private sealed class XmlReadContext
        {
            private readonly Dictionary<string, string> _dictNonDuplicatedNames = new Dictionary<string, string>();

            public string GetNonDuplicatedName(string name)
            {
                string nonDuplicatedName;
                if (!_dictNonDuplicatedNames.TryGetValue(name, out nonDuplicatedName))
                {
                    _dictNonDuplicatedNames.Add(name, name);
                    nonDuplicatedName = name;
                }
                return nonDuplicatedName;
            }
        }

        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            FormatVersion = reader.GetDoubleAttribute(ATTR.format_version);
            if (FormatVersion == 0)
                FormatVersion = FORMAT_VERSION_0_1;
            else if (FormatVersion > FORMAT_VERSION)
            {
                throw new VersionNewerException(
                    string.Format(Resources.SrmDocument_ReadXml_The_document_format_version__0__is_newer_than_the_version__1__supported_by__2__,
                                  FormatVersion,  FORMAT_VERSION, Install.ProgramNameAndVersion));
            }

            reader.ReadStartElement();  // Start document element

            Settings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();

            var context = new XmlReadContext();
            PeptideGroupDocNode[] children = null;
            if (reader.IsStartElement())
            {
                // Support v0.1 naming
                if (!reader.IsStartElement(EL.selected_proteins))
                    children = ReadPeptideGroupListXml(reader, context);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    children = ReadPeptideGroupListXml(reader, context);
                    reader.ReadEndElement();
                }
            }

            reader.ReadEndElement();    // End document element

            if (children == null)
                SetChildren(new PeptideGroupDocNode[0]);
            else
            {
                if (Properties.Settings.Default.TestSmallMolecules && children.Any() && !children.Any(p => p.IsNonProteomic)) // Make sure there's a custom ion node present in any non-empty document
                {
                    // Code for the purpose of testing custom molecules - normally used only in automated test
                    children = children.Concat(new[] { CreateNonProteomicTestPeptideGroupDocNode(children) }).ToArray();
                }

                // Make sure peptide standards lists are up to date
                Settings = Settings.CachePeptideStandards(new PeptideGroupDocNode[0], children);

                SetChildren(UpdateResultsSummaries(children, new Dictionary<int, PeptideDocNode>()));

                CheckIsProteinMetadataComplete(); // Background loaders are about to kick in, they need this info.
            }
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at the start of the list.
        /// </summary>
        /// <param name="reader">The reader positioned on the element of the first node</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <returns>An array of <see cref="PeptideGroupDocNode"/> objects for
        ///         inclusion in a <see cref="SrmDocument"/> child list</returns>
        private PeptideGroupDocNode[] ReadPeptideGroupListXml(XmlReader reader, XmlReadContext context)
        {
            var list = new List<PeptideGroupDocNode>();
            while (reader.IsStartElement(EL.protein) || reader.IsStartElement(EL.peptide_list))
            {
                if (reader.IsStartElement(EL.protein))
                    list.Add(ReadProteinXml(reader, context));
                else
                    list.Add(ReadPeptideGroupXml(reader, context));
            }
            return list.ToArray();
        }

        private static ProteinMetadata ReadProteinMetadataXML(XmlReader reader, bool labelNameAndDescription)
        {
            var labelPrefix = labelNameAndDescription ? "label_" : string.Empty; // Not L10N
            return new ProteinMetadata(
                reader.GetAttribute(labelPrefix+ATTR.name),
                reader.GetAttribute(labelPrefix + ATTR.description),
                reader.GetAttribute(ATTR.preferred_name),
                reader.GetAttribute(ATTR.accession),
                reader.GetAttribute(ATTR.gene),
                reader.GetAttribute(ATTR.species),
                reader.GetAttribute(ATTR.websearch_status));
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein&gt; tag.
        /// 
        /// In order to support the v0.1 format, the returned node may represent
        /// either a FASTA sequence or a peptide list.
        /// </summary>
        /// <param name="reader">The reader positioned at a protein tag</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadProteinXml(XmlReader reader, XmlReadContext context)
        {
            string name = reader.GetAttribute(ATTR.name);
            string description = reader.GetAttribute(ATTR.description);
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            var labelProteinMetadata = ReadProteinMetadataXML(reader, true);  // read label_name, label_description, and species, gene etc if any

            reader.ReadStartElement();

            var annotations = ReadAnnotations(reader, context);

            ProteinMetadata[] alternatives;
            if (!reader.IsStartElement(EL.alternatives) || reader.IsEmptyElement)
                alternatives = new ProteinMetadata[0];
            else
            {
                reader.ReadStartElement();
                alternatives = ReadAltProteinListXml(reader);
                reader.ReadEndElement();
            }

            reader.ReadStartElement(EL.sequence);
            string sequence = DecodeProteinSequence(reader.ReadContentAsString());
            reader.ReadEndElement();

            // Support v0.1 documents, where peptide lists were saved as proteins,
            // pre-v0.1 documents, which may not have identified peptide lists correctly.
            if (sequence.StartsWith("X") && sequence.EndsWith("X")) // Not L10N
                peptideList = true;

            // All v0.1 peptide lists should have a settable label
            if (peptideList)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeName(name ?? string.Empty);
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(description);
            }
            // Or any protein without a name attribute
            else if (name != null)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(null);
            }

            PeptideGroup group;
            if (peptideList)
                group = new PeptideGroup();
            // If there is no name attribute, ignore all info from the FASTA header line,
            // since it should be user settable.
            else if (name == null)
                group = new FastaSequence(null, null, null, sequence);
            else
                group = new FastaSequence(name, description, alternatives, sequence);

            PeptideDocNode[] children = null;
            if (!reader.IsStartElement(EL.selected_peptides))
                children = ReadPeptideListXml(reader, context, group);
            else if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement(EL.selected_peptides);
                children = ReadPeptideListXml(reader, context, group);
                reader.ReadEndElement();                    
            }

            reader.ReadEndElement();

            return new PeptideGroupDocNode(group, annotations, labelProteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="ProteinMetadata"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <returns>A new array of <see cref="ProteinMetadata"/></returns>
        private static ProteinMetadata[] ReadAltProteinListXml(XmlReader reader)
        {
            var list = new List<ProteinMetadata>();
            while (reader.IsStartElement(EL.alternative_protein))
            {
                var proteinMetaData = ReadProteinMetadataXML(reader, false);
                reader.Read();
                list.Add(proteinMetaData);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Decodes a FASTA sequence as stored in a XML document to one
        /// with all white space removed.
        /// </summary>
        /// <param name="sequence">The XML format sequence</param>
        /// <returns>The sequence suitible for use in a <see cref="FastaSequence"/></returns>
        private static string DecodeProteinSequence(IEnumerable<char> sequence)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char aa in sequence)
            {
                if (!char.IsWhiteSpace(aa))
                    sb.Append(aa);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> representing
        /// a peptide list from a <see cref="XmlReader"/> positioned at the
        /// start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide group</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadPeptideGroupXml(XmlReader reader, XmlReadContext context)
        {
            ProteinMetadata proteinMetadata = ReadProteinMetadataXML(reader, true); // read label_name and label_description
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);

            PeptideGroup group = new PeptideGroup(isDecoy);

            Annotations annotations = Annotations.EMPTY;
            PeptideDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, context);

                if (!reader.IsStartElement(EL.selected_peptides))
                    children = ReadPeptideListXml(reader, context, group);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    children = ReadPeptideListXml(reader, context, group);
                    reader.ReadEndElement();
                }

                reader.ReadEndElement();    // peptide_list
            }

            return new PeptideGroupDocNode(group, annotations, proteinMetadata,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <returns>A new array of <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode[] ReadPeptideListXml(XmlReader reader, XmlReadContext context, PeptideGroup group)
        {
            var list = new List<PeptideDocNode>();
            while (reader.IsStartElement(EL.molecule) || reader.IsStartElement(EL.peptide))
            {
                 list.Add(ReadPeptideXml(reader, context, group, reader.IsStartElement(EL.molecule)));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserialize any explictly set CE, DT, etc information from attributes
        /// </summary>
        private ExplicitTransitionGroupValues ReadExplicitTransitionValuesAttributes(XmlReader reader)
        {
            double? importedCollisionEnergy = reader.GetNullableDoubleAttribute(ATTR.explicit_collision_energy);
            double? importedDriftTimeMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_msec);
            double? importedDriftTimeHighEnergyOffsetMsec = reader.GetNullableDoubleAttribute(ATTR.explicit_drift_time_high_energy_offset_msec);
            double? importedSLens = reader.GetNullableDoubleAttribute(FormatVersion < FORMAT_VERSION_3_52 ? ATTR.s_lens_obsolete : ATTR.explicit_s_lens);
            double? importedConeVoltage = reader.GetNullableDoubleAttribute(FormatVersion < FORMAT_VERSION_3_52 ? ATTR.cone_voltage_obsolete : ATTR.explicit_cone_voltage);
            double? importedCompensationVoltage = reader.GetNullableDoubleAttribute(ATTR.explicit_compensation_voltage);
            double? importedDeclusteringPotential = reader.GetNullableDoubleAttribute(ATTR.explicit_declustering_potential);
            return new ExplicitTransitionGroupValues(importedCollisionEnergy, importedDriftTimeMsec, importedDriftTimeHighEnergyOffsetMsec, importedSLens, importedConeVoltage, 
                importedDeclusteringPotential, importedCompensationVoltage);
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide or molecule</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="isCustomMolecule">if true, we're reading a custom molecule, not a peptide</param>
        /// <returns>A new <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode ReadPeptideXml(XmlReader reader, XmlReadContext context, PeptideGroup group, bool isCustomMolecule)
        {
            int? start = reader.GetNullableIntAttribute(ATTR.start);
            int? end = reader.GetNullableIntAttribute(ATTR.end);
            string sequence = reader.GetAttribute(ATTR.sequence);
            string lookupSequence = reader.GetAttribute(ATTR.lookup_sequence);
            // If the group has no sequence, then this is a v0.1 peptide list or a custom ion
            if (group.Sequence == null)
            {
                // Ignore the start and end values
                start = null;
                end = null;
            }
            int missedCleavages = reader.GetIntAttribute(ATTR.num_missed_cleavages);
            // CONSIDER: Trusted value
            int? rank = reader.GetNullableIntAttribute(ATTR.rank);
            double? concentrationMultiplier = reader.GetNullableDoubleAttribute(ATTR.concentration_multiplier);
            double? internalStandardConcentration =
                reader.GetNullableDoubleAttribute(ATTR.internal_standard_concentration);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);
            string standardType = reader.GetAttribute(ATTR.standard_type);
            double? importedRetentionTimeValue = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time);
            double? importedRetentionTimeWindow = reader.GetNullableDoubleAttribute(ATTR.explicit_retention_time_window);
            var importedRetentionTime = importedRetentionTimeValue.HasValue
                ? new ExplicitRetentionTimeInfo(importedRetentionTimeValue.Value, importedRetentionTimeWindow)
                : null;
            var annotations = Annotations.EMPTY;
            ExplicitMods mods = null, lookupMods = null;
            Results<PeptideChromInfo> results = null;
            TransitionGroupDocNode[] children = null;
            var customIon = isCustomMolecule ? DocNodeCustomIon.Deserialize(reader) : null; // This Deserialize only reads attribures, doesn't advance the reader
            var peptide = isCustomMolecule ? 
                new Peptide(customIon) : 
                new Peptide(group as FastaSequence, sequence, start, end, missedCleavages, isDecoy);

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                if(reader.IsStartElement())
                    annotations = ReadAnnotations(reader, context);
                if (!isCustomMolecule)
                {
                    mods = ReadExplicitMods(reader, peptide);
                    SkipImplicitModsElement(reader);
                    lookupMods = ReadLookupMods(reader, lookupSequence);
                }
                results = ReadPeptideResults(reader, context);

                if (reader.IsStartElement(EL.precursor))
                {
                    children = ReadTransitionGroupListXml(reader, context, peptide, mods, customIon);
                }
                else if (reader.IsStartElement(EL.selected_transitions))
                {
                    // Support for v0.1
                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                    {
                        reader.ReadStartElement(EL.selected_transitions);
                        children = ReadUngroupedTransitionListXml(reader, context, peptide, mods);
                        reader.ReadEndElement();
                    }
                }

                reader.ReadEndElement();
            }

            ModifiedSequenceMods sourceKey = null;
            if (lookupSequence != null)
                sourceKey = new ModifiedSequenceMods(lookupSequence, lookupMods);

            PeptideDocNode peptideDocNode = new PeptideDocNode(peptide, Settings, mods, sourceKey, standardType, rank,
                importedRetentionTime, annotations, results, children ?? new TransitionGroupDocNode[0], autoManageChildren);
            peptideDocNode = peptideDocNode
                .ChangeConcentrationMultiplier(concentrationMultiplier)
                .ChangeInternalStandardConcentration(internalStandardConcentration);
            return peptideDocNode;
        }

        private ExplicitMods ReadLookupMods(XmlReader reader, string lookupSequence)
        {
            if (!reader.IsStartElement(EL.lookup_modifications))
                return null;
            reader.Read();
            string sequence = FastaSequence.StripModifications(lookupSequence);
            var mods = ReadExplicitMods(reader, new Peptide(sequence));
            reader.ReadEndElement();
            return mods;
        }

        private void SkipImplicitModsElement(XmlReader reader)
        {
            if (!reader.IsStartElement(EL.implicit_modifications)) 
                return;
            reader.Skip();
        }

        private ExplicitMods ReadExplicitMods(XmlReader reader, Peptide peptide)
        {
            IList<ExplicitMod> staticMods = null;
            TypedExplicitModifications staticTypedMods = null;
            IList<TypedExplicitModifications> listHeavyMods = null;
            bool isVariable = false;

            if (reader.IsStartElement(EL.variable_modifications))
            {
                staticTypedMods = ReadExplicitMods(reader, EL.variable_modifications,
                    EL.variable_modification, peptide, IsotopeLabelType.light);
                staticMods = staticTypedMods.Modifications;
                isVariable = true;
            }
            if (reader.IsStartElement(EL.explicit_modifications))
            {
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();

                    if (!isVariable)
                    {
                        if (reader.IsStartElement(EL.explicit_static_modifications))
                        {
                            staticTypedMods = ReadExplicitMods(reader, EL.explicit_static_modifications,
                                EL.explicit_modification, peptide, IsotopeLabelType.light);
                            staticMods = staticTypedMods.Modifications;
                        }
                        // For format version 0.2 and earlier it was not possible
                        // to have unmodified types.  The absence of a type simply
                        // meant it had no modifications.
                        else if (FormatVersion <= FORMAT_VERSION_0_2)
                        {
                            staticTypedMods = new TypedExplicitModifications(peptide,
                                IsotopeLabelType.light, new ExplicitMod[0]);
                            staticMods = staticTypedMods.Modifications;
                        }
                    }
                    listHeavyMods = new List<TypedExplicitModifications>();
                    while (reader.IsStartElement(EL.explicit_heavy_modifications))
                    {
                        var heavyMods = ReadExplicitMods(reader, EL.explicit_heavy_modifications,
                            EL.explicit_modification, peptide, IsotopeLabelType.heavy);
                        heavyMods = heavyMods.AddModMasses(staticTypedMods);
                        listHeavyMods.Add(heavyMods);
                    }
                    if (FormatVersion <= FORMAT_VERSION_0_2 && listHeavyMods.Count == 0)
                    {
                        listHeavyMods.Add(new TypedExplicitModifications(peptide,
                            IsotopeLabelType.heavy, new ExplicitMod[0]));
                    }
                    reader.ReadEndElement();
                }
            }
            if (staticMods == null && listHeavyMods == null)
                return null;

            listHeavyMods = (listHeavyMods != null ?
                listHeavyMods.ToArray() : new TypedExplicitModifications[0]);

            return new ExplicitMods(peptide, staticMods, listHeavyMods, isVariable);
        }

        private TypedExplicitModifications ReadExplicitMods(XmlReader reader, string name,
            string nameElMod, Peptide peptide, IsotopeLabelType labelTypeDefault)
        {
            if (!reader.IsStartElement(name))
                return new TypedExplicitModifications(peptide, labelTypeDefault, new ExplicitMod[0]);

            var typedMods = ReadLabelType(reader, labelTypeDefault);
            var listMods = new List<ExplicitMod>();

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement(nameElMod))
                {
                    int indexAA = reader.GetIntAttribute(ATTR.index_aa);
                    string nameMod = reader.GetAttribute(ATTR.modification_name);
                    int indexMod = typedMods.Modifications.IndexOf(mod => Equals(nameMod, mod.Name));
                    if (indexMod == -1)
                        throw new InvalidDataException(string.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document, nameMod));
                    StaticMod modAdd = typedMods.Modifications[indexMod];
                    listMods.Add(new ExplicitMod(indexAA, modAdd));
                    // Consume tag
                    reader.Read();
                }
                reader.ReadEndElement();                
            }
            return new TypedExplicitModifications(peptide, typedMods.LabelType, listMods.ToArray());
        }

        private Results<PeptideChromInfo> ReadPeptideResults(XmlReader reader, XmlReadContext context)
        {
            if (reader.IsStartElement(EL.peptide_results))
                return ReadResults(reader, context, Settings, EL.peptide_result, ReadPeptideChromInfo);
            return null;
        }

        private static PeptideChromInfo ReadPeptideChromInfo(XmlReader reader, XmlReadContext context,
            SrmSettings settings, ChromFileInfoId fileId)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            return new PeptideChromInfo(fileId, peakCountRatio, retentionTime, ImmutableList<PeptideLabelRatio>.EMPTY);
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionGroupDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="peptide">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="customIon">Custom ion to use for reading older formats</param>
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(XmlReader reader, XmlReadContext context, Peptide peptide, ExplicitMods mods, DocNodeCustomIon customIon)
        {
            var list = new List<TransitionGroupDocNode>();
            while (reader.IsStartElement(EL.precursor))
                list.Add(ReadTransitionGroupXml(reader, context, peptide, mods, customIon));
            return list.ToArray();
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XmlReader reader, XmlReadContext context, Peptide peptide, ExplicitMods mods, DocNodeCustomIon customIon)
        {
            int precursorCharge = reader.GetIntAttribute(ATTR.charge);
            var typedMods = ReadLabelType(reader, IsotopeLabelType.light);

            int? decoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            var explicitTransitionGroupValues = ReadExplicitTransitionValuesAttributes(reader);
            if (peptide.IsCustomIon)
            {
                // In small molecules, different labels and charges mean different ion formulas
                var ionFormula = reader.GetAttribute(ATTR.ion_formula);
                if (!string.IsNullOrEmpty(ionFormula))
                {
                    var ionName = reader.GetAttribute(ATTR.custom_ion_name);
                    customIon = new DocNodeCustomIon(ionFormula, ionName);
                }
                else
                {
                    var mz = reader.GetDoubleAttribute(ATTR.precursor_mz); // Normally ignored, but needed for molecules that are declared by mz and charge only
                    var mass = BioMassCalc.CalculateIonMassFromMz(mz, precursorCharge); // We can't actually tell mono from average in this case
                    double massMono = reader.GetNullableDoubleAttribute(ATTR.mass_monoisotopic) ?? mass;
                    double massAverage = reader.GetNullableDoubleAttribute(ATTR.mass_average) ?? mass;
                    if (FormatVersion < FORMAT_VERSION_3_12)
                    {
                        // In Skyline 3.1 we didn't suppport more than one transition group per molecule
                        // Passed-in customIon is the primary precursor
                    }
                    // We need to determine if this is the primary precursor transition - but all we have to go on is mass
                    else if (Math.Round(massMono, SequenceMassCalc.MassPrecision) == Math.Round(customIon.MonoisotopicMass, SequenceMassCalc.MassPrecision) &&
                        Math.Round(massAverage, SequenceMassCalc.MassPrecision) == Math.Round(customIon.AverageMass, SequenceMassCalc.MassPrecision))
                    {
                        // Passed-in customIon is the primary precursor
                    }
                    else
                    {
                        customIon = new DocNodeCustomIon(massMono, massAverage, reader.GetAttribute(ATTR.custom_ion_name));
                    }
                }
            }
            var group = new TransitionGroup(peptide, customIon, precursorCharge, typedMods.LabelType, false, decoyMassShift);
            var children = new TransitionDocNode[0];    // Empty until proven otherwise
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            if (reader.IsEmptyElement)
            {
                reader.Read();

                return new TransitionGroupDocNode(group,
                                                  Annotations.EMPTY,
                                                  Settings,
                                                  mods,
                                                  null,
                                                  explicitTransitionGroupValues,
                                                  null,
                                                  children,
                                                  autoManageChildren);
            }
            else
            {
                reader.ReadStartElement();
                var annotations = ReadAnnotations(reader, context);
                var libInfo = ReadTransitionGroupLibInfo(reader, context);
                var results = ReadTransitionGroupResults(reader, context);

                var nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  explicitTransitionGroupValues,
                                                  results,
                                                  children,
                                                  autoManageChildren);
                children = ReadTransitionListXml(reader, context, group, mods, nodeGroup.IsotopeDist);

                reader.ReadEndElement();

                return (TransitionGroupDocNode) nodeGroup.ChangeChildrenChecked(children);
            }
        }

        private TypedModifications ReadLabelType(XmlReader reader, IsotopeLabelType labelTypeDefault)
        {
            string typeName = reader.GetAttribute(ATTR.isotope_label);
            if (string.IsNullOrEmpty(typeName))
                typeName = labelTypeDefault.Name;
            var typedMods = Settings.PeptideSettings.Modifications.GetModificationsByName(typeName);
            if (typedMods == null)
                throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings, typeName));
            return typedMods;
        }

        private static SpectrumHeaderInfo ReadTransitionGroupLibInfo(XmlReader reader, XmlReadContext context)
        {
            // Look for an appropriate deserialization helper for spectrum
            // header info on the current tag.
            var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
            var helper = reader.FindHelper(helpers);
            if (helper != null)
            {
                var libInfo = helper.Deserialize(reader);
                return libInfo.ChangeLibraryName(context.GetNonDuplicatedName(libInfo.LibraryName));
            }

            return null;
        }

        private Results<TransitionGroupChromInfo> ReadTransitionGroupResults(XmlReader reader, XmlReadContext context)
        {
            if (reader.IsStartElement(EL.precursor_results))
                return ReadResults(reader, context, Settings, EL.precursor_peak, ReadTransitionGroupChromInfo);
            return null;
        }

        private static TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader, XmlReadContext context,
            SrmSettings settings, ChromFileInfoId fileId)
        {
            int optimizationStep = reader.GetIntAttribute(ATTR.step);
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? startTime = reader.GetNullableFloatAttribute(ATTR.start_time);
            float? endTime = reader.GetNullableFloatAttribute(ATTR.end_time);
            float? fwhm = reader.GetNullableFloatAttribute(ATTR.fwhm);
            float? area = reader.GetNullableFloatAttribute(ATTR.area);
            float? backgroundArea = reader.GetNullableFloatAttribute(ATTR.background);
            float? height = reader.GetNullableFloatAttribute(ATTR.height);
            float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
            int? truncated = reader.GetNullableIntAttribute(ATTR.truncated);            
            PeakIdentification identified = reader.GetEnumAttribute(ATTR.identified,
                PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
            float? libraryDotProduct = reader.GetNullableFloatAttribute(ATTR.library_dotp);
            float? isotopeDotProduct = reader.GetNullableFloatAttribute(ATTR.isotope_dotp);
            var annotations = Annotations.EMPTY;
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader, context);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const UserSet userSet = UserSet.FALSE;
            int countRatios = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
            return new TransitionGroupChromInfo(fileId,
                                                optimizationStep,
                                                peakCountRatio,
                                                retentionTime,
                                                startTime,
                                                endTime,
                                                fwhm,
                                                area, null, null, // Ms1 and Fragment values calculated later
                                                backgroundArea, null, null, // Ms1 and Fragment values calculated later
                                                height,
                                                TransitionGroupChromInfo.GetEmptyRatios(countRatios),
                                                massError,
                                                truncated,
                                                identified,
                                                libraryDotProduct,
                                                isotopeDotProduct,
                                                annotations,
                                                userSet);
        }

        /// <summary>
        /// Deserializes ungrouped transitions in v0.1 format from a <see cref="XmlReader"/>
        /// into an array of <see cref="TransitionGroupDocNode"/> objects with
        /// children <see cref="TransitionDocNode"/> from the XML correctly distributed.
        /// 
        /// There were no "heavy" transitions in v0.1, making this a matter of
        /// distributing multiple precursor charge states, though in most cases
        /// there will be only one.
        /// </summary>
        /// <param name="reader">The reader positioned on a &lt;transition&gt; start tag</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="peptide">A previously read <see cref="Peptide"/> instance</param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <returns>An array of <see cref="TransitionGroupDocNode"/> instances for
        ///         inclusion in a <see cref="PeptideDocNode"/> child list</returns>
        private TransitionGroupDocNode[] ReadUngroupedTransitionListXml(XmlReader reader, XmlReadContext context, Peptide peptide, ExplicitMods mods)
        {
            TransitionInfo info = new TransitionInfo();
            TransitionGroup curGroup = null;
            List<TransitionDocNode> curList = null;
            var listGroups = new List<TransitionGroup>();
            var mapGroupToList = new Dictionary<TransitionGroup, List<TransitionDocNode>>();
            while (reader.IsStartElement(EL.transition))
            {
                // Read a transition tag.
                info.ReadXml(reader, context, Settings);

                // If the transition is not in the current group
                if (curGroup == null || curGroup.PrecursorCharge != info.PrecursorCharge)
                {
                    // Look for an existing group that matches
                    curGroup = null;
                    foreach (TransitionGroup group in listGroups)
                    {
                        if (group.PrecursorCharge == info.PrecursorCharge)
                        {
                            curGroup = group;
                            break;
                        }
                    }
                    if (curGroup != null)
                        curList = mapGroupToList[curGroup];
                    else
                    {
                        // No existing group matches, so create a new one
                        curGroup = new TransitionGroup(peptide, null, info.PrecursorCharge, IsotopeLabelType.light);
                        curList = new List<TransitionDocNode>();
                        listGroups.Add(curGroup);
                        mapGroupToList.Add(curGroup, curList);
                    }
                }
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, peptide.Length);
                Transition transition = new Transition(curGroup, info.IonType,
                    offset, info.MassIndex, info.Charge);

                // No heavy transition support in v0.1, and no full-scan filtering
                double massH = Settings.GetFragmentMass(IsotopeLabelType.light, mods, transition, null);

                curList.Add(new TransitionDocNode(transition, info.Losses, massH, null, null));
            }

            // Use collected information to create the DocNodes.
            var list = new List<TransitionGroupDocNode>();
            foreach (TransitionGroup group in listGroups)
            {
                list.Add(new TransitionGroupDocNode(group, Annotations.EMPTY,
                    Settings, mods, null, ExplicitTransitionGroupValues.EMPTY, null, mapGroupToList[group].ToArray(), true));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionDocNode"/> objects from
        /// a <see cref="TransitionDocNode"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, XmlReadContext context,
            TransitionGroup group, ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            var list = new List<TransitionDocNode>();
            while (reader.IsStartElement(EL.transition))
                list.Add(ReadTransitionXml(reader, context, group, mods, isotopeDist));
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="TransitionDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a transition</param>
        /// <param name="context">Context object to store values passed to children but only used during this read</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XmlReader reader, XmlReadContext context, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            TransitionInfo info = new TransitionInfo();

            // Read all the XML attributes before the reader advances through the elements
            info.ReadXmlAttributes(reader, Settings);
            var isPrecursor = Transition.IsPrecursor(info.IonType);
            var isCustom = Transition.IsCustom(info.IonType, group);
            CustomIon customIon = null;
            if (isCustom)
            {
                if (info.MeasuredIon != null)
                    customIon = info.MeasuredIon.CustomIon;
                else if (isPrecursor)
                    customIon = group.CustomIon;
                else
                    customIon = DocNodeCustomIon.Deserialize(reader);
            }
            info.ReadXmlElements(reader, context, Settings);

            Transition transition;
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorCharge : info.Charge, info.MassIndex, 
                    customIon, info.IonType);
            }
            else if (isPrecursor)
            {
                transition = new Transition(group, info.IonType, group.Peptide.Length - 1, info.MassIndex,
                    group.PrecursorCharge, info.DecoyMassShift);
            }
            else 
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.MassIndex, info.Charge, info.DecoyMassShift);
            }

            var losses = info.Losses;
            double massH = Settings.GetFragmentMass(group.LabelType, mods, transition, isotopeDist);

            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(transition, losses, isotopeDist);

            if (group.DecoyMassShift.HasValue && !info.DecoyMassShift.HasValue)
                throw new InvalidDataException(Resources.SrmDocument_ReadTransitionXml_All_transitions_of_decoy_precursors_must_have_a_decoy_mass_shift);

            return new TransitionDocNode(transition, info.Annotations, losses,
                massH, isotopeDistInfo, info.LibInfo, info.Results);
        }

        /// <summary>
        /// Reads annotations without ensuring that they use a single unique key string. This
        /// is currently only used for <see cref="ChromatogramSet"/>, because it is difficult to
        /// get it to use the version with a non-null context and the possible level of repetition
        /// is much smaller than with the document nodes and results objects.
        /// </summary>
        public static Annotations ReadAnnotations(XmlReader reader)
        {
            return ReadAnnotations(reader, null);
        }

        private static Annotations ReadAnnotations(XmlReader reader, XmlReadContext context)
        {
            string note = null;
            int color = 0;
            var annotations = new Dictionary<string, string>();
            
            if (reader.IsStartElement(EL.note))
            {
                color = reader.GetIntAttribute(ATTR.category);
                note = reader.ReadElementString();
            }
            while (reader.IsStartElement(EL.annotation))
            {
                string name = reader.GetAttribute(ATTR.name);
                if (name == null)
                    throw new InvalidDataException(Resources.SrmDocument_ReadAnnotations_Annotation_found_without_name);
                if (context != null)
                    name = context.GetNonDuplicatedName(name);
                annotations[name] = reader.ReadElementString();
            }

            return note != null || annotations.Count > 0
                ? new Annotations(note, annotations, color)
                : Annotations.EMPTY;
        }

        /// <summary>
        /// Helper class for reading information from a transition element into
        /// memory for use in both <see cref="Transition"/> and <see cref="TransitionGroup"/>.
        /// 
        /// This class exists to share code between <see cref="ReadTransitionXml"/>
        /// and <see cref="ReadUngroupedTransitionListXml"/>.
        /// </summary>
        private class TransitionInfo
        {
            public IonType IonType { get; private set; }
            public int Ordinal { get; private set; }
            public int MassIndex { get; private set; }
            public int PrecursorCharge { get; private set; }
            public int Charge { get; private set; }
            public int? DecoyMassShift { get; private set; }
            public TransitionLosses Losses { get; private set; }
            public Annotations Annotations { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public Results<TransitionChromInfo> Results { get; private set; }
            public MeasuredIon MeasuredIon { get; private set; }

            public void ReadXml(XmlReader reader, XmlReadContext context, SrmSettings settings)
            {
                ReadXmlAttributes(reader, settings);
                ReadXmlElements(reader, context, settings);
            }

            public void ReadXmlAttributes(XmlReader reader, SrmSettings settings)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, XmlUtil.EnumCase.lower);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = reader.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorCharge = reader.GetIntAttribute(ATTR.precursor_charge);
                Charge = reader.GetIntAttribute(ATTR.product_charge);
                DecoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
                string measuredIonName = reader.GetAttribute(ATTR.measured_ion_name);
                if (measuredIonName != null)
                {
                    MeasuredIon = settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                        i => i.Name.Equals(measuredIonName));
                    if (MeasuredIon == null)
                        throw new InvalidDataException(string.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, measuredIonName));
                    IonType = IonType.custom;
                }
            }

            public void ReadXmlElements(XmlReader reader, XmlReadContext context, SrmSettings settings)
            {
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                }
                else
                {
                    reader.ReadStartElement();
                    Annotations = ReadAnnotations(reader, context); // This is reliably first in all versions
                    while (true)
                    {  // The order of these elements may depend on the version of the file being read
                        if (reader.IsStartElement(EL.losses))
                            Losses = ReadTransitionLosses(reader, settings);
                        else if (reader.IsStartElement(EL.transition_lib_info))
                            LibInfo = ReadTransitionLibInfo(reader);
                        else if (reader.IsStartElement(EL.transition_results))
                            Results = ReadTransitionResults(reader, context, settings);
                        // Read and discard informational elements.  These values are always
                        // calculated from the settings to ensure consistency.
                        else if (reader.IsStartElement(EL.precursor_mz))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.product_mz))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.collision_energy))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.declustering_potential))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.start_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else if (reader.IsStartElement(EL.stop_rt))
                            reader.ReadElementContentAsDoubleInvariant();
                        else
                            break;
                    }
                    reader.ReadEndElement();
                }
            }

            private static TransitionLosses ReadTransitionLosses(XmlReader reader, SrmSettings settings)
            {
                if (reader.IsStartElement(EL.losses))
                {
                    var staticMods = settings.PeptideSettings.Modifications.StaticModifications;
                    MassType massType = settings.TransitionSettings.Prediction.FragmentMassType;

                    reader.ReadStartElement();
                    var listLosses = new List<TransitionLoss>();
                    while (reader.IsStartElement(EL.neutral_loss))
                    {
                        string nameMod = reader.GetAttribute(ATTR.modification_name);
                        if (string.IsNullOrEmpty(nameMod))
                            listLosses.Add(new TransitionLoss(null, FragmentLoss.Deserialize(reader), massType));
                        else
                        {
                            int indexLoss = reader.GetIntAttribute(ATTR.loss_index);
                            int indexMod = staticMods.IndexOf(mod => Equals(nameMod, mod.Name));
                            if (indexMod == -1)
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.TransitionInfo_ReadTransitionLosses_No_modification_named__0__was_found_in_this_document,
                                                  nameMod));
                            }
                            StaticMod modLoss = staticMods[indexMod];
                            if (!modLoss.HasLoss || indexLoss >= modLoss.Losses.Count)
                            {
                                throw new InvalidDataException(
                                    string.Format(Resources.TransitionInfo_ReadTransitionLosses_Invalid_loss_index__0__for_modification__1__,
                                                  indexLoss, nameMod));
                            }
                            listLosses.Add(new TransitionLoss(modLoss, modLoss.Losses[indexLoss], massType));
                        }
                        reader.Read();
                    }
                    reader.ReadEndElement();

                    return new TransitionLosses(listLosses, massType);
                }
                return null;
            }

            private static TransitionLibInfo ReadTransitionLibInfo(XmlReader reader)
            {
                if (reader.IsStartElement(EL.transition_lib_info))
                {
                    var libInfo = new TransitionLibInfo(reader.GetIntAttribute(ATTR.rank),
                        reader.GetFloatAttribute(ATTR.intensity));
                    reader.ReadStartElement();
                    return libInfo;
                }
                return null;
            }

            private static Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader, XmlReadContext context, SrmSettings settings)
            {
                if (reader.IsStartElement(EL.transition_results))
                    return ReadResults(reader, context, settings, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private static TransitionChromInfo ReadTransitionPeak(XmlReader reader, XmlReadContext context,
                SrmSettings settings, ChromFileInfoId fileId)
            {
                int optimizationStep = reader.GetIntAttribute(ATTR.step);
                float? massError = reader.GetNullableFloatAttribute(ATTR.mass_error_ppm);
                float retentionTime = reader.GetFloatAttribute(ATTR.retention_time);
                float startRetentionTime = reader.GetFloatAttribute(ATTR.start_time);
                float endRetentionTime = reader.GetFloatAttribute(ATTR.end_time);
                // Protect against negative areas, since they can cause real problems
                // for ratio calculations.
                float area = Math.Max(0, reader.GetFloatAttribute(ATTR.area));
                float backgroundArea = Math.Max(0, reader.GetFloatAttribute(ATTR.background));
                float height = reader.GetFloatAttribute(ATTR.height);
                float fwhm = reader.GetFloatAttribute(ATTR.fwhm);
                // Strange issue where fwhm got set to NaN
                if (float.IsNaN(fwhm))
                    fwhm = 0;
                bool fwhmDegenerate = reader.GetBoolAttribute(ATTR.fwhm_degenerate);
                bool? truncated = reader.GetNullableBoolAttribute(ATTR.truncated);
                var identified = reader.GetEnumAttribute(ATTR.identified,
                    PeakIdentification.FALSE, XmlUtil.EnumCase.upper);
                UserSet userSet = reader.GetEnumAttribute(ATTR.user_set, UserSet.FALSE, XmlUtil.EnumCase.upper);
                var annotations = Annotations.EMPTY;
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = ReadAnnotations(reader, context);
                }
                int countRatios = settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count;
                return new TransitionChromInfo(fileId,
                                               optimizationStep,
                                               massError,
                                               retentionTime,
                                               startRetentionTime,
                                               endRetentionTime,
                                               area,
                                               backgroundArea,
                                               height,
                                               fwhm,
                                               fwhmDegenerate,
                                               truncated,
                                               identified,
                                               TransitionChromInfo.GetEmptyRatios(countRatios),
                                               annotations,
                                               userSet);
            }
        }

        private static Results<TItem> ReadResults<TItem>(XmlReader reader, XmlReadContext context, SrmSettings settings, string start,
                Func<XmlReader, XmlReadContext, SrmSettings, ChromFileInfoId, TItem> readInfo)
            where TItem : ChromInfo
        {
            // If the results element is empty, then there are no results to read.
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return null;
            }

            MeasuredResults results = settings.MeasuredResults;
            if (results == null)
                throw new InvalidDataException(Resources.SrmDocument_ReadResults_No_results_information_found_in_the_document_settings);

            reader.ReadStartElement();
            var arrayListChromInfos = new List<TItem>[results.Chromatograms.Count];
            ChromatogramSet chromatogramSet = null;
            int index = -1;
            while (reader.IsStartElement(start))
            {
                string name = reader.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!results.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadResults_No_replicate_named__0__found_in_measured_results, name));
                }
                string fileId = reader.GetAttribute(ATTR.file);
                var fileInfoId = (fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId);
                if (fileInfoId == null)
                    throw new InvalidDataException(string.Format(Resources.SrmDocument_ReadResults_No_file_with_id__0__found_in_the_replicate__1__, fileId, name));

                TItem chromInfo = readInfo(reader, context, settings, fileInfoId);
                // Consume the tag
                reader.Read();

                if (!ReferenceEquals(chromInfo, default(TItem)))
                {
                    if (arrayListChromInfos[index] == null)
                        arrayListChromInfos[index] = new List<TItem>();
                    // Deal with cache corruption issue where the same results info could
                    // get written multiple times for the same precursor.
                    var listChromInfos = arrayListChromInfos[index];
                    if (listChromInfos.Count == 0 || !Equals(chromInfo, listChromInfos[listChromInfos.Count - 1]))
                        arrayListChromInfos[index].Add(chromInfo);
                }
            }
            reader.ReadEndElement();

            var arrayChromInfoLists = new ChromInfoList<TItem>[arrayListChromInfos.Length];
            for (int i = 0; i < arrayListChromInfos.Length; i++)
            {
                if (arrayListChromInfos[i] != null)
                    arrayChromInfoLists[i] = new ChromInfoList<TItem>(arrayListChromInfos[i]);
            }
            return new Results<TItem>(arrayChromInfoLists);
        }

        /// <summary>
        /// Serializes a tree of document objects to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.format_version, FORMAT_VERSION);
            writer.WriteAttribute(ATTR.software_version, Install.ProgramNameAndVersion);

            writer.WriteElement(Settings);
            foreach (PeptideGroupDocNode nodeGroup in Children)
            {
                if (nodeGroup.Id is FastaSequence)
                    writer.WriteStartElement(EL.protein);
                else
                    writer.WriteStartElement(EL.peptide_list);
                WritePeptideGroupXml(writer, nodeGroup);
                writer.WriteEndElement();
            }
        }


        private void WriteProteinMetadataXML(XmlWriter writer, ProteinMetadata proteinMetadata, bool skipNameAndDescription) // Not L10N
        {
            if (!skipNameAndDescription)
            {
                writer.WriteAttributeIfString(ATTR.name, proteinMetadata.Name); 
                writer.WriteAttributeIfString(ATTR.description, proteinMetadata.Description); 
            }
            writer.WriteAttributeIfString(ATTR.accession, proteinMetadata.Accession);
            writer.WriteAttributeIfString(ATTR.gene, proteinMetadata.Gene);
            writer.WriteAttributeIfString(ATTR.species, proteinMetadata.Species);
            writer.WriteAttributeIfString(ATTR.preferred_name, proteinMetadata.PreferredName);
            writer.WriteAttributeIfString(ATTR.websearch_status, proteinMetadata.WebSearchInfo.ToString());
        }


        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide group document node</param>
        private void WritePeptideGroupXml(XmlWriter writer, PeptideGroupDocNode node)
        {
            // save the identity info
            if (node.PeptideGroup.Name != null)
            {
                writer.WriteAttributeString(ATTR.name, node.PeptideGroup.Name); 
            }
            if (node.PeptideGroup.Description != null)
            {
                writer.WriteAttributeString(ATTR.description, node.PeptideGroup.Description); 
            }
            // save any overrides
            if ((node.ProteinMetadataOverrides.Name != null) && !Equals(node.ProteinMetadataOverrides.Name, node.PeptideGroup.Name))
            {
                writer.WriteAttributeString(ATTR.label_name, node.ProteinMetadataOverrides.Name); 
            }
            if ((node.ProteinMetadataOverrides.Description != null) && !Equals(node.ProteinMetadataOverrides.Description, node.PeptideGroup.Description))
            {
                writer.WriteAttributeString(ATTR.label_description, node.ProteinMetadataOverrides.Description); 
            }
            WriteProteinMetadataXML(writer, node.ProteinMetadataOverrides, true); // write the protein metadata, skipping the name and description we already wrote
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            writer.WriteAttribute(ATTR.decoy, node.IsDecoy);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);

            FastaSequence seq = node.PeptideGroup as FastaSequence;
            if (seq != null)
            {
                if (seq.Alternatives.Count > 0)
                {
                    writer.WriteStartElement(EL.alternatives);
                    foreach (ProteinMetadata alt in seq.Alternatives)
                    {
                        writer.WriteStartElement(EL.alternative_protein);
                        WriteProteinMetadataXML(writer, alt, false); // don't skip name and description
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteStartElement(EL.sequence);
                writer.WriteString(FormatProteinSequence(seq.Sequence));
                writer.WriteEndElement();
            }

            foreach (PeptideDocNode nodePeptide in node.Children)
            {
                WritePeptideXml(writer, nodePeptide);
            }
        }

        /// <summary>
        /// Formats a FASTA sequence string for output as XML element content.
        /// </summary>
        /// <param name="sequence">An unformated FASTA sequence string</param>
        /// <returns>A formatted version of the input sequence</returns>
        private static string FormatProteinSequence(string sequence)
        {
            const string lineSeparator = "\r\n        "; // Not L10N

            StringBuilder sb = new StringBuilder();
            if (sequence.Length > 50)
                sb.Append(lineSeparator);
            for (int i = 0; i < sequence.Length; i += 10)
            {
                if (sequence.Length - i <= 10)
                    sb.Append(sequence.Substring(i));
                else
                {
                    sb.Append(sequence.Substring(i, Math.Min(10, sequence.Length - i)));
                    sb.Append(i % 50 == 40 ? "\r\n        " : " "); // Not L10N
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes any optionally explicitly specified CE, RT and DT information to attributes only
        /// </summary>
        private void WriteExplicitTransitionGroupValuesAttributes(XmlWriter writer, ExplicitTransitionGroupValues importedAttributes)
        {
            writer.WriteAttributeNullable(ATTR.explicit_collision_energy, importedAttributes.CollisionEnergy);
            writer.WriteAttributeNullable(ATTR.explicit_drift_time_msec, importedAttributes.DriftTimeMsec);
            writer.WriteAttributeNullable(ATTR.explicit_drift_time_high_energy_offset_msec, importedAttributes.DriftTimeHighEnergyOffsetMsec);
            writer.WriteAttributeNullable(ATTR.explicit_s_lens, importedAttributes.SLens);
            writer.WriteAttributeNullable(ATTR.explicit_cone_voltage, importedAttributes.ConeVoltage);
            writer.WriteAttributeNullable(ATTR.explicit_declustering_potential, importedAttributes.DeclusteringPotential);
            writer.WriteAttributeNullable(ATTR.explicit_compensation_voltage, importedAttributes.CompensationVoltage);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide (or small molecule) document node</param>
        private void WritePeptideXml(XmlWriter writer, PeptideDocNode node)
        {
            var peptide = node.Peptide;
            var isCustomIon = peptide.IsCustomIon;

            writer.WriteStartElement(isCustomIon ? EL.molecule : EL.peptide);
            if (node.ExplicitRetentionTime != null)
            {
                writer.WriteAttribute(ATTR.explicit_retention_time, node.ExplicitRetentionTime.RetentionTime);
                writer.WriteAttributeNullable(ATTR.explicit_retention_time_window, node.ExplicitRetentionTime.RetentionTimeWindow);
            }
            double? scoreCalc = null;

            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            if (node.GlobalStandardType != null)
                writer.WriteAttribute(ATTR.standard_type, node.GlobalStandardType);

            writer.WriteAttributeNullable(ATTR.rank, node.Rank);
            writer.WriteAttributeNullable(ATTR.concentration_multiplier, node.ConcentrationMultiplier);
            writer.WriteAttributeNullable(ATTR.internal_standard_concentration, node.InternalStandardConcentration);

            if (isCustomIon)
            {
                peptide.CustomIon.WriteXml(writer);                
            }
            else
            {
                string sequence = peptide.Sequence;
                writer.WriteAttributeString(ATTR.sequence, sequence);
                string modSeq = Settings.GetModifiedSequence(node);
                writer.WriteAttributeString(ATTR.modified_sequence, modSeq);
                if (node.SourceKey != null)
                    writer.WriteAttributeString(ATTR.lookup_sequence, node.SourceKey.ModifiedSequence);
                if (peptide.Begin.HasValue && peptide.End.HasValue)
                {
                    writer.WriteAttribute(ATTR.start, peptide.Begin.Value);
                    writer.WriteAttribute(ATTR.end, peptide.End.Value);
                    writer.WriteAttribute(ATTR.prev_aa, peptide.PrevAA);
                    writer.WriteAttribute(ATTR.next_aa, peptide.NextAA);
                }
                double massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(sequence);
                writer.WriteAttribute(ATTR.calc_neutral_pep_mass,
                    SequenceMassCalc.PersistentNeutral(massH));

                writer.WriteAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
                writer.WriteAttribute(ATTR.decoy, node.IsDecoy);
                var rtPredictor = Settings.PeptideSettings.Prediction.RetentionTime;
                if(rtPredictor != null)
                {
                    scoreCalc = rtPredictor.Calculator.ScoreSequence(modSeq);
                    if (scoreCalc.HasValue)
                    {
                        writer.WriteAttributeNullable(ATTR.rt_calculator_score, scoreCalc);
                        writer.WriteAttributeNullable(ATTR.predicted_retention_time,
                            rtPredictor.GetRetentionTime(scoreCalc.Value));
                    } 
                }
            }
            
            writer.WriteAttributeNullable(ATTR.avg_measured_retention_time, node.AverageMeasuredRetentionTime);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            if (!isCustomIon)
            {
                WriteExplicitMods(writer, node.Peptide.Sequence, node.ExplicitMods);
                WriteImplicitMods(writer, node);
                WriteLookupMods(writer, node);
            }
            if (node.HasResults)
            {
                WriteResults(writer, Settings, node.Results,
                    EL.peptide_results, EL.peptide_result, (w, i) => WritePeptideChromInfo(w, i, scoreCalc));
            }

            foreach (TransitionGroupDocNode nodeGroup in node.Children)
            {
                writer.WriteStartElement(EL.precursor);
                WriteTransitionGroupXml(writer, node, nodeGroup);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WriteLookupMods(XmlWriter writer, PeptideDocNode node)
        {
            if (node.SourceKey == null || node.SourceKey.ExplicitMods == null)
                return;
            writer.WriteStartElement(EL.lookup_modifications);
            WriteExplicitMods(writer, node.SourceKey.Sequence, node.SourceKey.ExplicitMods);
            writer.WriteEndElement();
        }

        private void WriteExplicitMods(XmlWriter writer, string sequence,  ExplicitMods mods)
        {
            if (mods == null)
                return;
            if (mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.variable_modifications,
                    EL.variable_modification, null, mods.StaticModifications, sequence);

                // If no heavy modifications, then don't write an <explicit_modifications> tag
                if (!mods.HasHeavyModifications)
                    return;
            }
            writer.WriteStartElement(EL.explicit_modifications);
            if (!mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.explicit_static_modifications,
                    EL.explicit_modification, null, mods.StaticModifications, sequence);
            }
            foreach (var heavyMods in mods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                WriteExplicitMods(writer, EL.explicit_heavy_modifications,
                    EL.explicit_modification, labelType, heavyMods.Modifications, sequence);
            }
            writer.WriteEndElement();
        }

		private void WriteImplicitMods(XmlWriter writer, PeptideDocNode node)
        {
            // Get the implicit  modifications on this peptide.
            var implicitMods = new ExplicitMods(node,
                Settings.PeptideSettings.Modifications.StaticModifications, 
                Properties.Settings.Default.StaticModList,
                Settings.PeptideSettings.Modifications.GetHeavyModifications(), 
                Properties.Settings.Default.HeavyModList,
                true);

            bool hasStaticMods = implicitMods.StaticModifications.Count != 0 && node.CanHaveImplicitStaticMods;
            bool hasHeavyMods = implicitMods.HasHeavyModifications &&
                                Settings.PeptideSettings.Modifications.GetHeavyModifications().Any(
                                     mod => node.CanHaveImplicitHeavyMods(mod.LabelType));

            if (!hasStaticMods && !hasHeavyMods)
            {
                return;
            }

            writer.WriteStartElement(EL.implicit_modifications);
            
            // implicit static modifications.
            if (hasStaticMods)
            {
                WriteExplicitMods(writer, EL.implicit_static_modifications,
                        EL.implicit_modification, null, implicitMods.StaticModifications, 
                        node.Peptide.Sequence);
            }

            // implicit heavy modifications
            foreach (var heavyMods in implicitMods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (!node.CanHaveImplicitHeavyMods(labelType))
                {
                    continue;
                }
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                WriteExplicitMods(writer, EL.implicit_heavy_modifications,
                                  EL.implicit_modification, labelType, heavyMods.Modifications,
                                  node.Peptide.Sequence);
            }
            writer.WriteEndElement();
        }


        private void WriteExplicitMods(XmlWriter writer, string name,
            string nameElMod, IsotopeLabelType labelType, IEnumerable<ExplicitMod> mods, 
            string sequence)
        {
            if (mods == null)
                return;
            writer.WriteStartElement(name);
            if (labelType != null)
                writer.WriteAttribute(ATTR.isotope_label, labelType);

            SequenceMassCalc massCalc = Settings.TransitionSettings.Prediction.PrecursorMassType == MassType.Monoisotopic ?
                SrmSettings.MonoisotopicMassCalc : SrmSettings.AverageMassCalc;
            foreach (ExplicitMod mod in mods)
            {
                writer.WriteStartElement(nameElMod);
                writer.WriteAttribute(ATTR.index_aa, mod.IndexAA);
                writer.WriteAttribute(ATTR.modification_name, mod.Modification.Name);

                double massDiff = massCalc.GetModMass(sequence[mod.IndexAA], mod.Modification);

                writer.WriteAttribute(ATTR.mass_diff,
                                      string.Format("{0}{1}", (massDiff < 0 ? string.Empty : "+"), Math.Round(massDiff, 1))); // Not L10N

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private void WritePeptideChromInfo(XmlWriter writer, PeptideChromInfo chromInfo, double? scoreCalc)
        {
            writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            if (scoreCalc.HasValue)
            {
                double? rt = Settings.PeptideSettings.Prediction.RetentionTime.GetRetentionTime(scoreCalc.Value,
                                                                                      chromInfo.FileId);
                writer.WriteAttributeNullable(ATTR.predicted_retention_time, rt);
            } 
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="nodePep">The parent peptide document node</param>
        /// <param name="node">The transition group document node</param>
        private void WriteTransitionGroupXml(XmlWriter writer, PeptideDocNode nodePep, TransitionGroupDocNode node)
        {
            TransitionGroup group = node.TransitionGroup;
            var isCustomIon = nodePep.Peptide.IsCustomIon;
            writer.WriteAttribute(ATTR.charge, group.PrecursorCharge);
            if (!group.LabelType.IsLight)
                writer.WriteAttribute(ATTR.isotope_label, group.LabelType);
            if (!isCustomIon)
            {
                writer.WriteAttribute(ATTR.calc_neutral_mass, node.GetPrecursorIonPersistentNeutralMass());
            }
            writer.WriteAttribute(ATTR.precursor_mz, SequenceMassCalc.PersistentMZ(node.PrecursorMz));
            WriteExplicitTransitionGroupValuesAttributes(writer, node.ExplicitValues);

            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            writer.WriteAttributeNullable(ATTR.decoy_mass_shift, group.DecoyMassShift);


            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            double regressionMz = Settings.GetRegressionMz(nodePep, node);
            var ce = predict.CollisionEnergy.GetCollisionEnergy(node.TransitionGroup.PrecursorCharge, regressionMz);
            writer.WriteAttribute(ATTR.collision_energy, ce);

            var dpRegression = predict.DeclusteringPotential;
            if (dpRegression != null)
            {
                var dp = dpRegression.GetDeclustringPotential(regressionMz);
                writer.WriteAttribute(ATTR.declustering_potential, dp);
            }
            
            if (!isCustomIon)
            {
                // modified sequence
                var calcPre = Settings.GetPrecursorCalc(node.TransitionGroup.LabelType, nodePep.ExplicitMods);
                string seq = node.TransitionGroup.Peptide.Sequence;
                writer.WriteAttribute(ATTR.modified_sequence, calcPre.GetModifiedSequence(seq, true));
            }
            else
            {
                // Custom ion
                node.CustomIon.WriteXml(writer);
            }
            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            if (node.HasLibInfo)
            {
                var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
                writer.WriteElements(new[] {node.LibInfo}, helpers);
            }

            if (node.HasResults)
            {
                WriteResults(writer, Settings, node.Results,
                    EL.precursor_results, EL.precursor_peak, WriteTransitionGroupChromInfo);
            }

            foreach (TransitionDocNode nodeTransition in node.Children)
            {
                writer.WriteStartElement(EL.transition);
                WriteTransitionXml(writer, nodePep, node, nodeTransition);
                writer.WriteEndElement();
            }
        }

        private static void WriteTransitionGroupChromInfo(XmlWriter writer, TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);
            writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            writer.WriteAttributeNullable(ATTR.start_time, chromInfo.StartRetentionTime);
            writer.WriteAttributeNullable(ATTR.end_time, chromInfo.EndRetentionTime);
            writer.WriteAttributeNullable(ATTR.fwhm, chromInfo.Fwhm);
            writer.WriteAttributeNullable(ATTR.area, chromInfo.Area);
            writer.WriteAttributeNullable(ATTR.background, chromInfo.BackgroundArea);
            writer.WriteAttributeNullable(ATTR.height, chromInfo.Height);
            writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
            writer.WriteAttributeNullable(ATTR.truncated, chromInfo.Truncated);
            writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
            writer.WriteAttributeNullable(ATTR.library_dotp, chromInfo.LibraryDotProduct);
            writer.WriteAttributeNullable(ATTR.isotope_dotp, chromInfo.IsotopeDotProduct);
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="nodePep">The transition group's parent peptide node</param>
        /// <param name="nodeGroup">The transition node's parent group node</param>
        /// <param name="nodeTransition">The transition document node</param>
        private void WriteTransitionXml(XmlWriter writer, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, 
                                        TransitionDocNode nodeTransition)
        {
            Transition transition = nodeTransition.Transition;
            writer.WriteAttribute(ATTR.fragment_type, transition.IonType);
            if (transition.IsCustom())
            {
                if (transition.CustomIon is DocNodeCustomIon)
                {
                    transition.CustomIon.WriteXml(writer);
                }
                else
                {
                    writer.WriteAttributeString(ATTR.measured_ion_name, transition.CustomIon.Name);
                }
            }
            writer.WriteAttributeNullable(ATTR.decoy_mass_shift, transition.DecoyMassShift);
            // NOTE: MassIndex is the peak index in the isotopic distribution of the precursor.
            //       0 for monoisotopic peaks and for non "precursor" ion types.
            if (transition.MassIndex != 0)
                writer.WriteAttribute(ATTR.mass_index, transition.MassIndex);
            if (nodeTransition.HasDistInfo)
            {
                writer.WriteAttribute(ATTR.isotope_dist_rank, nodeTransition.IsotopeDistInfo.Rank);
                writer.WriteAttribute(ATTR.isotope_dist_proportion, nodeTransition.IsotopeDistInfo.Proportion);
            }
            if (!transition.IsPrecursor())
            {
                if (!transition.IsCustom())
                {
                    writer.WriteAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                    writer.WriteAttribute(ATTR.calc_neutral_mass, nodeTransition.GetIonPersistentNeutralMass());
                }
                writer.WriteAttribute(ATTR.product_charge, transition.Charge);
                if (!transition.IsCustom())
                {
                    writer.WriteAttribute(ATTR.cleavage_aa, transition.AA.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttribute(ATTR.loss_neutral_mass, nodeTransition.LostMass); //po
                }
            }

            // Order of elements matters for XSD validation
            WriteAnnotations(writer, nodeTransition.Annotations);
            writer.WriteElementString(EL.precursor_mz, SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz));
            writer.WriteElementString(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz));

            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            var optimizationMethod = predict.OptimizedMethodType;
            double? ce = null;
            double? dp = null;
            var lib = predict.OptimizedLibrary;
            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.collision_energy,
                    Settings.GetSourceTextId(nodePep), nodeGroup.PrecursorCharge,
                    nodeTransition.FragmentIonName, nodeTransition.Transition.Charge);
                if (optimization != null)
                {
                    ce = optimization.Value;
                }
            }

            double regressionMz = Settings.GetRegressionMz(nodePep, nodeGroup);
            var ceRegression = predict.CollisionEnergy;
            var dpRegression = predict.DeclusteringPotential;
            if (optimizationMethod == OptimizedMethodType.None)
            {
                if (ceRegression != null && !ce.HasValue)
                {
                    ce = ceRegression.GetCollisionEnergy(nodeGroup.PrecursorCharge, regressionMz);
                }
                if (dpRegression != null)
                {
                    dp = dpRegression.GetDeclustringPotential(regressionMz);
                }
            }
            else
            {
                if (!ce.HasValue)
                {
                    ce = OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                    nodePep, nodeGroup, nodeTransition, optimizationMethod, ceRegression, GetCollisionEnergy);
                }

                dp = OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, optimizationMethod, dpRegression, GetDeclusteringPotential);
            }

            if (nodeGroup.ExplicitValues.CollisionEnergy.HasValue)
                ce = nodeGroup.ExplicitValues.CollisionEnergy; // Explicitly imported, overrides any calculation

            if (ce.HasValue)
            {
                writer.WriteElementString(EL.collision_energy, ce.Value);
            }

            if (dp.HasValue)
            {
                writer.WriteElementString(EL.declustering_potential, dp.Value);
            }
            WriteTransitionLosses(writer, nodeTransition.Losses);

            if (nodeTransition.HasLibInfo)
            {
                writer.WriteStartElement(EL.transition_lib_info);
                writer.WriteAttribute(ATTR.rank, nodeTransition.LibInfo.Rank);
                writer.WriteAttribute(ATTR.intensity, nodeTransition.LibInfo.Intensity);
                writer.WriteEndElement();
            }

            if (nodeTransition.HasResults)
            {
                WriteResults(writer, Settings, nodeTransition.Results,
                    EL.transition_results, EL.transition_peak, WriteTransitionChromInfo);
            }

            var progressWriter = writer as XmlWriterWithProgress;
            if (progressWriter != null)
                progressWriter.WroteTransition();
        }

        public double GetCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, int step)
        {
            return GetCollisionEnergy(Settings, nodePep, nodeGroup,
                                      Settings.TransitionSettings.Prediction.CollisionEnergy, step);
        }

        private static double GetCollisionEnergy(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CollisionEnergyRegression regression, int step)
        {
            var ce = nodeGroup.ExplicitValues.CollisionEnergy;
            if (regression != null)
            {
                if (!ce.HasValue)
                {
                    var charge = nodeGroup.TransitionGroup.PrecursorCharge;
                    var mz = settings.GetRegressionMz(nodePep, nodeGroup);
                    ce = regression.GetCollisionEnergy(charge, mz);
                }
                return ce.Value + regression.StepSize * step;
            }
            return ce ?? 0.0;
        }

        public double? GetOptimizedCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            if (nodeGroup.ExplicitValues.CollisionEnergy.HasValue)
                return nodeGroup.ExplicitValues.CollisionEnergy.Value;   // Use the explicitly imported CE value

            var prediction = Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var lib = prediction.OptimizedLibrary;
            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.collision_energy,
                    Settings.GetSourceTextId(nodePep), nodeGroup.PrecursorCharge,
                    nodeTransition.FragmentIonName, nodeTransition.Transition.Charge);
                if (optimization != null)
                {
                    return optimization.Value;
                }
            }

            if (prediction.OptimizedMethodType != OptimizedMethodType.None)
            {
                var regression = prediction.CollisionEnergy;
                return OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                    nodePep, nodeGroup, nodeTransition, methodType, regression, GetCollisionEnergy);
            }

            return null;
        }

        public double GetDeclusteringPotential(PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, int step)
        {
            return GetDeclusteringPotential(Settings, nodePep, nodeGroup,
                                            Settings.TransitionSettings.Prediction.DeclusteringPotential, step);
        }

        private static double GetDeclusteringPotential(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, DeclusteringPotentialRegression regression, int step)
        {
            if (regression == null)
                return 0;
            double mz = settings.GetRegressionMz(nodePep, nodeGroup);
            return regression.GetDeclustringPotential(mz) + regression.StepSize * step;
        }

        public double GetOptimizedDeclusteringPotential(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            if (nodeGroup.ExplicitValues.DeclusteringPotential.HasValue)
                return nodeGroup.ExplicitValues.DeclusteringPotential.Value;   // Use the explicitly imported value
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

            var optLib = Settings.HasOptimizationLibrary
                ? Settings.TransitionSettings.Prediction.OptimizedLibrary
                : null;
            var optType = CompensationVoltageParameters.GetOptimizationType(tuneLevel);

            foreach (var seq in MoleculeGroups.Where(seq => seq.TransitionCount > 0))
            {
                foreach (PeptideDocNode nodePep in seq.Children)
                {
                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children.Where(nodeGroup => ((TransitionGroupDocNode)nodeGroup).Children.Any()))
                    {
                        double? cov;

                        if (optLib != null)
                        {
                            // Check if the optimization library has a value
                            var optimization = optLib.GetOptimization(optType, Settings.GetSourceTextId(nodePep), nodeGroup.PrecursorCharge);
                            if (optimization != null)
                            {
                                break;
                            }
                        }

                        switch (tuneLevel)
                        {
                            case CompensationVoltageParameters.Tuning.fine:
                                cov = OptimizationStep<CompensationVoltageRegressionFine>.FindOptimizedValueFromResults(
                                    Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageFine);
                                break;
                            case CompensationVoltageParameters.Tuning.medium:
                                cov = OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValueFromResults(
                                    Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageMedium);
                                break;
                            default:
                                cov = OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValueFromResults(
                                    Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageRough);
                                break;
                        }
                        if (!cov.HasValue || cov.Value.Equals(0))
                        {
                            yield return nodeGroup.ToString();
                        }
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
                    if (highestTuneLevel < CompensationVoltageParameters.Tuning.fine &&
                        OptimizationType.compensation_voltage_fine.Equals(optType))
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

        public double GetCompensationVoltage(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, int step, CompensationVoltageParameters.Tuning tuneLevel)
        {
            var cov = Settings.TransitionSettings.Prediction.CompensationVoltage;
            switch (tuneLevel)
            {
                case CompensationVoltageParameters.Tuning.fine:
                    return GetCompensationVoltageFine(Settings, nodePep, nodeGroup, cov, step);   
                case CompensationVoltageParameters.Tuning.medium:
                    return GetCompensationVoltageMedium(Settings, nodePep, nodeGroup, cov, step);
                default:
                    return GetCompensationVoltageRough(Settings, nodePep, nodeGroup, cov, step);
            }
        }

        private static double GetCompensationVoltageRough(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            return (regression.MinCov + regression.MaxCov)/2 + regression.StepSizeRough*step;
        }

        private static double GetCompensationVoltageMedium(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            double? covRough = OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValueFromResults(settings,
                nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageRough);
            return covRough.HasValue && covRough.Value > 0 ? covRough.Value + regression.StepSizeMedium*step : 0;
        }

        public static double GetCompensationVoltageFine(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CompensationVoltageParameters regression, int step)
        {
            if (regression == null)
                return 0;

            double? covMedium = OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValueFromResults(settings,
                nodePep, nodeGroup, null, OptimizedMethodType.Precursor, GetCompensationVoltageMedium);
            return covMedium.HasValue && covMedium.Value > 0 ? covMedium.Value + regression.StepSizeFine*step : 0;
        }

        public double GetOptimizedCompensationVoltage(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            if (nodeGroup.ExplicitValues.CompensationVoltage.HasValue)
                return nodeGroup.ExplicitValues.CompensationVoltage.Value;

            var prediction = Settings.TransitionSettings.Prediction;
            var lib = prediction.OptimizedLibrary;

            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.compensation_voltage_fine,
                    Settings.GetSourceTextId(nodePep), nodeGroup.PrecursorCharge);
                if (optimization != null)
                {
                    return optimization.Value;
                }
            }

            var covMain = prediction.CompensationVoltage;
            if (covMain == null)
                return 0;

            double cov;
            if ((cov = OptimizationStep<CompensationVoltageRegressionFine>.FindOptimizedValue(
                Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionFine, GetCompensationVoltageFine)) > 0)
            {
                return cov;
            }
            else if ((cov = OptimizationStep<CompensationVoltageRegressionMedium>.FindOptimizedValue(
                Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionMedium, GetCompensationVoltageMedium)) > 0)
            {
                return cov;
            }
            return OptimizationStep<CompensationVoltageRegressionRough>.FindOptimizedValue(
                Settings, nodePep, nodeGroup, null, OptimizedMethodType.Precursor, covMain.RegressionRough, GetCompensationVoltageRough);
        }

        private static void WriteTransitionLosses(XmlWriter writer, TransitionLosses losses)
        {
            if (losses == null)
                return;
            writer.WriteStartElement(EL.losses);
            foreach (var loss in losses.Losses)
            {
                writer.WriteStartElement(EL.neutral_loss);
                if (loss.PrecursorMod == null)                                                                      
                {
                    // Custom neutral losses are not yet implemented to cause this case
                    // TODO: Implement custome neutral losses, and remove this comment.
                    loss.Loss.WriteXml(writer);
                }
                else
                {
                    writer.WriteAttribute(ATTR.modification_name, loss.PrecursorMod.Name);
                    int indexLoss = loss.LossIndex;
                    if (indexLoss != 0)
                        writer.WriteAttribute(ATTR.loss_index, indexLoss);
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteTransitionChromInfo(XmlWriter writer, TransitionChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);

            // Only write peak information, if it is not empty
            if (!chromInfo.IsEmpty)
            {
                writer.WriteAttributeNullable(ATTR.mass_error_ppm, chromInfo.MassError);
                writer.WriteAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                writer.WriteAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                writer.WriteAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                writer.WriteAttribute(ATTR.area, chromInfo.Area);
                writer.WriteAttribute(ATTR.background, chromInfo.BackgroundArea);
                writer.WriteAttribute(ATTR.height, chromInfo.Height);
                writer.WriteAttribute(ATTR.fwhm, chromInfo.Fwhm);
                writer.WriteAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                writer.WriteAttributeNullable(ATTR.truncated, chromInfo.IsTruncated);
                writer.WriteAttribute(ATTR.identified, chromInfo.Identified.ToString().ToLowerInvariant());
                writer.WriteAttribute(ATTR.rank, chromInfo.Rank);
            }
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        public static void WriteAnnotations(XmlWriter writer, Annotations annotations)
        {
            if (annotations.IsEmpty)
                return;

            if (annotations.Note != null || annotations.ColorIndex > 0)
            {
                if (annotations.ColorIndex == 0)
                    writer.WriteElementString(EL.note, annotations.Note);
                else
                {
                    writer.WriteStartElement(EL.note);
                    writer.WriteAttribute(ATTR.category, annotations.ColorIndex);
                    if (annotations.Note != null)
                    {
                        writer.WriteString(annotations.Note);
                    }
                    writer.WriteEndElement();
                }
            }
            foreach (var entry in annotations.ListAnnotations())
            {
                writer.WriteStartElement(EL.annotation);
                writer.WriteAttribute(ATTR.name, entry.Key);
                writer.WriteString(entry.Value);
                writer.WriteEndElement();
            }
        }

        private static void WriteResults<TItem>(XmlWriter writer, SrmSettings settings,
                IEnumerable<ChromInfoList<TItem>> results, string start, string startChild,
                Action<XmlWriter, TItem> writeChromInfo)
            where TItem : ChromInfo
        {
            bool started = false;
            var enumReplicates = settings.MeasuredResults.Chromatograms.GetEnumerator();
            foreach (var listChromInfo in results)
            {
// ReSharper disable RedundantAssignment
                bool success = enumReplicates.MoveNext();
                Debug.Assert(success);
// ReSharper restore RedundantAssignment
                if (listChromInfo == null)
                    continue;
                var chromatogramSet = enumReplicates.Current;
                if (chromatogramSet == null)
                    continue;
                string name = chromatogramSet.Name;
                foreach (var chromInfo in listChromInfo)
                {
                    if (!started)
                    {
                        writer.WriteStartElement(start);
                        started = true;                        
                    }
                    writer.WriteStartElement(startChild);
                    writer.WriteAttribute(ATTR.replicate, name);
                    if (chromatogramSet.FileCount > 1)
                        writer.WriteAttribute(ATTR.file, chromatogramSet.GetFileSaveId(chromInfo.FileId));
                    writeChromInfo(writer, chromInfo);
                    writer.WriteEndElement();
                }
            }
            if (started)
                writer.WriteEndElement();
        }

        #endregion

        #region object overrides

        public bool Equals(SrmDocument obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && Equals(obj.Settings, Settings);
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
}
