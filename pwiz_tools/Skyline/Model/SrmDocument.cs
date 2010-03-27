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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Results;

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
        public DocumentChangedEventArgs(SrmDocument documentPrevious)
        {
            DocumentPrevious = documentPrevious;
        }

        public SrmDocument DocumentPrevious { get; private set; }
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
    [XmlRoot("srm_settings")]
    public class SrmDocument : DocNodeParent, IXmlSerializable
    {
        /// <summary>
        /// Document extension on disk
        /// </summary>
        public const string EXT = "sky";

        public const double FORMAT_VERSION_0_1 = 0.1;
        public const double FORMAT_VERSION = 0.2;

        public const int MAX_PEPTIDE_COUNT = 50000;
        public const int MAX_TRANSITION_COUNT = 100000;

        // Version of this document in deserialized XML
        private double _formatVersion;

        public SrmDocument(SrmSettings settings)
            : base(new SrmDocumentId(), new PeptideGroupDocNode[0])
        {
            _formatVersion = FORMAT_VERSION;
            Settings = settings;
        }

        public SrmDocument(SrmDocument doc, SrmSettings settings, IList<DocNode> children)
            : base(doc.Id, children)
        {
            _formatVersion = doc._formatVersion;
            RevisionIndex = doc.RevisionIndex + 1;
            Settings = settings;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { 
            get { throw new InvalidOperationException();}
        }

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
        /// Document-wide settings information
        /// </summary>
        public SrmSettings Settings { get; private set; }

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { PeptideGroups, Peptides, TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int PeptideGroupCount { get { return GetCount((int)Level.PeptideGroups); } }
        public int PeptideCount { get { return GetCount((int)Level.Peptides); } }
        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public IEnumerable<PeptideGroupDocNode> PeptideGroups
        {
            get
            {
                foreach (PeptideGroupDocNode node in Children)
                    yield return node;
            }
        }

        public IEnumerable<PeptideDocNode> Peptides
        {
            get
            {
                foreach (PeptideGroupDocNode node in PeptideGroups)
                {
                    foreach (PeptideDocNode nodePep in node.Children)
                        yield return nodePep;
                }
            }
        }

        public IEnumerable<TransitionGroupDocNode> TransitionGroups
        {
            get
            {
                foreach (PeptideDocNode node in Peptides)
                {
                    foreach (TransitionGroupDocNode nodeGroup in node.Children)
                        yield return nodeGroup;
                }
            }
        }

        public IEnumerable<TransitionDocNode> Transitions
        {
            get
            {
                foreach (TransitionGroupDocNode node in TransitionGroups)
                {
                    foreach (TransitionDocNode nodeTran in node.Children)
                        yield return nodeTran;
                }
            }
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

        /// <summary>
        /// Make sure every new copy of a document gets an incremented value
        /// for <see cref="RevisionIndex"/>.
        /// </summary>
        /// <param name="clone">The new copy of the document</param>
        protected override IList<DocNode> OnChangingChildren(DocNodeParent clone)
        {
            SrmDocument docClone = (SrmDocument)clone;
            docClone.RevisionIndex = RevisionIndex + 1;

            // If this document has associated results, update the results
            // for any peptides that have changed.
            if (!Settings.HasResults)
                return docClone.Children;

            // Store indexes to previous results in a dictionary for lookup
            var dictPeptideIdPeptide = new Dictionary<int, PeptideDocNode>();
            foreach (var nodePeptide in Peptides)
                dictPeptideIdPeptide.Add(nodePeptide.Peptide.GlobalIndex, nodePeptide);

            return UpdateResultsSummaries(docClone.PeptideGroups, dictPeptideIdPeptide);
        }

        /// <summary>
        /// Update results for the changed peptides.  This needs to start
        /// at the peptide level, because peptides have useful peak picking information
        /// like predicted retention time, and multiple measured precursors.
        /// </summary>
        private IList<DocNode> UpdateResultsSummaries(IEnumerable<PeptideGroupDocNode> peptideGroups,
            IDictionary<int, PeptideDocNode> dictPeptideIdPeptide)
        {
            var diffResults = new SrmSettingsDiff(Settings, true);
            var listPeptideGroups = new List<DocNode>();
            var listPeptides = new List<DocNode>();
            foreach (var nodeGroup in peptideGroups)
            {
                listPeptides.Clear();
                foreach (PeptideDocNode nodePeptide in nodeGroup.Children)
                {
                    int index = nodePeptide.Peptide.GlobalIndex;

                    PeptideDocNode nodeExisting;
                    if (dictPeptideIdPeptide.TryGetValue(index, out nodeExisting) &&
                        ReferenceEquals(nodeExisting, nodePeptide))
                        listPeptides.Add(nodePeptide);
                    else
                        listPeptides.Add(nodePeptide.ChangeSettings(Settings, diffResults));
                }
                listPeptideGroups.Add(nodeGroup.ChangeChildrenChecked(listPeptides.ToArray()));
            }
            return listPeptideGroups.ToArray();
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value, updating the <see cref="DocNode"/> hierarchy to reflect the change.
        /// </summary>
        /// <param name="settingsNew">New settings value</param>
        /// <returns>A new document revision</returns>
        public SrmDocument ChangeSettings(SrmSettings settingsNew)
        {
            // Preserve measured results.  Call ChangeMeasureResults to change the
            // MeasuredResults property on the SrmSettings.
            if (!ReferenceEquals(Settings.MeasuredResults, settingsNew.MeasuredResults))
                settingsNew = settingsNew.ChangeMeasuredResults(Settings.MeasuredResults);
            return ChangeSettingsInternal(settingsNew);
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
        /// <returns>A new document revision</returns>
        public SrmDocument ChangeMeasuredResults(MeasuredResults results)
        {
            return ChangeSettingsInternal(Settings.ChangeMeasuredResults(results));
        }

        /// <summary>
        /// Creates a cloned instance of the document with a new <see cref="Settings"/>
        /// value.
        /// </summary>
        /// <param name="settingsNew">New settings value</param>
        /// <returns>A new document revision</returns>
        private SrmDocument ChangeSettingsInternal(SrmSettings settingsNew)
        {
            // First figure out what changed.
            SrmSettingsDiff diff = new SrmSettingsDiff(Settings, settingsNew);

            // If there were no changes that require DocNode tree updates
            if (!diff.RequiresDocNodeUpdate)
                return ChangeSettingsNoDiff(settingsNew);
            else
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                // Enumerate the nodes making necessary changes.
                foreach (PeptideGroupDocNode group in Children)
                    childrenNew.Add(group.ChangeSettings(settingsNew, diff));

                // Don't change the children, if the resulting list contains
                // only reference equal children of the same length and in the
                // same order.
                if (ArrayUtil.ReferencesEqual(childrenNew, Children))
                    childrenNew = Children;

                return new SrmDocument(this, settingsNew, childrenNew);
            }
        }

        public SrmDocument ImportFasta(TextReader reader, bool peptideList, IdentityPath to,
                out IdentityPath firstAdded)
        {
            FastaImporter importer = new FastaImporter(this, peptideList);
            return AddPeptideGroups(importer.Import(reader), peptideList, to, out firstAdded);
        }

        public SrmDocument ImportMassList(TextReader reader, IFormatProvider provider, char separator,
            string textSeq, IdentityPath to, out IdentityPath firstAdded)
        {
            MassListImporter importer = new MassListImporter(this, provider, separator);
            return AddPeptideGroups(importer.Import(reader, textSeq), false, to, out firstAdded);
        }

        public SrmDocument AddPeptideGroups(IEnumerable<PeptideGroupDocNode> peptideGroupsNew, bool peptideList, IdentityPath to, out IdentityPath firstAdded)
        {
            var peptideGroupsAdd = peptideGroupsNew.ToArray();

            // If there are no new groups to add, as in the case where already added
            // FASTA sequences are pasted, just return this, and a null path.  Callers
            // must handle this case gracefully, e.g. not adding an undo record.
            if (peptideGroupsAdd.Length == 0)
            {
                firstAdded = null;
                return this;
            }
            firstAdded = new IdentityPath(peptideGroupsAdd[0].Id);

            // Add to the end, if no insert node
            if (to == null || to.Length - 1 < (int)Level.PeptideGroups)
                return (SrmDocument) AddAll(peptideGroupsAdd);
            
            IdentityPath pathGroup = to.GetPathTo((int)Level.PeptideGroups);

            // Precalc depth of last identity in the path
            int last = to.Length - 1;

            // If it is a peptide list, allow pasting to children to existing peptide list.
            if (peptideList && !(to.GetIdentity((int)Level.PeptideGroups) is FastaSequence))
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
                        listAdd.Add(nodePeptide);
                }

                // No modification necessary, if no unique peptides
                if (listAdd.Count == 0)
                {
                    firstAdded = null;
                    return this;
                }

                // If no peptide was in the selection path, add to the end of the list
                DocNode docNew;
                if (last < (int)Level.Peptides)
                    docNew = AddAll(to, listAdd);
                    // If one of the peptides was selected, insert before it
                else if (last == (int)Level.Peptides)
                    docNew = InsertAll(to, listAdd);
                    // Otherise, insert after the peptide of the child that was selected
                else
                    docNew = InsertAll(to.GetPathTo((int)Level.Peptides), listAdd, true);

                // Change the selection path to point to the first peptide pasted.
                firstAdded = new IdentityPath(pathGroup, listAdd[0].Id);
                return (SrmDocument)docNew;
            }
                // Insert the new groups before a selected group
            else if (last == (int)Level.PeptideGroups)
                return (SrmDocument)InsertAll(pathGroup, peptideGroupsAdd);
                // Or after, if a group child is selected
            else
                return (SrmDocument)InsertAll(pathGroup, peptideGroupsAdd, true);
        }

        public bool IsValidMove(IdentityPath from, IdentityPath to)
        {
            int lastFrom = from.Length - 1;
            // Peptide groups can always be moved
            if (lastFrom == (int)Level.PeptideGroups)
                return true;
            // Peptides can be moved, if going from a peptide list to a peptide list
            else if (to != null && lastFrom == (int)Level.Peptides &&
                    !(from.GetIdentity((int)Level.PeptideGroups) is FastaSequence) &&
                    !(to.GetIdentity((int)Level.PeptideGroups) is FastaSequence))
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
            if (lastFrom == (int)Level.PeptideGroups)
            {
                SrmDocument document = (SrmDocument)RemoveChild(nodeFrom);
                // If no good target, append
                if (to == null || lastTo == -1)
                    document = (SrmDocument)document.Add(nodeFrom);
                // If dropped over a group, insert before
                else if (lastTo == (int)Level.PeptideGroups)
                    document = (SrmDocument)document.Insert(to, nodeFrom);
                // If over the child of a group, insert after
                else
                    document = (SrmDocument)document.Insert(to.GetPathTo((int)Level.PeptideGroups), nodeFrom, true);
                newLocation = new IdentityPath(nodeFrom.Id);
                return document;
            }
            // If moving a peptide that comes from a peptide list
            else if (lastFrom == (int)Level.Peptides)
            {
                if (from.GetIdentity((int)Level.PeptideGroups) is FastaSequence)
                    throw new InvalidOperationException("Invalid move source.");
                if (to == null || to.GetIdentity((int)Level.PeptideGroups) is FastaSequence)
                    throw new InvalidOperationException("Invalid move target.");

                SrmDocument document = (SrmDocument)RemoveChild(from.Parent, nodeFrom);
                // If dropped over a group, add to the end
                if (lastTo == (int)Level.PeptideGroups)
                    document = (SrmDocument) document.Add(to, nodeFrom);
                // If over a peptide, insert before
                else if (lastTo == (int)Level.Peptides)
                    document = (SrmDocument) document.Insert(to, nodeFrom);
                // If over the child of a peptide, insert after
                else
                    document = (SrmDocument) document.Insert(to.GetPathTo((int)Level.Peptides), nodeFrom, true);
                newLocation = new IdentityPath(to.GetPathTo((int)Level.PeptideGroups), nodeFrom.Id);
                return document;
            }
            throw new InvalidOperationException("Invalid move source.");
        }

        public SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, string filePath,
            Identity tranId, double retentionTime)
        {
            return ChangePeak(groupPath, nameSet, filePath, false,
                (node, info, tol, iSet, iFile, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, iFile, reg, tranId, retentionTime));
        }

        public SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, string filePath,
            Transition transition, double startTime, double endTime)
        {
            return ChangePeak(groupPath, nameSet, filePath, true,
                (node, info, tol, iSet, iFile, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, iFile, reg, transition, startTime, endTime));
        }

        private delegate DocNode ChangeNodePeak(TransitionGroupDocNode nodeGroup,
            ChromatogramGroupInfo chromInfoGroup, double mzMatchTolerance, int indexSet, int indexFile,
            OptimizableRegression regression);

        private SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, string filePath, bool loadPoints,
            ChangeNodePeak change)
        {
            var nodeGroup = (TransitionGroupDocNode)FindNode(groupPath);
            if (nodeGroup == null)
                throw new IdentityNotFoundException(groupPath.Child);
            // Get the chromatogram set containing the chromatograms of interest
            int indexSet;
            ChromatogramSet chromatograms;
            if (!Settings.HasResults || !Settings.MeasuredResults.TryGetChromatogramSet(nameSet, out chromatograms, out indexSet))
                throw new ArgumentOutOfRangeException(string.Format("No replicate named {0} was found", nameSet));
            // Calculate the file index that supplied the chromatograms
            int indexFile = chromatograms.MSDataFilePaths.IndexOf(filePath);
            if (indexFile == -1)
                throw new ArgumentOutOfRangeException(string.Format("The file {0} was not found in the replicate {1}.", filePath, nameSet));
            // Get all chromatograms for this transition group
            double mzMatchTolerance = Settings.TransitionSettings.Instrument.MzMatchTolerance;
            ChromatogramGroupInfo[] arrayChromInfo;
            if (!Settings.MeasuredResults.TryLoadChromatogram(chromatograms, nodeGroup, (float)mzMatchTolerance, loadPoints, out arrayChromInfo))
                throw new ArgumentOutOfRangeException(string.Format("No results found for the precursor {0} in the replicate {1}", this, nameSet));
            // Get the chromatograms for only the file of interest
            int indexInfo = arrayChromInfo.IndexOf(info => Equals(filePath, info.FilePath));
            if (indexInfo == -1)
                throw new ArgumentOutOfRangeException(string.Format("No results found for the precursor {0} in the file {1}", this, filePath));
            var chromInfoGroup = arrayChromInfo[indexInfo];
            var nodeGroupNew = change(nodeGroup, chromInfoGroup, mzMatchTolerance, indexSet, indexFile,
                chromatograms.OptimizationFunction);
            if (ReferenceEquals(nodeGroup, nodeGroupNew))
                return this;
            return (SrmDocument)ReplaceChild(groupPath.Parent, nodeGroupNew);
        }

        public SrmDocument ChangePeptideMods(IdentityPath peptidePath, ExplicitMods mods,
            IList<StaticMod> listGlobalStaticMods, IList<StaticMod> listGlobalHeavyMods)
        {
            var docResult = this;
            var nodePeptide = (PeptideDocNode)FindNode(peptidePath);
            if (nodePeptide == null)
                throw new IdentityNotFoundException(peptidePath.Child);
            // Make sure modifications are in synch with global values
            if (mods != null)
                mods = mods.ChangeGlobalMods(listGlobalStaticMods, listGlobalHeavyMods);
            // If modifications have changed, update the peptide.
            var modsPep = nodePeptide.ExplicitMods;
            if (!Equals(mods, modsPep))
            {
                // Update the peptide to the new explicit modifications
                SrmSettingsDiff settingsDiff;
                if (mods == null || modsPep == null ||
                    !ArrayUtil.ReferencesEqual(mods.HeavyModifications, modsPep.HeavyModifications))
                    settingsDiff = SrmSettingsDiff.ALL;
                else
                    settingsDiff = SrmSettingsDiff.PROPS;
                // Change the explicit modifications, and force a settings update through the peptide
                // to all of its children.
                // CONSIDER: This is not really the right SrmSettings object to be using for this
                //           update, but constructing the right one currently depends on the
                //           peptide being added to the document.  Doesn't seem like the potential
                //           changes would have any impact on this operation, though.
                nodePeptide = nodePeptide.ChangeExplicitMods(mods).ChangeSettings(Settings, settingsDiff);

                docResult = (SrmDocument) ReplaceChild(peptidePath.Parent, nodePeptide);
            }
            var pepMods = docResult.Settings.PeptideSettings.Modifications;
            var pepModsNew = pepMods.DeclareExplicitMods(docResult, listGlobalStaticMods, listGlobalHeavyMods);
            if (ReferenceEquals(pepModsNew, pepMods))
                return docResult;

            var settings = docResult.Settings.ChangePeptideModifications(m => pepModsNew);
            return docResult.ChangeSettings(settings);
        }

        #region Implementation of IXmlSerializable

        private enum EL
        {
            // v0.1 lists
            selected_proteins,
            selected_peptides,
            selected_transitions,

            protein,
            note,
            annotation,
            alternatives,
            alternative_protein,
            sequence,
            peptide_list,
            peptide,
            explicit_modifications,
            explicit_static_modifications,
            explicit_heavy_modifications,
            explicit_modification,
            peptide_results,
            peptide_result,
            precursor,
            precursor_results,
            precursor_peak,
            transition,
            transition_results,
            transition_peak,
            transition_lib_info,
            precursor_mz,
            product_mz,
            collision_energy,
            declustering_potential,
            start_rt,
            stop_rt
        }

        private enum ATTR
        {
            format_version,
            name,
            description,
            label_name,
            label_description,
            peptide_list,
            start,
            end,
            sequence,
            prev_aa,
            next_aa,
            index_aa,
            modification_name,
            calc_neutral_pep_mass,
            num_missed_cleavages,
            predicted_retention_time,
            isotope_label,
            fragment_type,
            fragment_ordinal,
            calc_neutral_mass,
            charge,
            precursor_charge,   // backware compatibility with v0.1
            product_charge,
            rank,
            intensity,
            // Results
            replicate,
            file,
            step,
            retention_time,
            start_time,
            end_time,
            area,
            background,
            height,
            fwhm,
            fwhm_degenerate,
            user_set,
            peak_count_ratio,
            ratio,
            ratio_stdev,
            library_dotp,
            auto_manage_children,
        }

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

        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            _formatVersion = reader.GetDoubleAttribute(ATTR.format_version);
            if (_formatVersion == 0)
                _formatVersion = FORMAT_VERSION_0_1;
            else if (_formatVersion > FORMAT_VERSION)
                throw new InvalidDataException(string.Format("The document format version {0} is not supported.", _formatVersion));

            reader.ReadStartElement();  // Start document element

            Settings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();

            PeptideGroupDocNode[] children = null;
            if (reader.IsStartElement())
            {
                // Support v0.1 naming
                if (!reader.IsStartElement(EL.selected_proteins))
                    children = ReadPeptideGroupListXml(reader);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    children = ReadPeptideGroupListXml(reader);
                    reader.ReadEndElement();
                }
            }

            reader.ReadEndElement();    // End document element

            if (children == null)
                SetChildren(new PeptideGroupDocNode[0]);
            else
                SetChildren(UpdateResultsSummaries(children, new Dictionary<int, PeptideDocNode>()));
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at the start of the list.
        /// </summary>
        /// <param name="reader">The reader positioned on the element of the first node</param>
        /// <returns>An array of <see cref="PeptideGroupDocNode"/> objects for
        ///         inclusion in a <see cref="SrmDocument"/> child list</returns>
        private PeptideGroupDocNode[] ReadPeptideGroupListXml(XmlReader reader)
        {
            var list = new List<PeptideGroupDocNode>();
            while (reader.IsStartElement(EL.protein) || reader.IsStartElement(EL.peptide_list))
            {
                if (reader.IsStartElement(EL.protein))
                    list.Add(ReadProteinXml(reader));
                else
                    list.Add(ReadPeptideGroupXml(reader));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein&gt; tag.
        /// 
        /// In order to support the v0.1 format, the returned node may represent
        /// either a FASTA sequence or a peptide list.
        /// </summary>
        /// <param name="reader">The reader positioned at a protein tag</param>
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadProteinXml(XmlReader reader)
        {
            string name = reader.GetAttribute(ATTR.name);
            string description = reader.GetAttribute(ATTR.description) ?? "";
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            string label = reader.GetAttribute(ATTR.label_name) ?? "";
            string labelDescription = reader.GetAttribute(ATTR.label_description) ?? "";
            reader.ReadStartElement();

            var annotations = ReadAnnotations(reader);

            AlternativeProtein[] alternatives;
            if (!reader.IsStartElement(EL.alternatives) || reader.IsEmptyElement)
                alternatives = new AlternativeProtein[0];
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
            if (sequence.StartsWith("X") && sequence.EndsWith("X"))
                peptideList = true;

            // All v0.1 peptide lists should have a settable label
            if (peptideList)
            {
                label = name ?? "";
                labelDescription = description;
            }
            // Or any protein without a name attribute
            else if (name != null)
            {
                label = null;
                labelDescription = null;
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
                children = ReadPeptideListXml(reader, group);
            else if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement(EL.selected_peptides);
                children = ReadPeptideListXml(reader, group);
                reader.ReadEndElement();                    
            }

            reader.ReadEndElement();

            return new PeptideGroupDocNode(group, annotations, label, labelDescription,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="AlternativeProtein"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <returns>A new array of <see cref="AlternativeProtein"/></returns>
        private static AlternativeProtein[] ReadAltProteinListXml(XmlReader reader)
        {
            var list = new List<AlternativeProtein>();
            while (reader.IsStartElement(EL.alternative_protein))
            {
                string name = reader.GetAttribute(ATTR.name);
                string description = reader.GetAttribute(ATTR.description);
                reader.Read();
                list.Add(new AlternativeProtein(name, description));
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
        /// <returns>A new <see cref="PeptideGroupDocNode"/></returns>
        private PeptideGroupDocNode ReadPeptideGroupXml(XmlReader reader)
        {
            string name = reader.GetAttribute(ATTR.label_name) ?? "";
            string description = reader.GetAttribute(ATTR.label_description) ?? "";
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            PeptideGroup group = new PeptideGroup();

            Annotations annotations = Annotations.Empty;
            PeptideDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader);

                if (!reader.IsStartElement(EL.selected_peptides))
                    children = ReadPeptideListXml(reader, group);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    children = ReadPeptideListXml(reader, group);
                    reader.ReadEndElement();
                }

                reader.ReadEndElement();    // peptide_list
            }

            return new PeptideGroupDocNode(group, annotations, name, description,
                children ?? new PeptideDocNode[0], autoManageChildren);
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <returns>A new array of <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode[] ReadPeptideListXml(XmlReader reader, PeptideGroup group)
        {
            var list = new List<PeptideDocNode>();
            while (reader.IsStartElement(EL.peptide))
                list.Add(ReadPeptideXml(reader, group));
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a peptide</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <returns>A new <see cref="PeptideDocNode"/></returns>
        private PeptideDocNode ReadPeptideXml(XmlReader reader, PeptideGroup group)
        {
            int? start = reader.GetNullableIntAttribute(ATTR.start);
            int? end = reader.GetNullableIntAttribute(ATTR.end);
            string sequence = reader.GetAttribute(ATTR.sequence);
            // If the group has no sequence, then this is a v0.1 peptide list
            if (group.Sequence == null)
            {
                // Ignore the start and end values
                start = null;
                end = null;
            }
            int missedCleavages = reader.GetIntAttribute(ATTR.num_missed_cleavages);
            // CONSIDER: Trusted value
            int? rank = reader.GetNullableIntAttribute(ATTR.rank);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            Peptide peptide = new Peptide(group as FastaSequence, sequence, start, end, missedCleavages);

            var annotations = Annotations.Empty;
            ExplicitMods mods = null;
            Results<PeptideChromInfo> results = null;
            TransitionGroupDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader);
                mods = ReadExplicitMods(reader, peptide);

                results = ReadPeptideResults(reader);

                if (reader.IsStartElement(EL.precursor))
                {
                    children = ReadTransitionGroupListXml(reader, peptide, mods);
                }
                else if (reader.IsStartElement(EL.selected_transitions))
                {
                    // Support for v0.1
                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                    {
                        reader.ReadStartElement(EL.selected_transitions);
                        children = ReadUngroupedTransitionListXml(reader, peptide, mods);
                        reader.ReadEndElement();
                    }
                }

                reader.ReadEndElement();
            }

            return new PeptideDocNode(peptide, rank, annotations, mods, results,
                children ?? new TransitionGroupDocNode[0], autoManageChildren);
        }

        private ExplicitMods ReadExplicitMods(XmlReader reader, Peptide peptide)
        {
            if (reader.IsStartElement(EL.explicit_modifications))
            {
                IList<ExplicitMod> staticMods;
                IList<ExplicitMod> heavyMods;
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                    staticMods = new ExplicitMod[0];
                    heavyMods = new ExplicitMod[0];
                }
                else
                {
                    reader.ReadStartElement();
                    var modSettings = Settings.PeptideSettings.Modifications;
                    staticMods = ReadExplicitMods(reader, EL.explicit_static_modifications,
                        modSettings.StaticModifications);
                    heavyMods = ReadExplicitMods(reader, EL.explicit_heavy_modifications,
                        modSettings.HeavyModifications);
                    reader.ReadEndElement();
                }
                return new ExplicitMods(peptide, staticMods, heavyMods);
            }
            return null;
        }

        private static ExplicitMod[] ReadExplicitMods(XmlReader reader, Enum name, IList<StaticMod> mods)
        {
            if (!reader.IsStartElement(name))
                return new ExplicitMod[0];
            var listMods = new List<ExplicitMod>();
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement(EL.explicit_modification))
                {
                    int indexAA = reader.GetIntAttribute(ATTR.index_aa);
                    string nameMod = reader.GetAttribute(ATTR.modification_name);
                    int indexMod = mods.IndexOf(mod => Equals(nameMod, mod.Name));
                    if (indexMod == -1)
                        throw new InvalidDataException(string.Format("No modification named {0} was found in this document.", nameMod));
                    StaticMod modAdd = mods[indexMod];
                    // In the document context, all static mods must have the explicit
                    // flag off to behave correctly for equality checks.  Only in the
                    // settings context is the explicit flag necessary to destinguish
                    // between the global implicit modifications and the explicit modifications
                    // which do not apply to everything.
                    if (modAdd.IsExplicit)
                        modAdd = modAdd.ChangeExplicit(false);
                    listMods.Add(new ExplicitMod(indexAA, modAdd));
                    // Consume tag
                    reader.Read();
                }
                reader.ReadEndElement();                
            }
            return listMods.ToArray();
        }

        private Results<PeptideChromInfo> ReadPeptideResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.peptide_results))
                return ReadResults<PeptideChromInfo>(reader, Settings, EL.peptide_result, ReadPeptideChromInfo);
            return null;
        }

        private static PeptideChromInfo ReadPeptideChromInfo(XmlReader reader, int indexFile)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? ratioToStandard = reader.GetNullableFloatAttribute(ATTR.ratio);
            return new PeptideChromInfo(indexFile, peakCountRatio, retentionTime, ratioToStandard);
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionGroupDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="peptide">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <returns>A new array of <see cref="TransitionGroupDocNode"/></returns>
        private TransitionGroupDocNode[] ReadTransitionGroupListXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            var list = new List<TransitionGroupDocNode>();
            while (reader.IsStartElement(EL.precursor))
                list.Add(ReadTransitionGroupXml(reader, peptide, mods));
            return list.ToArray();
        }

        private TransitionGroupDocNode ReadTransitionGroupXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            int precursorCharge = reader.GetIntAttribute(ATTR.charge);
            IsotopeLabelType labelType = reader.GetEnumAttribute(ATTR.isotope_label, IsotopeLabelType.light);

            TransitionGroup group = new TransitionGroup(peptide, precursorCharge, labelType);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            var annotations = Annotations.Empty;
            SpectrumHeaderInfo libInfo = null;
            Results<TransitionGroupChromInfo> results = null;
            TransitionDocNode[] children = null;

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader);
                libInfo = ReadTransitionGroupLibInfo(reader);
                results = ReadTransitionGroupResults(reader);

                children = ReadTransitionListXml(reader, group, mods);

                reader.ReadEndElement();
            }

            double precursorMassH = Settings.GetPrecursorMass(group.LabelType, peptide.Sequence, mods);
            RelativeRT relativeRT = Settings.GetRelativeRT(group.LabelType, peptide.Sequence, mods);
            return new TransitionGroupDocNode(group,
                                              annotations,
                                              precursorMassH,
                                              relativeRT,
                                              libInfo,
                                              results,
                                              children ?? new TransitionDocNode[0],
                                              autoManageChildren);
        }

        private static SpectrumHeaderInfo ReadTransitionGroupLibInfo(XmlReader reader)
        {
            // Look for an appropriate deserialization helper for spectrum
            // header info on the current tag.
            var helpers = PeptideLibraries.SpectrumHeaderXmlHelpers;
            var helper = reader.FindHelper(helpers);
            if (helper != null)
                return helper.Deserialize(reader);

            return null;
        }

        private Results<TransitionGroupChromInfo> ReadTransitionGroupResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.precursor_results))
                return ReadResults<TransitionGroupChromInfo>(reader, Settings, EL.precursor_peak, ReadTransitionGroupChromInfo);
            return null;
        }

        private static TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader, int indexFile)
        {
            int optimizationStep = reader.GetIntAttribute(ATTR.step);
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            float? startTime = reader.GetNullableFloatAttribute(ATTR.start_time);
            float? endTime = reader.GetNullableFloatAttribute(ATTR.end_time);
            float? fwhm = reader.GetNullableFloatAttribute(ATTR.fwhm);
            float? area = reader.GetNullableFloatAttribute(ATTR.area);
            float? backgroundArea = reader.GetNullableFloatAttribute(ATTR.background);
            float? ratio = reader.GetNullableFloatAttribute(ATTR.ratio);
            float? stdev = reader.GetNullableFloatAttribute(ATTR.ratio_stdev);
            float? libraryDotProduct = reader.GetNullableFloatAttribute(ATTR.library_dotp);
            var annotations = Annotations.Empty;
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const bool userSet = false;
            return new TransitionGroupChromInfo(indexFile,
                                                optimizationStep,
                                                peakCountRatio,
                                                retentionTime,
                                                startTime,
                                                endTime,
                                                fwhm,
                                                area,
                                                backgroundArea,
                                                ratio,
                                                stdev,
                                                libraryDotProduct,
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
        /// <param name="peptide">A previously read <see cref="Peptide"/> instance</param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <returns>An array of <see cref="TransitionGroupDocNode"/> instances for
        ///         inclusion in a <see cref="PeptideDocNode"/> child list</returns>
        private TransitionGroupDocNode[] ReadUngroupedTransitionListXml(XmlReader reader, Peptide peptide, ExplicitMods mods)
        {
            TransitionInfo info = new TransitionInfo();
            TransitionGroup curGroup = null;
            List<TransitionDocNode> curList = null;
            var listGroups = new List<TransitionGroup>();
            var mapGroupToList = new Dictionary<TransitionGroup, List<TransitionDocNode>>();
            while (reader.IsStartElement(EL.transition))
            {
                // Read a transition tag.
                info.ReadXml(reader, Settings);

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
                        curGroup = new TransitionGroup(peptide, info.PrecursorCharge, IsotopeLabelType.light);
                        curList = new List<TransitionDocNode>();
                        listGroups.Add(curGroup);
                        mapGroupToList.Add(curGroup, curList);
                    }
                }
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, peptide.Length);
                Transition transition = new Transition(curGroup, info.IonType,
                    offset, info.Charge);

                // No heavy transition support in v0.1
                double massH = Settings.GetFragmentMass(IsotopeLabelType.light, mods, transition);

                curList.Add(new TransitionDocNode(transition, massH, null));
            }

            double precursorMassH = Settings.GetPrecursorMass(IsotopeLabelType.light, peptide.Sequence, mods);
            RelativeRT relativeRT = Settings.GetRelativeRT(IsotopeLabelType.light, peptide.Sequence, mods);

            // Use collected information to create the DocNodes.
            var list = new List<TransitionGroupDocNode>();
            foreach (TransitionGroup group in listGroups)
            {
                list.Add(new TransitionGroupDocNode(group, precursorMassH, relativeRT,
                    mapGroupToList[group].ToArray()));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes an array of <see cref="TransitionDocNode"/> objects from
        /// a <see cref="TransitionDocNode"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit modifications for the peptide</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, TransitionGroup group, ExplicitMods mods)
        {
            var list = new List<TransitionDocNode>();
            while (reader.IsStartElement(EL.transition))
                list.Add(ReadTransitionXml(reader, group, mods));
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="TransitionDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a transition</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XmlReader reader, TransitionGroup group, ExplicitMods mods)
        {
            TransitionInfo info = new TransitionInfo();
            info.ReadXml(reader, Settings);

            Transition transition;
            if (Transition.IsPrecursor(info.IonType))
                transition = new Transition(group);
            else
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.Charge);
            }

            double massH = Settings.GetFragmentMass(group.LabelType, mods, transition);

            return new TransitionDocNode(transition, info.Annotations, massH, info.LibInfo, info.Results);
        }

        private static Annotations ReadAnnotations(XmlReader reader)
        {
            string note = null;
            var annotations = new Dictionary<string, string>();

            if (reader.IsStartElement(EL.note))
                note = reader.ReadElementString();
            while (reader.IsStartElement(EL.annotation))
            {
                annotations[reader.GetAttribute(ATTR.name)] = reader.ReadElementString();
            }
            return new Annotations(note, annotations);
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
            public int PrecursorCharge { get; private set; }
            public int Charge { get; private set; }
            public Annotations Annotations { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public Results<TransitionChromInfo> Results { get; private set; }

            public void ReadXml(XmlReader reader, SrmSettings settings)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, true);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                PrecursorCharge = reader.GetIntAttribute(ATTR.precursor_charge);
                Charge = reader.GetIntAttribute(ATTR.product_charge);

                if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    Annotations = ReadAnnotations(reader);
                    LibInfo = ReadTransitionLibInfo(reader);
                    Results = ReadTransitionResults(reader, settings);

                    // Read an discard informational elements.  These values are always
                    // calculated from the settings to ensure consistency.
                    if (reader.IsStartElement(EL.precursor_mz))
                        reader.ReadElementContentAsDoubleInvariant();
                    if (reader.IsStartElement(EL.product_mz))
                        reader.ReadElementContentAsDoubleInvariant();
                    if (reader.IsStartElement(EL.collision_energy))
                        reader.ReadElementContentAsDoubleInvariant();
                    if (reader.IsStartElement(EL.declustering_potential))
                        reader.ReadElementContentAsDoubleInvariant();
                    if (reader.IsStartElement(EL.start_rt))
                        reader.ReadElementContentAsDoubleInvariant();
                    if (reader.IsStartElement(EL.stop_rt))
                        reader.ReadElementContentAsDoubleInvariant();

                    reader.ReadEndElement();                                    
                }
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

            private static Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader, SrmSettings settings)
            {
                if (reader.IsStartElement(EL.transition_results))
                    return ReadResults<TransitionChromInfo>(reader, settings, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private static TransitionChromInfo ReadTransitionPeak(XmlReader reader, int indexFile)
            {
                int optimizationStep = reader.GetIntAttribute(ATTR.step);
                float retentionTime = reader.GetFloatAttribute(ATTR.retention_time);
                float startRetentionTime = reader.GetFloatAttribute(ATTR.start_time);
                float endRetentionTime = reader.GetFloatAttribute(ATTR.end_time);
                // Protect against negative areas, since they can cause real problems
                // for ratio calculations.
                float area = Math.Max(0, reader.GetFloatAttribute(ATTR.area));
                float backgroundArea = Math.Max(0, reader.GetFloatAttribute(ATTR.background));
                float height = reader.GetFloatAttribute(ATTR.height);
                float fwhm = reader.GetFloatAttribute(ATTR.fwhm);
                bool fwhmDegenerate = reader.GetBoolAttribute(ATTR.fwhm_degenerate);
                float? ratio = reader.GetNullableFloatAttribute(ATTR.ratio);
                // Make sure non-null ratios are not negative
                if (ratio.HasValue)
                    ratio = Math.Max(0, ratio.Value);
                bool userSet = reader.GetBoolAttribute(ATTR.user_set);
                var annotations = Annotations.Empty;
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = ReadAnnotations(reader);
                }
                return new TransitionChromInfo(indexFile, optimizationStep, retentionTime, startRetentionTime, endRetentionTime,
                                               area, backgroundArea, height, fwhm, fwhmDegenerate, ratio, annotations, userSet);
            }
        }

        private static Results<T> ReadResults<T>(XmlReader reader, SrmSettings settings, Enum start,
                Func<XmlReader, int, T> readInfo)
            where T : ChromInfo
        {
            // If the results element is empty, then there are no results to read.
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return null;
            }

            MeasuredResults results = settings.MeasuredResults;
            if (results == null)
                throw new InvalidDataException("No results information found in the document settings");

            reader.ReadStartElement();
            var arrayListChromInfos = new List<T>[results.Chromatograms.Count];
            ChromatogramSet chromatogramSet = null;
            int index = -1;
            while (reader.IsStartElement(start))
            {
                string name = reader.GetAttribute(ATTR.replicate);
                if (chromatogramSet == null || !Equals(name, chromatogramSet.Name))
                {
                    if (!results.TryGetChromatogramSet(name, out chromatogramSet, out index))
                        throw new InvalidDataException(string.Format("No replicate named {0} found in measured results", name));
                }
                string fileId = reader.GetAttribute(ATTR.file);
                int indexFile = (fileId != null ? chromatogramSet.IndexOfId(fileId) : 0);
                if (indexFile == -1)
                    throw new InvalidDataException(string.Format("No file with id {0} found in the replicate {1}", fileId, name));

                T chromInfo = readInfo(reader, indexFile);
                // Consume the tag
                reader.Read();

                if (chromInfo != default(T))
                {
                    if (arrayListChromInfos[index] == null)
                        arrayListChromInfos[index] = new List<T>();
                    arrayListChromInfos[index].Add(chromInfo);                    
                }
            }
            reader.ReadEndElement();

            var arrayChromInfoLists = new ChromInfoList<T>[arrayListChromInfos.Length];
            for (int i = 0; i < arrayListChromInfos.Length; i++)
            {
                if (arrayListChromInfos[i] != null)
                    arrayChromInfoLists[i] = new ChromInfoList<T>(arrayListChromInfos[i]);
            }
            return new Results<T>(arrayChromInfoLists);
        }

        /// <summary>
        /// Serializes a tree of document objects to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.format_version, FORMAT_VERSION);
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

        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide group document node</param>
        private void WritePeptideGroupXml(XmlWriter writer, PeptideGroupDocNode node)
        {
            // If the FASTA sequence has a name, then save it
            if (node.PeptideGroup.Name != null)
            {
                writer.WriteAttributeString(ATTR.name, node.PeptideGroup.Name);
                writer.WriteAttributeIfString(ATTR.description, node.PeptideGroup.Description);
            }
            // Otherwise, save the label set by the user
            else
            {
                writer.WriteAttributeString(ATTR.label_name, node.Name);
                writer.WriteAttributeIfString(ATTR.label_description, node.Description);                
            }
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            // Write child elements
            WriteAnnotations(writer, node.Annotations);

            FastaSequence seq = node.PeptideGroup as FastaSequence;
            if (seq != null)
            {
                if (seq.Alternatives.Count > 0)
                {
                    writer.WriteStartElement(EL.alternatives);
                    foreach (AlternativeProtein alt in seq.Alternatives)
                    {
                        writer.WriteStartElement(EL.alternative_protein);
                        writer.WriteAttributeString(ATTR.name, alt.Name);
                        writer.WriteAttributeString(ATTR.description, alt.Description);
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
                writer.WriteStartElement(EL.peptide);
                WritePeptideXml(writer, nodePeptide);
                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Formats a FASTA sequence string for output as XML element content.
        /// </summary>
        /// <param name="sequence">An unformated FASTA sequence string</param>
        /// <returns>A formatted version of the input sequence</returns>
        private static string FormatProteinSequence(string sequence)
        {
            const string lineSeparator = "\r\n        ";

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
                    sb.Append(i % 50 == 40 ? "\r\n        " : " ");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide document node</param>
        private void WritePeptideXml(XmlWriter writer, PeptideDocNode node)
        {
            Peptide peptide = node.Peptide;
            string sequence = peptide.Sequence;
            if (peptide.Begin.HasValue && peptide.End.HasValue)
            {
                writer.WriteAttribute(ATTR.start, peptide.Begin.Value);
                writer.WriteAttribute(ATTR.end, peptide.End.Value);
                writer.WriteAttributeString(ATTR.sequence, sequence);
                writer.WriteAttribute(ATTR.prev_aa, peptide.PrevAA);
                writer.WriteAttribute(ATTR.next_aa, peptide.NextAA);
            }
            else
            {
                writer.WriteAttributeString(ATTR.sequence, sequence);                
            }
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);

            double massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(sequence);
            writer.WriteAttribute(ATTR.calc_neutral_pep_mass,
                SequenceMassCalc.PersistentNeutral(massH));

            writer.WriteAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
            writer.WriteAttributeNullable(ATTR.rank, node.Rank);

            RetentionTimeRegression regression = Settings.PeptideSettings.Prediction.RetentionTime;
            if (regression != null)
            {
                double retentionTime = regression.GetRetentionTime(sequence);
                writer.WriteAttribute(ATTR.predicted_retention_time, retentionTime);
            }
            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            WriteExplicitMods(writer, node);

            if (node.HasResults)
            {
                WriteResults(writer, Settings, node.Results,
                    EL.peptide_results, EL.peptide_result, WritePeptideChromInfo);
            }

            foreach (TransitionGroupDocNode nodeGroup in node.Children)
            {
                writer.WriteStartElement(EL.precursor);
                WriteTransitionGroupXml(writer, nodeGroup);
                writer.WriteEndElement();
            }
        }

        private static void WriteExplicitMods(XmlWriter writer, PeptideDocNode node)
        {
            if (node.ExplicitMods == null)
                return;
            writer.WriteStartElement(EL.explicit_modifications);
            WriteExplicitMods(writer, EL.explicit_static_modifications, node.ExplicitMods.StaticModifications);
            WriteExplicitMods(writer, EL.explicit_heavy_modifications, node.ExplicitMods.HeavyModifications);
            writer.WriteEndElement();
        }

        private static void WriteExplicitMods(XmlWriter writer, Enum name, ICollection<ExplicitMod> mods)
        {
            if (mods == null || mods.Count == 0)
                return;
            writer.WriteStartElement(name);
            foreach (ExplicitMod mod in mods)
            {
                writer.WriteStartElement(EL.explicit_modification);
                writer.WriteAttribute(ATTR.index_aa, mod.IndexAA);
                writer.WriteAttribute(ATTR.modification_name, mod.Modification.Name);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WritePeptideChromInfo(XmlWriter writer, PeptideChromInfo chromInfo)
        {
            writer.WriteAttribute(ATTR.peak_count_ratio, chromInfo.PeakCountRatio);
            writer.WriteAttributeNullable(ATTR.retention_time, chromInfo.RetentionTime);
            writer.WriteAttributeNullable(ATTR.ratio, chromInfo.RatioToStandard);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The transition group document node</param>
        private void WriteTransitionGroupXml(XmlWriter writer, TransitionGroupDocNode node)
        {
            TransitionGroup group = node.TransitionGroup;
            writer.WriteAttribute(ATTR.charge, group.PrecursorCharge);
            if (group.LabelType != IsotopeLabelType.light)
                writer.WriteAttribute(ATTR.isotope_label, group.LabelType);
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
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
                WriteTransitionXml(writer, node, nodeTransition);
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
            writer.WriteAttributeNullable(ATTR.ratio, chromInfo.Ratio);
            writer.WriteAttributeNullable(ATTR.ratio_stdev, chromInfo.RatioStdev);
            writer.WriteAttributeNullable(ATTR.library_dotp, chromInfo.LibraryDotProduct);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="TransitionDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="nodeGroup">The transition nodes parent group node</param>
        /// <param name="nodeTransition">The transition document node</param>
        private void WriteTransitionXml(XmlWriter writer, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            Transition transition = nodeTransition.Transition;
            writer.WriteAttribute(ATTR.fragment_type, transition.IonType);
            if (!transition.IsPrecursor())
            {
                writer.WriteAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                double massH = SequenceMassCalc.GetMH(nodeTransition.Mz, transition.Charge);
                writer.WriteAttribute(ATTR.calc_neutral_mass,
                    SequenceMassCalc.PersistentNeutral(massH));
                writer.WriteAttribute(ATTR.product_charge, transition.Charge);
            }

            WriteAnnotations(writer, nodeTransition.Annotations);

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

            double precursorMz = nodeGroup.PrecursorMz;
            writer.WriteElementString(EL.precursor_mz, SequenceMassCalc.PersistentMZ(precursorMz));
            writer.WriteElementString(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz));
            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            var ceRegression = predict.CollisionEnergy;
            writer.WriteElementString(EL.collision_energy,
                ceRegression.GetCollisionEnergy(transition.Group.PrecursorCharge, precursorMz));
            var deRegression = predict.DeclusteringPotential;
            if (deRegression != null)
            {
                writer.WriteElementString(EL.declustering_potential,
                    deRegression.GetDeclustringPotential(precursorMz));
            }
        }

        private static void WriteTransitionChromInfo(XmlWriter writer, TransitionChromInfo chromInfo)
        {
            if (chromInfo.OptimizationStep != 0)
                writer.WriteAttribute(ATTR.step, chromInfo.OptimizationStep);

            // Only write peak information, if it is not empty
            if (!chromInfo.IsEmpty)
            {
                writer.WriteAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                writer.WriteAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                writer.WriteAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                writer.WriteAttribute(ATTR.area, chromInfo.Area);
                writer.WriteAttribute(ATTR.background, chromInfo.BackgroundArea);
                writer.WriteAttribute(ATTR.height, chromInfo.Height);
                writer.WriteAttribute(ATTR.fwhm, chromInfo.Fwhm);
                writer.WriteAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                writer.WriteAttribute(ATTR.rank, chromInfo.Rank);
                writer.WriteAttributeNullable(ATTR.ratio, chromInfo.Ratio);                
            }
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        private static void WriteAnnotations(XmlWriter writer, Annotations annotations)
        {
            if (annotations.Note != null)
                writer.WriteElementString(EL.note, annotations.Note);
            foreach (var entry in annotations.ListAnnotations())
            {
                writer.WriteStartElement(EL.annotation);
                writer.WriteAttribute(ATTR.name, entry.Key);
                writer.WriteString(entry.Value);
                writer.WriteEndElement();
            }
        }

        private static void WriteResults<T>(XmlWriter writer, SrmSettings settings,
                IEnumerable<ChromInfoList<T>> results, Enum start, Enum startChild,
                Action<XmlWriter, T> writeChromInfo)
            where T : ChromInfo
        {
            bool started = false;
            var enumReplicates = settings.MeasuredResults.Chromatograms.GetEnumerator();
            foreach (var listChromInfo in results)
            {
                bool success = enumReplicates.MoveNext();
                Debug.Assert(success);
                if (listChromInfo == null)
                    continue;
                var chromatogramSet = enumReplicates.Current;
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
                    if (chromatogramSet.MSDataFilePaths.Count > 1)
                        writer.WriteAttribute(ATTR.file, chromatogramSet.GetFileSaveId(chromInfo.FileIndex));
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
