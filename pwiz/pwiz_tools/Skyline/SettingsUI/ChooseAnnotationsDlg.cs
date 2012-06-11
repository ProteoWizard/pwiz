/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Form for choosing which annotation definitions should be included in the document
    /// </summary>
    public partial class ChooseAnnotationsDlg : FormEx
    {
        private readonly SkylineWindow _parent;
        private readonly SettingsListBoxDriver<AnnotationDef> _annotationsListBoxDriver;

        public ChooseAnnotationsDlg(SkylineWindow parent)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _parent = parent;
            _annotationsListBoxDriver = new SettingsListBoxDriver<AnnotationDef>(checkedListBoxAnnotations, Settings.Default.AnnotationDefList);
            _annotationsListBoxDriver.LoadList(null, parent.DocumentUI.Settings.DataSettings.AnnotationDefs);
        }

        private void btnEditAnnotationList_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            CheckDisposed();
            _annotationsListBoxDriver.EditList();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            _parent.ModifyDocument("Change Annotation Settings", ChangeAnnotationDefs);
            Close();
        }

        /// <summary>
        /// Changes the set of annotation definitions in the document to the ones chosen
        /// in the checkedlistbox.  Also, walks the nodes in the document tree and removes any
        /// annotation values that is no longer defined in the document.
        /// </summary>
        private SrmDocument ChangeAnnotationDefs(SrmDocument srmDocument)
        {
            var dataSettings = srmDocument.Settings.DataSettings;
            dataSettings = dataSettings.ChangeAnnotationDefs(_annotationsListBoxDriver.Chosen);
            if (dataSettings.Equals(srmDocument.Settings.DataSettings))
            {
                return srmDocument;
            }
            srmDocument = srmDocument.ChangeSettings(srmDocument.Settings.ChangeDataSettings(dataSettings));
            var annotationNames = new HashSet<string>();
            foreach (var annotationDef in dataSettings.AnnotationDefs)
            {
                annotationNames.Add(annotationDef.Name);
            }
            srmDocument = (SrmDocument) StripAnnotationValues(annotationNames, srmDocument);
            return srmDocument;
        }

        /// <summary>
        /// Walks the node tree, and removes any annotation values whose name is not
        /// in "annotationNamesToKeep".  Returns true if the node was modified.
        /// </summary>
        private static DocNode StripAnnotationValues(ICollection<string> annotationNamesToKeep, DocNode docNode)
        {
            var annotations = docNode.Annotations;
            if (StripAnnotationValues(annotationNamesToKeep, ref annotations))
            {
                docNode = docNode.ChangeAnnotations(annotations);
            }
            var docNodeParent = docNode as DocNodeParent;
            if (docNodeParent != null)
            {
                var newChildren = new List<DocNode>();
                var childrenChanged = false;
                foreach (var child in docNodeParent.Children)
                {
                    var newChild = StripAnnotationValues(annotationNamesToKeep, child);
                    childrenChanged = childrenChanged || !ReferenceEquals(child, newChild);
                    newChildren.Add(newChild);
                }
                if (childrenChanged)
                {
                    docNode = docNodeParent.ChangeChildren(newChildren);
                }
            }
            if (docNode is TransitionGroupDocNode)
            {
                var transitionGroupDocNode = docNode as TransitionGroupDocNode;
                if (transitionGroupDocNode.Results != null)
                {
                    var results = transitionGroupDocNode.Results;
                    if (StripAnnotationValues(annotationNamesToKeep, ref results))
                    {
                        docNode = transitionGroupDocNode.ChangeResults(results);
                    }
                }
            }
            if (docNode is TransitionDocNode)
            {
                var transitionDocNode = docNode as TransitionDocNode;
                if (transitionDocNode.Results != null)
                {
                    var results = transitionDocNode.Results;
                    if (StripAnnotationValues(annotationNamesToKeep, ref results))
                    {
                        docNode = transitionDocNode.ChangeResults(results);
                    }
                }
            }
            return docNode;
        }

        private static bool StripAnnotationValues<TItem>(ICollection<string> annotationNamesToKeep, ref Results<TItem> results)
            where TItem : ChromInfo
        {
            if (results == null)
            {
                return false;
            }
            var newResults = new List<ChromInfoList<TItem>>();
            bool fResult = false;
            foreach (var replicate in results)
            {
                var chromInfoList = replicate;
                fResult |= StripAnnotationValues(annotationNamesToKeep, ref chromInfoList);
                newResults.Add(chromInfoList);
            }
            if (fResult)
            {
                results = new Results<TItem>(newResults);
            }
            return fResult;
        }

        private static bool StripAnnotationValues<TItem>(ICollection<string> annotationNamesToKeep, ref ChromInfoList<TItem> chromInfoList)
        {
            if (chromInfoList == null)
                return false;

            bool fResult = false;
            var newList = new List<TItem>();
            foreach (var chromInfo in chromInfoList)
            {
                var transitionChromInfo = chromInfo as TransitionChromInfo;
                if (transitionChromInfo != null)
                {
                    var annotations = transitionChromInfo.Annotations;
                    if (StripAnnotationValues(annotationNamesToKeep, ref annotations))
                    {
                        newList.Add((TItem)(object) transitionChromInfo.ChangeAnnotations(annotations));
                        fResult = true;
                        continue;
                    }
                }
                var transitionGroupChromInfo = chromInfo as TransitionGroupChromInfo;
                if (transitionGroupChromInfo != null)
                {
                    var annotations = transitionGroupChromInfo.Annotations;
                    if (StripAnnotationValues(annotationNamesToKeep, ref annotations))
                    {
                        newList.Add((TItem) (object) transitionGroupChromInfo.ChangeAnnotations(annotations));
                        fResult = true;
                        continue;
                    }
                }
                newList.Add(chromInfo);
            }
            if (fResult)
            {
                chromInfoList = new ChromInfoList<TItem>(newList);
            }
            return fResult;
        }

        private static bool StripAnnotationValues(ICollection<string> annotationNamesToKeep, ref Annotations annotations)
        {
            bool result = false;
            foreach (var entry in annotations.ListAnnotations())
            {
                if (annotationNamesToKeep.Contains(entry.Key))
                {
                    continue;
                }
                annotations = annotations.ChangeAnnotation(entry.Key, null);
                result = true;
            }
            return result;
        }
        public CheckedListBox AnnotationsCheckedListBox
        {
            get { return checkedListBoxAnnotations; }
        }
    }
}
