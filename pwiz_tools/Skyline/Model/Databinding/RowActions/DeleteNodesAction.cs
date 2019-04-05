/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding.RowActions
{
    public abstract class DeleteNodesAction : RowAction
    {
        public static readonly DeleteNodesAction Transitions = new DeleteTransitions();
        public static readonly DeleteNodesAction Precursors = new DeletePrecursors();
        public static readonly DeleteNodesAction Peptides = new DeletePeptides();
        public static readonly DeleteNodesAction Proteins = new DeleteProteins();

        public static IEnumerable<DeleteNodesAction> All
        {
            get
            {
                yield return Proteins;
                yield return Peptides;
                yield return Precursors;
                yield return Transitions;
            }
        }


        public abstract IEnumerable<SkylineDocNode> GetSelectedNodes(BoundDataGridView dataGridView);

        public override ToolStripMenuItem CreateMenuItem(SrmDocument.DOCUMENT_TYPE docType, BoundDataGridView dataGridView)
        {
            var menuItem = new ToolStripMenuItem(GetMenuItemText(docType), null, (sender, args) => DeleteNodes(dataGridView));
            if (!GetSelectedNodes(dataGridView).Any())
            {
                menuItem.Enabled = false;
            }

            return menuItem;
        }

        protected void DeleteNodes(BoundDataGridView dataGridView)
        {
            DeleteSkylineDocNodes(GetSkylineWindow(dataGridView), dataGridView, GetSelectedNodes(dataGridView));
        }

        class DeleteTransitions : DeleteNodesAction
        {
            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return Resources.DeleteTransitions_MenuItemText_Delete_Transitions___;
            }
            public override IEnumerable<SkylineDocNode> GetSelectedNodes(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(Entities.Transition), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var transition in rowItemValues.GetRowValues(rowItem).Cast<Entities.Transition>())
                    {
                        yield return transition;
                    }
                }
            }
        }

        class DeletePrecursors : DeleteNodesAction
        {
            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return Resources.DeletePrecursors_MenuItemText_Delete_Precursors___;
            }
            public override IEnumerable<SkylineDocNode> GetSelectedNodes(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(Precursor), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var precursor in rowItemValues.GetRowValues(rowItem).Cast<Precursor>())
                    {
                        yield return precursor;
                    }
                }

                foreach (var transition in Transitions.GetSelectedNodes(dataGridView))
                {
                    yield return ((Entities.Transition) transition).Precursor;
                }
            }
        }

        class DeletePeptides : DeleteNodesAction
        {
            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return docType == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? Resources.DeletePeptides_MenuItemText_Delete_Peptides___
                    : Resources.DeletePeptides_GetMenuItemText_Delete_Molecules___;
            }
            public override IEnumerable<SkylineDocNode> GetSelectedNodes(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(Entities.Peptide), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var precursor in rowItemValues.GetRowValues(rowItem).Cast<Entities.Peptide>())
                    {
                        yield return precursor;
                    }
                }

                foreach (var precursor in Precursors.GetSelectedNodes(dataGridView))
                {
                    yield return ((Precursor)precursor).Peptide;
                }
            }
        }
        class DeleteProteins : DeleteNodesAction
        {
            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return docType == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? Resources.DeleteProteins_MenuItemText_Delete_Proteins___
                    : Resources.DeleteProteins_MenuItemText_Delete_Molecule_Lists___;
            }

            public override IEnumerable<SkylineDocNode> GetSelectedNodes(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(Protein), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var protein in rowItemValues.GetRowValues(rowItem).Cast<Protein>())
                    {
                        yield return protein;
                    }
                }

                foreach (var peptide in Peptides.GetSelectedNodes(dataGridView))
                {
                    yield return ((Entities.Peptide)peptide).Protein;
                }
            }
        }

        public static void DeleteSkylineDocNodes(SkylineWindow skylineWindow, BoundDataGridView dataGridView, IEnumerable<SkylineDocNode> docNodesToDelete)
        {
            var docNodes = (ICollection<SkylineDocNode>) DistinctNodes(docNodesToDelete).ToList();
            if (docNodes.Count == 0)
            {
                return;
            }

            var owner = FormUtil.FindTopLevelOwner(dataGridView);
            var confirmationMessages = docNodes.Select(node => node.GetDeleteConfirmation(docNodes.Count)).Distinct()
                .ToArray();
            string message = confirmationMessages.Length == 1
                ? confirmationMessages[0]
                : SkylineDocNode.GetGenericDeleteConfirmation(docNodes.Count);
            if (MultiButtonMsgDlg.Show(owner, message, MultiButtonMsgDlg.BUTTON_OK) != DialogResult.OK)
            {
                return;
            }
            DeleteDocNodes(skylineWindow, new HashSet<IdentityPath>(docNodes.Select(node => node.IdentityPath)));
        }


        private static void DeleteDocNodes(SkylineWindow skylineWindow, HashSet<IdentityPath> identityPaths)
        {
            if (null != skylineWindow)
            {
                List<IdentityPath> deletedNodePaths = null;
                skylineWindow.ModifyDocument(Resources.SkylineViewContext_DeleteDocNodes_Delete_items,
                    doc => DeleteNodes(doc, identityPaths, out deletedNodePaths),
                    docPair => SkylineWindow.CreateDeleteNodesEntry(docPair,
                        deletedNodePaths.Select(i => AuditLogEntry.GetNodeName(docPair.OldDoc, docPair.OldDoc.FindNode(i)).ToString()), deletedNodePaths.Count));
            }
        }

        private static SrmDocument DeleteNodes(SrmDocument document, HashSet<IdentityPath> identityPathsToDelete, out List<IdentityPath> deletedPaths)
        {
            var newDocument = (SrmDocument)DeleteChildren(document, IdentityPath.ROOT, identityPathsToDelete, out deletedPaths);
            if (newDocument != null)
            {
                return newDocument;
            }
            return (SrmDocument)document.ChangeChildren(new DocNode[0]);
        }

        private static DocNode DeleteChildren(DocNode parent, IdentityPath identityPath, HashSet<IdentityPath> pathsToDelete, out List<IdentityPath> deletedPaths)
        {
            deletedPaths = new List<IdentityPath>();
            var docNodeParent = parent as DocNodeParent;
            if (docNodeParent == null)
            {
                return parent;
            }
            if (docNodeParent.Children.Count == 0)
            {
                return parent;
            }
            var newChildren = new List<DocNode>();
            foreach (var child in docNodeParent.Children)
            {
                var childPath = new IdentityPath(identityPath, child.Id);
                if (pathsToDelete.Contains(childPath))
                {
                    deletedPaths.Add(childPath);
                    continue;
                }

                List<IdentityPath> deletedChildPaths;
                var newChild = DeleteChildren(child, childPath, pathsToDelete, out deletedChildPaths);
                if (newChild != null)
                    newChildren.Add(newChild);
                deletedPaths.AddRange(deletedChildPaths);
            }
            if (newChildren.Count == 0)
            {
                deletedPaths.Clear();
                deletedPaths.Add(identityPath);
                return null;
            }
            return docNodeParent.ChangeChildren(newChildren);
        }


        private static IEnumerable<SkylineDocNode> DistinctNodes(IEnumerable<SkylineDocNode> nodes)
        {
            var identityPaths = new HashSet<IdentityPath>();
            foreach (var node in nodes)
            {
                if (identityPaths.Add(node.IdentityPath))
                {
                    yield return node;
                }
            }
        }
    }
}
