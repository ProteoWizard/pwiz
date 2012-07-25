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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
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
        public const double FORMAT_VERSION_0_2 = 0.2;
        public const double FORMAT_VERSION_0_8 = 0.8;
        public const double FORMAT_VERSION_1_2 = 1.2;   // Used briefly during development of v1.3
        public const double FORMAT_VERSION_1_3 = 1.3;
        public const double FORMAT_VERSION_1_4 = 1.4;
        public const double FORMAT_VERSION = FORMAT_VERSION_1_4;

        public const int MAX_PEPTIDE_COUNT = 100*1000;
        public const int MAX_TRANSITION_COUNT = 2*MAX_PEPTIDE_COUNT;

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
            Settings = settings;
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
                return Children.Cast<PeptideGroupDocNode>();
            }
        }

        public IEnumerable<PeptideDocNode> Peptides
        {
            get 
            {
                return PeptideGroups.SelectMany(node => node.Children.Cast<PeptideDocNode>());
            }
        }

        public IEnumerable<TransitionGroupDocNode> TransitionGroups
        {
            get 
            {
                return Peptides.SelectMany(node => node.Children.Cast<TransitionGroupDocNode>());
            }
        }

        public IEnumerable<TransitionDocNode> Transitions
        {
            get
            {
                return TransitionGroups.SelectMany(node => node.Children.Cast<TransitionDocNode>());
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

            string baseId = (peptideList ? "peptides" : "sequence");
            int i = 1;
            while (ids.Contains(baseId + i))
                i++;
            return baseId + i;
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

                IList<PeptideGroupDocNode> peptideGroups = docImport.PeptideGroups.ToList();
                if (pasteToPeptideList)
                {
                    PeptideGroupDocNode peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), null, null, new PeptideDocNode[0]);
                    IList<DocNode> peptides = docImport.Peptides.Cast<DocNode>().ToList();
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
                    throw new InvalidDataException(string.Format("The peptide {0} was found multiple times with user modifications.",
                        Settings.GetPrecursorCalc(IsotopeLabelType.light, nodePep.ExplicitMods).GetModifiedSequence(nodePep.Peptide.Sequence, true)));
                }
                dictPeptidesModified.Add(key, nodePep);
            }

            var diff = new SrmSettingsDiff(Settings, true);
            var setMerged = new HashSet<PeptideModKey>();
            var listPeptideGroupsMerged = new List<DocNode>();
            foreach (var nodePepGroup in PeptideGroups)
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
            return ImportFasta(reader, null, -1, peptideList, to, out firstAdded);
        }

        public SrmDocument ImportFasta(TextReader reader, ILongWaitBroker longWaitBroker, long lines, bool peptideList,
                IdentityPath to, out IdentityPath firstAdded)
        {
            FastaImporter importer = new FastaImporter(this, peptideList);
            IdentityPath nextAdd;
            return AddPeptideGroups(importer.Import(reader, longWaitBroker, lines), peptideList,
                to, out firstAdded, out nextAdd);
        }

        public SrmDocument ImportFasta(TextReader reader, ILongWaitBroker longWaitBroker, long lines, 
            ModificationMatcher matcher, IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdded)
        {
            if (matcher == null)
            {
                nextAdded = null;
                return ImportFasta(reader, false, to, out firstAdded);
            }

            FastaImporter importer = new FastaImporter(this, matcher);
            return AddPeptideGroups(importer.Import(reader, longWaitBroker, lines), true, to, out firstAdded, out nextAdded);
        }

        public SrmDocument ImportMassList(TextReader reader, IFormatProvider provider, char separator,
            IdentityPath to, out IdentityPath firstAdded)
        {
            return ImportMassList(reader, null, -1, provider, separator, to, out firstAdded);
        }

        public SrmDocument ImportMassList(TextReader reader, ILongWaitBroker longWaitBroker, long lines,
            IFormatProvider provider, char separator, IdentityPath to, out IdentityPath firstAdded)
        {
            MassListImporter importer = new MassListImporter(this, provider, separator);
            IdentityPath nextAdd;
            return AddPeptideGroups(importer.Import(reader, longWaitBroker, lines), false,
                to, out firstAdded, out nextAdd);
        }

        public SrmDocument AddPeptideGroups(IEnumerable<PeptideGroupDocNode> peptideGroupsNew,
            bool peptideList, IdentityPath to, out IdentityPath firstAdded, out IdentityPath nextAdd)
        {
            // For multiple add operations, make the next addtion at the same location by default
            nextAdd = to;

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
            if (to == null || to.Depth < (int)Level.PeptideGroups)
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
                {
                    nextAdd = FindNextInsertNode(to, (int) Level.Peptides);
                    docNew = InsertAll(to.GetPathTo((int)Level.Peptides), listAdd, true);
                }

                // Change the selection path to point to the first peptide pasted.
                firstAdded = new IdentityPath(pathGroup, listAdd[0].Id);
                return (SrmDocument)docNew;
            }
                // Insert the new groups before a selected group
            else if (last == (int)Level.PeptideGroups)
                return (SrmDocument)InsertAll(pathGroup, peptideGroupsAdd);
                // Or after, if a group child is selected
            else
            {
                nextAdd = FindNextInsertNode(to, (int)Level.PeptideGroups);
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
                (node, info, tol, iSet, fileId, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, fileId, reg, tranId, retentionTime));
        }

        public SrmDocument ChangePeak(IdentityPath groupPath, string nameSet, string filePath,
            Transition transition, double startTime, double endTime, bool identified)
        {
            return ChangePeak(groupPath, nameSet, filePath, true,
                (node, info, tol, iSet, fileId, reg) =>
                    node.ChangePeak(Settings, info, tol, iSet, fileId, reg, transition, startTime, endTime, identified));
        }

        private delegate DocNode ChangeNodePeak(TransitionGroupDocNode nodeGroup,
            ChromatogramGroupInfo chromInfoGroup, double mzMatchTolerance, int indexSet,
            ChromFileInfoId indexFile, OptimizableRegression regression);

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
            ChromFileInfoId fileId = chromatograms.FindFile(filePath);
            if (fileId == null)
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
                                                        nodePeptide.ExplicitMods,
                                                        nodePeptide.Rank,   // Results
                                                        Annotations.EMPTY,
                                                        null, nodePeptide.Children.ToList().ConvertAll(node =>
                                                                                                       (TransitionGroupDocNode)node).ToArray(), nodePeptide.AutoManageChildren);
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
        }
        // ReSharper restore InconsistentNaming

        // ReSharper disable InconsistentNaming
        // Enum.ToString() was too slow for use in the document
        public static class ATTR
        {
            public const string format_version = "format_version";
            public const string name = "name";
            public const string category = "category";
            public const string description = "description";
            public const string label_name = "label_name";
            public const string label_description = "label_description";
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
            public const string avg_measured_retention_time = "avg_measured_retention_time";
            public const string isotope_label = "isotope_label";
            public const string fragment_type = "fragment_type";
            public const string fragment_ordinal = "fragment_ordinal";
            public const string mass_index = "mass_index";
            public const string calc_neutral_mass = "calc_neutral_mass";
            public const string precursor_mz = "precursor_mz";
            public const string charge = "charge";
            public const string precursor_charge = "precursor_charge";   // backware compatibility with v0.1
            public const string product_charge = "product_charge";
            public const string rank = "rank";
            public const string intensity = "intensity";
            public const string auto_manage_children = "auto_manage_children";
            public const string decoy = "decoy";
            public const string decoy_mass_shift = "decoy_mass_shift";
            public const string isotope_dist_rank = "isotope_dist_rank";
            public const string isotope_dist_proportion = "isotope_dist_proportion";
            public const string modified_sequence = "modified_sequence";
            public const string cleavage_aa = "cleavage_aa";
            public const string loss_neutral_mass = "loss_neutral_mass";
            public const string collision_energy = "collision_energy";
            public const string declustering_potential = "declustering_potential";

            // Results
            public const string replicate = "replicate";
            public const string file = "file";
            public const string step = "step";
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
            foreach (PeptideDocNode nodePep in Peptides)
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
                    throw new InvalidDataException("Results found in document with no replicates.");
                return;
            }
            // This check was a little too agressive.
            // If a node's transition count is zero, then it can still have null for results.
//            if (results == null)
//                throw new InvalidDataException("DocNode missing results in document with replicates.");
            if (results != null)
                results.Validate(settings);
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
                throw new InvalidDataException(string.Format("The document format version {0} is not supported.", FormatVersion));

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
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);

            PeptideGroup group = new PeptideGroup(isDecoy);

            Annotations annotations = Annotations.EMPTY;
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
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);

            Peptide peptide = new Peptide(group as FastaSequence, sequence, start, end, missedCleavages, isDecoy);

            var annotations = Annotations.EMPTY;
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
                SkipImplicitModsElement(reader);

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

            return new PeptideDocNode(peptide, mods, rank,
                annotations, results, children ?? new TransitionGroupDocNode[0], autoManageChildren);
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
                        throw new InvalidDataException(string.Format("No modification named {0} was found in this document.", nameMod));
                    StaticMod modAdd = typedMods.Modifications[indexMod];
                    listMods.Add(new ExplicitMod(indexAA, modAdd));
                    // Consume tag
                    reader.Read();
                }
                reader.ReadEndElement();                
            }
            return new TypedExplicitModifications(peptide, typedMods.LabelType, listMods.ToArray());
        }

        private Results<PeptideChromInfo> ReadPeptideResults(XmlReader reader)
        {
            if (reader.IsStartElement(EL.peptide_results))
                return ReadResults(reader, Settings, EL.peptide_result, ReadPeptideChromInfo);
            return null;
        }

        private static PeptideChromInfo ReadPeptideChromInfo(XmlReader reader,
            SrmSettings settings, ChromFileInfoId fileId)
        {
            float peakCountRatio = reader.GetFloatAttribute(ATTR.peak_count_ratio);
            float? retentionTime = reader.GetNullableFloatAttribute(ATTR.retention_time);
            return new PeptideChromInfo(fileId, peakCountRatio, retentionTime, new PeptideLabelRatio[0]);
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

            var typedMods = ReadLabelType(reader, IsotopeLabelType.light);
            int? decoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);
            TransitionGroup group = new TransitionGroup(peptide, precursorCharge, typedMods.LabelType, false, decoyMassShift);
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
                                                  null,
                                                  children,
                                                  autoManageChildren);
            }
            else
            {
                reader.ReadStartElement();
                var annotations = ReadAnnotations(reader);
                var libInfo = ReadTransitionGroupLibInfo(reader);
                var results = ReadTransitionGroupResults(reader);

                var nodeGroup = new TransitionGroupDocNode(group,
                                                  annotations,
                                                  Settings,
                                                  mods,
                                                  libInfo,
                                                  results,
                                                  children,
                                                  autoManageChildren);

                children = ReadTransitionListXml(reader, group, mods, nodeGroup.IsotopeDist);

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
                throw new InvalidDataException(string.Format("The isotope modification type {0} does not exist in the document settings.", typeName));
            return typedMods;
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
                return ReadResults(reader, Settings, EL.precursor_peak, ReadTransitionGroupChromInfo);
            return null;
        }

        private static TransitionGroupChromInfo ReadTransitionGroupChromInfo(XmlReader reader,
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
            int? truncated = reader.GetNullableIntAttribute(ATTR.truncated);
            bool identified = reader.GetBoolAttribute(ATTR.identified);
            float? libraryDotProduct = reader.GetNullableFloatAttribute(ATTR.library_dotp);
            float? isotopeDotProduct = reader.GetNullableFloatAttribute(ATTR.isotope_dotp);
            var annotations = Annotations.EMPTY;
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                annotations = ReadAnnotations(reader);
            }
            // Ignore userSet during load, since all values are still calculated
            // from the child transitions.  Otherwise inconsistency is possible.
//            bool userSet = reader.GetBoolAttribute(ATTR.user_set);
            const bool userSet = false;
            int countRatios = settings.PeptideSettings.Modifications.InternalStandardTypes.Count;
            return new TransitionGroupChromInfo(fileId,
                                                optimizationStep,
                                                peakCountRatio,
                                                retentionTime,
                                                startTime,
                                                endTime,
                                                fwhm,
                                                area,
                                                backgroundArea,
                                                new float?[countRatios],
                                                new float?[countRatios],
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
                    Settings, mods, null, null, mapGroupToList[group].ToArray(), true));
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
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new array of <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode[] ReadTransitionListXml(XmlReader reader, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            var list = new List<TransitionDocNode>();
            while (reader.IsStartElement(EL.transition))
                list.Add(ReadTransitionXml(reader, group, mods, isotopeDist));
            return list.ToArray();
        }

        /// <summary>
        /// Deserializes a single <see cref="TransitionDocNode"/> from a <see cref="XmlReader"/>
        /// positioned at the start element.
        /// </summary>
        /// <param name="reader">The reader positioned at a start element of a transition</param>
        /// <param name="group">A previously read parent <see cref="Identity"/></param>
        /// <param name="mods">Explicit mods for the peptide</param>
        /// <param name="isotopeDist">Isotope peak distribution to use for assigning M+N m/z values</param>
        /// <returns>A new <see cref="TransitionDocNode"/></returns>
        private TransitionDocNode ReadTransitionXml(XmlReader reader, TransitionGroup group,
            ExplicitMods mods, IsotopeDistInfo isotopeDist)
        {
            TransitionInfo info = new TransitionInfo();
            info.ReadXml(reader, Settings);

            Transition transition;
            if (Transition.IsPrecursor(info.IonType))
                transition = new Transition(group, IonType.precursor, group.Peptide.Length - 1, info.MassIndex, group.PrecursorCharge, info.DecoyMassShift);
            else
            {
                int offset = Transition.OrdinalToOffset(info.IonType,
                    info.Ordinal, group.Peptide.Length);
                transition = new Transition(group, info.IonType, offset, info.MassIndex, info.Charge,info.DecoyMassShift);
            }

            double massH = Settings.GetFragmentMass(group.LabelType, mods, transition, isotopeDist);
            var isotopeDistInfo = TransitionDocNode.GetIsotopeDistInfo(transition, isotopeDist);

            if (group.DecoyMassShift.HasValue && !info.DecoyMassShift.HasValue)
                throw new InvalidDataException("All transitions of decoy precursors must have a decoy mass shift.");

            return new TransitionDocNode(transition, info.Annotations, info.Losses,
                massH, isotopeDistInfo, info.LibInfo, info.Results);
        }

        private static Annotations ReadAnnotations(XmlReader reader)
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
                    throw new InvalidDataException("Annotation found without name.");
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

            public void ReadXml(XmlReader reader, SrmSettings settings)
            {
                // Accept uppercase and lowercase for backward compatibility with v0.1
                IonType = reader.GetEnumAttribute(ATTR.fragment_type, IonType.y, true);
                Ordinal = reader.GetIntAttribute(ATTR.fragment_ordinal);
                MassIndex = reader.GetIntAttribute(ATTR.mass_index);
                // NOTE: PrecursorCharge is used only in TransitionInfo.ReadUngroupedTransitionListXml()
                //       to support v0.1 document format
                PrecursorCharge = reader.GetIntAttribute(ATTR.precursor_charge);
                Charge = reader.GetIntAttribute(ATTR.product_charge);
                DecoyMassShift = reader.GetNullableIntAttribute(ATTR.decoy_mass_shift);

                if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    Annotations = ReadAnnotations(reader);
                    Losses = ReadTransitionLosses(reader, settings);
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
                                throw new InvalidDataException(string.Format("No modification named {0} was found in this document.", nameMod));
                            StaticMod modLoss = staticMods[indexMod];
                            if (!modLoss.HasLoss || indexLoss >= modLoss.Losses.Count)
                                throw new InvalidDataException(string.Format("Invalid loss index {0} for modification {1}", indexLoss, nameMod));
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

            private static Results<TransitionChromInfo> ReadTransitionResults(XmlReader reader, SrmSettings settings)
            {
                if (reader.IsStartElement(EL.transition_results))
                    return ReadResults(reader, settings, EL.transition_peak, ReadTransitionPeak);
                return null;
            }

            private static TransitionChromInfo ReadTransitionPeak(XmlReader reader,
                SrmSettings settings, ChromFileInfoId fileId)
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
                bool? truncated = reader.GetNullableBoolAttribute(ATTR.truncated);
                bool identified = reader.GetBoolAttribute(ATTR.identified);
                bool userSet = reader.GetBoolAttribute(ATTR.user_set);
                var annotations = Annotations.EMPTY;
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    annotations = ReadAnnotations(reader);
                }
                int countRatios = settings.PeptideSettings.Modifications.InternalStandardTypes.Count;
                return new TransitionChromInfo(fileId,
                                               optimizationStep,
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
                                               new float?[countRatios],
                                               annotations,
                                               userSet);
            }
        }

        private static Results<TItem> ReadResults<TItem>(XmlReader reader, SrmSettings settings, string start,
                Func<XmlReader, SrmSettings, ChromFileInfoId, TItem> readInfo)
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
                throw new InvalidDataException("No results information found in the document settings");

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
                        throw new InvalidDataException(string.Format("No replicate named {0} found in measured results", name));
                }
                string fileId = reader.GetAttribute(ATTR.file);
                var fileInfoId = (fileId != null
                    ? chromatogramSet.FindFileById(fileId)
                    : chromatogramSet.MSDataFileInfos[0].FileId);
                if (fileInfoId == null)
                    throw new InvalidDataException(string.Format("No file with id {0} found in the replicate {1}", fileId, name));

                TItem chromInfo = readInfo(reader, settings, fileInfoId);
                // Consume the tag
                reader.Read();

                if (chromInfo != default(TItem))
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
            writer.WriteAttribute(ATTR.decoy, node.IsDecoy);

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
            writer.WriteAttribute(ATTR.decoy, node.IsDecoy);

            double massH = Settings.GetPrecursorCalc(IsotopeLabelType.light, node.ExplicitMods).GetPrecursorMass(sequence);
            writer.WriteAttribute(ATTR.calc_neutral_pep_mass,
                SequenceMassCalc.PersistentNeutral(massH));

            writer.WriteAttribute(ATTR.num_missed_cleavages, peptide.MissedCleavages);
            writer.WriteAttributeNullable(ATTR.rank, node.Rank);

            string modSeq = Settings.GetModifiedSequence(node);
            var rtPredictor = Settings.PeptideSettings.Prediction.RetentionTime;
            double? scoreCalc = null;
            if(rtPredictor != null)
            {
                scoreCalc = rtPredictor.Calculator.ScoreSequence(modSeq);
                if (scoreCalc.HasValue)
                {
                    writer.WriteAttribute(ATTR.rt_calculator_score, scoreCalc);
                    writer.WriteAttributeNullable(ATTR.predicted_retention_time,
                        rtPredictor.GetRetentionTime(scoreCalc.Value));
                } 
            }
            
            writer.WriteAttributeNullable(ATTR.avg_measured_retention_time, node.AverageMeasuredRetentionTime);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);
            WriteExplicitMods(writer, node);
            WriteImplicitMods(writer, node);

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
        }

        private void WriteExplicitMods(XmlWriter writer, PeptideDocNode node)
        {
            if (node.ExplicitMods == null)
                return;
            var mods = node.ExplicitMods;
            if (mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.variable_modifications,
                    EL.variable_modification, null, node.ExplicitMods.StaticModifications, node.Peptide.Sequence);                

                // If no heavy modifications, then don't write an <explicit_modifications> tag
                if (!mods.HasHeavyModifications)
                    return;
            }
            writer.WriteStartElement(EL.explicit_modifications);
            if (!mods.IsVariableStaticMods)
            {
                WriteExplicitMods(writer, EL.explicit_static_modifications,
                    EL.explicit_modification, null, node.ExplicitMods.StaticModifications, node.Peptide.Sequence);                
            }
            foreach (var heavyMods in node.ExplicitMods.GetHeavyModifications())
            {
                IsotopeLabelType labelType = heavyMods.LabelType;
                if (Equals(labelType, IsotopeLabelType.heavy))
                    labelType = null;

                WriteExplicitMods(writer, EL.explicit_heavy_modifications,
                    EL.explicit_modification, labelType, heavyMods.Modifications, node.Peptide.Sequence);                
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
                                      string.Format("{0}{1}", (massDiff < 0 ? "" : "+"), Math.Round(massDiff, 1)));

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
            writer.WriteAttribute(ATTR.charge, group.PrecursorCharge);
            if (!group.LabelType.IsLight)
                writer.WriteAttribute(ATTR.isotope_label, group.LabelType);
            writer.WriteAttribute(ATTR.calc_neutral_mass,
                                  SequenceMassCalc.PersistentNeutral(SequenceMassCalc.GetMH(node.PrecursorMz,
                                                                                            group.PrecursorCharge)));
            writer.WriteAttribute(ATTR.precursor_mz, SequenceMassCalc.PersistentMZ(node.PrecursorMz));

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
            
            // modified sequence
            var calcPre = Settings.GetPrecursorCalc(node.TransitionGroup.LabelType, nodePep.ExplicitMods);
            string seq = node.TransitionGroup.Peptide.Sequence;
            writer.WriteAttribute(ATTR.modified_sequence, calcPre.GetModifiedSequence(seq, true));

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
            writer.WriteAttributeNullable(ATTR.truncated, chromInfo.Truncated);
            writer.WriteAttributeNullable(ATTR.identified, chromInfo.Identified);
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
                writer.WriteAttribute(ATTR.fragment_ordinal, transition.Ordinal);
                double massH = SequenceMassCalc.GetMH(nodeTransition.Mz, transition.Charge);
                writer.WriteAttribute(ATTR.calc_neutral_mass,
                    SequenceMassCalc.PersistentNeutral(massH));
                writer.WriteAttribute(ATTR.product_charge, transition.Charge);
                writer.WriteAttribute(ATTR.cleavage_aa, transition.AA.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttribute(ATTR.loss_neutral_mass, nodeTransition.LostMass);
            }

            WriteAnnotations(writer, nodeTransition.Annotations);
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

            writer.WriteElementString(EL.precursor_mz, SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz));
            writer.WriteElementString(EL.product_mz, SequenceMassCalc.PersistentMZ(nodeTransition.Mz));
            TransitionPrediction predict = Settings.TransitionSettings.Prediction;
            var optimizationMethod = predict.OptimizedMethodType;
            double? ce = null;
            double? dp = null;
            double regressionMz = Settings.GetRegressionMz(nodePep, nodeGroup);
            var ceRegression = predict.CollisionEnergy;
            var dpRegression = predict.DeclusteringPotential;
            if (optimizationMethod == OptimizedMethodType.None)
            {
                if (ceRegression != null)
                {
                    ce = ceRegression.GetCollisionEnergy(transition.Group.PrecursorCharge, regressionMz);
                }
                if (dpRegression != null)
                {
                    dp = dpRegression.GetDeclustringPotential(regressionMz);
                }
            }
            else
            {
                ce = OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, optimizationMethod, ceRegression, GetCollisionEnergy);

                dp = OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, optimizationMethod, dpRegression, GetDeclusteringPotential);
            }

            if (ce.HasValue)
            {
                writer.WriteElementString(EL.collision_energy, ce.Value);
            }

            if (dp.HasValue)
            {
                writer.WriteElementString(EL.declustering_potential, dp.Value);
            }
        }

        public double GetCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, int step)
        {
            return GetCollisionEnergy(Settings, nodePep, nodeGroup,
                                      Settings.TransitionSettings.Prediction.CollisionEnergy, step);
        }

        private static double GetCollisionEnergy(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CollisionEnergyRegression regression, int step)
        {
            int charge = nodeGroup.TransitionGroup.PrecursorCharge;
            double mz = settings.GetRegressionMz(nodePep, nodeGroup);
            return regression.GetCollisionEnergy(charge, mz) + regression.StepSize * step;
        }

        public double GetOptimizedCollisionEnergy(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTransition)
        {
            var prediction = Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var regression = prediction.CollisionEnergy;

            return OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, methodType, regression, GetCollisionEnergy);
        }

        public  double GetDeclusteringPotential(PeptideDocNode nodePep,
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
            var prediction = Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var regression = prediction.DeclusteringPotential;

            return OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Settings,
                nodePep, nodeGroup, nodeTransition, methodType, regression, GetDeclusteringPotential);
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
                writer.WriteAttribute(ATTR.retention_time, chromInfo.RetentionTime);
                writer.WriteAttribute(ATTR.start_time, chromInfo.StartRetentionTime);
                writer.WriteAttribute(ATTR.end_time, chromInfo.EndRetentionTime);
                writer.WriteAttribute(ATTR.area, chromInfo.Area);
                writer.WriteAttribute(ATTR.background, chromInfo.BackgroundArea);
                writer.WriteAttribute(ATTR.height, chromInfo.Height);
                writer.WriteAttribute(ATTR.fwhm, chromInfo.Fwhm);
                writer.WriteAttribute(ATTR.fwhm_degenerate, chromInfo.IsFwhmDegenerate);
                writer.WriteAttributeNullable(ATTR.truncated, chromInfo.IsTruncated);
                writer.WriteAttribute(ATTR.identified, chromInfo.IsIdentified);
                writer.WriteAttribute(ATTR.rank, chromInfo.Rank);
            }
            writer.WriteAttribute(ATTR.user_set, chromInfo.UserSet);
            WriteAnnotations(writer, chromInfo.Annotations);
        }

        private static void WriteAnnotations(XmlWriter writer, Annotations annotations)
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
