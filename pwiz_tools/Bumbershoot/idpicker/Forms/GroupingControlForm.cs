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
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using NHibernate;
using NHibernate.Linq;
using IDPicker.DataModel;

namespace IDPicker.Forms
{
    public partial class GroupingControlForm : Form
    {
        private class tlvBranch
        {
            public string Text;
            public object Data;
            public tlvBranch Parent;
            public List<tlvBranch> Children;
            public ContextMenuStrip cms;
        }

        NHibernate.ISession session;
        private OLVListItem _clickedItem;
        int _numberNewNodes;
        private tlvBranch _rootNode;

        public GroupingControlForm(NHibernate.ISessionFactory sessionFactory)
        {
            InitializeComponent();

            this.session = sessionFactory.OpenSession();

            tlvGroupedFiles.CanExpandGetter += x => (((tlvBranch)x).Data is SpectrumSourceGroup && ((tlvBranch)x).Children.Any());
            tlvGroupedFiles.ChildrenGetter += getChildren;
            tlvGroups.AspectGetter += x =>
                                          {
                                              var nodeTracker = ((tlvBranch) x);
                                              var offsetCorrection = 0;
                                              while (nodeTracker.Parent.Text != null)
                                              {
                                                  offsetCorrection++;
                                                  nodeTracker = nodeTracker.Parent;
                                              }
                                              return ((tlvBranch) x).Text + new string(' ',offsetCorrection*7);
                                          };
            tlvGroups.AutoCompleteEditor = false;

            tlvGroups.ImageGetter += delegate(object x) { return (((tlvBranch)x).Data is SpectrumSourceGroup) ? Properties.Resources.XPfolder_closed : Properties.Resources.file; };

            ApplyDefaultGroups(null, null);
        }

        private IEnumerable getChildren(object model)
        {
            var branch = (tlvBranch)model;
            return branch.Children;
        }

        private void AddAssociatedSpectra(ref tlvBranch groupNode, Dictionary<int, List<SpectrumSource>> spectraDictionary)
        {
            try
            {
                int groupID = (int)(groupNode.Data as SpectrumSourceGroup).Id;

                if (spectraDictionary.ContainsKey(groupID))
                {
                    foreach (SpectrumSource ss in spectraDictionary[groupID])
                    {
                        var newNode = new tlvBranch
                                          {
                                              Text = Path.GetFileName(ss.Name),
                                              Parent = groupNode,
                                              Data = ss,
                                              cms = cmRightClickFileNode
                                          };

                        groupNode.Children.Add(newNode);
                    }
                }
            }
            catch
            {
                MessageBox.Show("Could not add spectra to branch");
            }
        }

        private tlvBranch FillBranch(tlvBranch groupNode, IList<SpectrumSourceGroup> groups, Dictionary<int, List<SpectrumSource>> spectraDictionary)
        {
            List<SpectrumSourceGroup> potentialChildren;
            string fullPath = (groupNode.Data as SpectrumSourceGroup).Name;

            if (fullPath == "/")
                potentialChildren = (from g in groups
                                     where g.Name != fullPath &&
                                     !g.Name.Remove(0,1).Contains("/")
                                     select g).ToList();
            else
                potentialChildren = (from g in groups
                                     where g.Name.Contains(fullPath + "/") &&
                                     !g.Name.Remove(0,fullPath.Length+1).Contains("/")
                                     select g).ToList();

            foreach (SpectrumSourceGroup ssg in potentialChildren)
            {
                var newNode = new tlvBranch
                                  {
                                      Text = Path.GetFileName(ssg.Name),
                                      Children = new List<tlvBranch>(),
                                      Data = ssg,
                                      Parent = groupNode,
                                      cms = cmRightClickGroupNode
                                  };
                groups.Remove(newNode.Data as SpectrumSourceGroup);

                AddAssociatedSpectra(ref newNode, spectraDictionary);

                groupNode.Children.Add(FillBranch(newNode, groups, spectraDictionary));
            }

            OrganizeNode(groupNode);
            return groupNode;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            var currentSession = session;
            var currentRoot = _rootNode;

            var transaction = currentSession.BeginTransaction();

            SetGroupingHierarchy(currentRoot, currentSession);

            // save ungrouped spectrum sources
            foreach (ListViewItem lvi in lvNonGroupedFiles.Items)
            {
                var ss = (SpectrumSource)lvi.Tag;
                ss.Group = null;
                currentSession.Update(ss);
            }

            transaction.Commit();
        }

        private static List<SpectrumSourceGroup> SetGroupingHierarchy(tlvBranch currentRoot, ISession currentSession)
        {
            var spectraLocations = new List<tlvBranch>();
            var groupsToSave = new List<tlvBranch>();

            // find all groups still present
            getSprectrumSourceGroupsRecursively(currentRoot, groupsToSave,string.Empty);

            // remove old groups and links
            currentSession.CreateQuery("DELETE SpectrumSourceGroupLink").ExecuteUpdate();
            var unusedGroups = currentSession.Query<SpectrumSourceGroup>().ToList();

            foreach (tlvBranch tn in groupsToSave)
                unusedGroups.Remove((tn.Data as SpectrumSourceGroup));
            foreach (SpectrumSourceGroup ssg in unusedGroups)
                currentSession.Delete(ssg);
               
            // save group layout
            foreach (tlvBranch treeNode in groupsToSave)
                currentSession.SaveOrUpdate(treeNode.Data as SpectrumSourceGroup);

            // get new spectra locations
            getListOfSprectrumSourcesRecursively(currentRoot, ref spectraLocations);

            // update SpectrumSource.Group_ and insert new SpectrumSourceGroupLinks;
            // using prepared SQL commands for speed

            var cmd1 = currentSession.Connection.CreateCommand();
            var cmd2 = currentSession.Connection.CreateCommand();
            cmd1.CommandText = "UPDATE SpectrumSource SET Group_ = ? WHERE Id = ?";
            cmd2.CommandText = "INSERT INTO SpectrumSourceGroupLink (Group_, Source) VALUES (?,?)";
            var parameters1 = new List<IDbDataParameter>();
            var parameters2 = new List<IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters1.Add(cmd1.CreateParameter());
                parameters2.Add(cmd2.CreateParameter());
                cmd1.Parameters.Add(parameters1[i]);
                cmd2.Parameters.Add(parameters2[i]);
            }
            cmd1.Prepare();
            cmd2.Prepare();

            foreach (tlvBranch tn in spectraLocations)
            {
                parameters1[0].Value = ((SpectrumSourceGroup)tn.Parent.Data).Id;
                parameters1[1].Value = ((SpectrumSource)tn.Data).Id;
                cmd1.ExecuteNonQuery();

                var tempNode = tn;
                while (tempNode.Parent.Text != null)
                {
                    parameters2[0].Value = ((SpectrumSourceGroup)tempNode.Parent.Data).Id;
                    parameters2[1].Value = ((SpectrumSource)tn.Data).Id;
                    tempNode = tempNode.Parent;
                    cmd2.ExecuteNonQuery();
                }
            }
            return groupsToSave.Select(treeNode => treeNode.Data as SpectrumSourceGroup).ToList();
        }

        /// <summary>
        /// Allow delete (delete key) and rename (F2 key) of group nodes (not root or file)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_KeyDown(object sender, KeyEventArgs e)
        {
            var selNode = (tlvBranch)tlvGroupedFiles.SelectedObject;

            try
            {
                if (e.KeyCode == Keys.Delete)
                    RemoveGroupNode(selNode, true);
            }
            catch (Exception exc)
            {
                //HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
                MessageBox.Show(exc.ToString());
            }
        }

        /// <summary>
        /// Set location for determining node that was clicked on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    var column = (OLVColumn)tlvGroupedFiles.Columns[0].Clone();
                    _clickedItem = tlvGroupedFiles.GetItemAt(e.X, e.Y, out column); ;
                    if (_clickedItem != null)
                    {
                        var selNode = (tlvBranch)tlvGroupedFiles.GetModelObject(_clickedItem.Index);
                        if (selNode != null)
                            selNode.cms.Show(tlvGroupedFiles, e.Location.X, e.Location.Y);
                    }
                }
            }
            catch (Exception exc)
            {
                //HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
                MessageBox.Show(exc.ToString());
            }
        }

        /// <summary>
        /// Check to prevent group nodes from being dragged into their children
        /// group nodes.
        /// </summary>
        /// <returns></returns>
        private bool checkIfDestGroupAChildNodeOfMe(tlvBranch destNode, tlvBranch dragNode)
        {
            try
            {
                return destNode.Data is SpectrumSourceGroup
                       && dragNode.Data is SpectrumSourceGroup
                       && ((SpectrumSourceGroup)destNode.Data).Name
                              .Contains(((SpectrumSourceGroup)dragNode.Data).Name);
            }
            catch (Exception exc)
            {
                throw new Exception("Error validating group move: checkIfDestGroupAChildNodeOfMe()\r\n", exc);
            }

        }

        private static void getListOfSprectrumSourcesRecursively(tlvBranch treeNode, ref List<tlvBranch> spectraNodes)
        {
            try
            {
                if (treeNode.Data is SpectrumSource)
                    spectraNodes.Add(treeNode);
                else
                    foreach (tlvBranch subNode in treeNode.Children)
                        getListOfSprectrumSourcesRecursively(subNode, ref spectraNodes);
            }
            catch (Exception exc)
            {
                throw new Exception("Error in retrieving spectra", exc);
            }

        }

        private static void getSprectrumSourceGroupsRecursively(tlvBranch treeNode, List<tlvBranch> nodeList, string rootname)
        {
            if (treeNode.Data is SpectrumSourceGroup)
            {
                (treeNode.Data as SpectrumSourceGroup).Name =
                    (rootname + "/"+ treeNode.Text)
                        .Replace("//", "/");

                nodeList.Add(treeNode);

                foreach (tlvBranch subNode in treeNode.Children)
                    getSprectrumSourceGroupsRecursively(subNode, nodeList, (treeNode.Data as SpectrumSourceGroup).Name);
            }
        }

        private void addGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selNode = (tlvBranch)tlvGroupedFiles.GetModelObject(_clickedItem.Index);

            _numberNewNodes++;

            var ssg = new SpectrumSourceGroup();
            ssg.Name = (string.Format("{0}/New Group({1})", selNode.Text, _numberNewNodes).Replace(@"//",@"/"));

            var newNode = new tlvBranch
                              {
                                  Text = Path.GetFileName(ssg.Name),
                                  cms = cmRightClickGroupNode,
                                  Parent = selNode,
                                  Children = new List<tlvBranch>(),
                                  Data = ssg
                              };

            selNode.Children.Add(newNode);
            OrganizeNode(selNode);
            tlvGroupedFiles.RefreshObject(selNode);
            if (selNode.Children.Count == 1)
                tlvGroupedFiles.Expand(selNode);
            tlvGroupedFiles.EditSubItem(tlvGroupedFiles.ModelToItem(newNode),0);
        }

        private void renameGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selNode = (tlvBranch)tlvGroupedFiles.GetModelObject(_clickedItem.Index);


            if (selNode.Parent.Text != null)
                tlvGroupedFiles.EditSubItem(_clickedItem, 0);
        }

        private void removeGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selNode = (tlvBranch)tlvGroupedFiles.GetModelObject(_clickedItem.Index);
            RemoveGroupNode(selNode, true);
        }

        private void RemoveGroupNode(tlvBranch selNode, bool refresh)
        {
            var abandonedSpectraSources = new List<tlvBranch>();
            getListOfSprectrumSourcesRecursively(selNode, ref abandonedSpectraSources);

            if (selNode.Parent.Text != null)
            {
                selNode.Parent.Children.Remove(selNode);
                if (refresh)
                    tlvGroupedFiles.RefreshObject(selNode.Parent);
            }
            else
            {
                selNode.Children.Clear();
                if (refresh)
                    tlvGroupedFiles.RefreshObject(selNode);
            }

            foreach (tlvBranch tn in abandonedSpectraSources)
            {
                var lvi = new ListViewItem {Text = Path.GetFileName(tn.Text), Tag = tn};
                lvNonGroupedFiles.Items.Add(lvi);
            }
        }

        private void RemoveFileNode(tlvBranch selNode, bool refresh)
        {
            selNode.Parent.Children.Remove(selNode);
            if (refresh)
                tlvGroupedFiles.RefreshObject(selNode.Parent);

            var lvi = new ListViewItem {Text = Path.GetFileName(selNode.Text), Tag = selNode};
            lvNonGroupedFiles.Items.Add(lvi);
        }

        private void removeFileNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selNode = (tlvBranch)tlvGroupedFiles.GetModelObject(_clickedItem.Index);
            RemoveFileNode(selNode, true);
        }

        private void lvNonGroupedFiles_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void lvNonGroupedFiles_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("System.String", true))
            {
                var dragSources = from tlvBranch branch in tlvGroupedFiles.SelectedObjects
                                where !(branch.Data is SpectrumSourceGroup)
                                select branch;

                var remaining = from tlvBranch branch in tlvGroupedFiles.SelectedObjects
                                where (branch.Data is SpectrumSourceGroup)
                                select branch;

                lvNonGroupedFiles.BeginUpdate();
                var toRefresh = new HashSet<tlvBranch>();
                foreach (var item in dragSources)
                {
                    RemoveFileNode(item, false);
                    toRefresh.Add(item.Parent);
                }
                foreach (var item in remaining)
                {
                    RemoveGroupNode(item, false);
                    toRefresh.Add(item);
                    toRefresh.Add(item.Parent);
                }
                lvNonGroupedFiles.EndUpdate();
                foreach (var item in toRefresh)
                    tlvGroupedFiles.RefreshObject(item);
                
                tlvGroupedFiles.Sort();
            }
        }

        private void lvNonGroupedFiles_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void lvNonGroupedFiles_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void lvNonGroupedFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Modifiers & Keys.Control) == Keys.Control && e.KeyCode == Keys.A)
                foreach (ListViewItem lvi in lvNonGroupedFiles.Items)
                    lvi.Selected = true;
        }

        private void miResetFiles_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to remove all groups?", "Remove All", MessageBoxButtons.YesNo) == DialogResult.Yes)
                RemoveGroupNode((tlvBranch) tlvGroupedFiles.GetModelObject(0), true);
        }

        private void miExpandGroups_Click(object sender, EventArgs e)
        {
            tlvGroupedFiles.ExpandAll();
        }

        private void miCollapseGroups_Click(object sender, EventArgs e)
        {
            tlvGroupedFiles.CollapseAll();
        }

        private void tlvGroupedFiles_CanDrop(object sender, OlvDropEventArgs e)
        {
            if (e.DataObject.GetType().ToString() == "System.Windows.Forms.DataObject")
                e.Effect = DragDropEffects.Move;
            else if (e.DataObject is OLVDataObject
                     && (e.DropTargetItem.RowObject is tlvBranch
                         && ((e.DropTargetItem.RowObject as tlvBranch).Data is SpectrumSourceGroup
                              || (e.DropTargetItem.RowObject as tlvBranch).Data is SpectrumSource)))
            {
                var target = e.DropTargetItem.RowObject as tlvBranch;
                var dragging = (e.DataObject as OLVDataObject).ModelObjects;
                var isValid = true;
                foreach (var item in dragging)
                {
                    if (checkIfDestGroupAChildNodeOfMe(target,item as tlvBranch))
                    {
                        isValid = false;
                        break;
                    }
                }
                if (isValid)
                    e.Effect = DragDropEffects.Move;
            }
        }

        private void tlvGroupedFiles_Dropped(object sender, OlvDropEventArgs e)
        {
            var target = (tlvBranch)e.DropTargetItem.RowObject;
            var index = -1;
            var toRefresh = new HashSet<tlvBranch>();

            if (target.Data is SpectrumSource)
            {
                index = target.Parent.Children.IndexOf(target);
                target = target.Parent;
            }
            if (e.DataObject.GetType().ToString() == "System.Windows.Forms.DataObject")
            {
                var sources = from ListViewItem item in lvNonGroupedFiles.SelectedItems
                              select (tlvBranch) item.Tag;

                foreach (var source in sources)
                {
                    if (index >= 0)
                        target.Children.Insert(index, source);
                    else
                        target.Children.Add(source);
                    source.Parent = target;
                }

                toRefresh.Add(target);

                var usedItems = from ListViewItem item in lvNonGroupedFiles.SelectedItems
                                select item;
                foreach (var item in usedItems)
                    lvNonGroupedFiles.Items.Remove(item);
            }
            else if (e.DataObject is OLVDataObject)
            {
                var dragging = e.DataObject as OLVDataObject;
                var sources = from tlvBranch item in dragging.ModelObjects
                              where item.Data is SpectrumSource
                              select item;
                var groups = from tlvBranch item in dragging.ModelObjects
                             where (item.Data is SpectrumSourceGroup
                             && (item.Data as SpectrumSourceGroup).Name != "\\")
                             select item;
                var sourcesToIgnore = new List<tlvBranch>();

                foreach (var group in groups)
                {
                    //find and ignore spectra in group
                    getListOfSprectrumSourcesRecursively(group, ref sourcesToIgnore);

                    group.Parent.Children.Remove(group);
                    toRefresh.Add(group.Parent);
                    group.Parent = target;
                    if (target.Children.Any())
                        target.Children.Insert(0, group);
                    else
                        target.Children.Add(group);
                }

                sources = from tlvBranch s in sources where !sourcesToIgnore.Contains(s) select s;
                foreach (var source in sources)
                {
                    source.Parent.Children.Remove(source);
                    toRefresh.Add(source.Parent);
                    source.Parent = target;
                    if (index >= 0)
                        target.Children.Insert(index, source);
                    else
                        target.Children.Add(source);
                }

                toRefresh.Add(target);
                tlvGroupedFiles.Expand(target);
            }
            foreach (var item in toRefresh)
                tlvGroupedFiles.RefreshObject(item);
            OrganizeNode(target);
        }

        private void tlvGroupedFiles_CellEditFinishing(object sender, CellEditEventArgs e)
        {
            if (e.NewValue.ToString().Contains("/"))
            {
                e.Cancel = true;
                return;
            }

            foreach (tlvBranch item in ((tlvBranch) e.RowObject).Parent.Children)
                if (item.Text == (string)e.NewValue)
                {
                    e.Cancel = true;
                    return;
                }
            var rowObject = ((tlvBranch) e.RowObject);
            rowObject.Text = ((string)e.NewValue).Trim();
            
            if (rowObject.Parent != null 
                && rowObject.Data is SpectrumSourceGroup
                && rowObject.Parent.Data is SpectrumSourceGroup)
            {
                ((SpectrumSourceGroup) rowObject.Data).Name =
                    (((SpectrumSourceGroup) rowObject.Parent.Data).Name + "/" +
                     ((string) e.NewValue).Trim()).Replace("//", "/");
            }
        }

        private void tlvGroupedFiles_CellEditStarting(object sender, CellEditEventArgs e)
        {
            if (((tlvBranch)e.RowObject).Data is SpectrumSource ||(string)e.Value == "/")
            {
                e.Cancel = true;
            }

            e.Control.Text = ((string) e.Value).Trim();
        }

        private void ApplyDefaultGroups(object sender, EventArgs e)
        {
            if (sender == miDefaultGroups &&
                MessageBox.Show("Are you sure you want to reset the groups to their initial values?",
                "Reset Groups?", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            var obj = tlvGroupedFiles.Objects;
            foreach(var item in obj)
                tlvGroupedFiles.RemoveObject(item);
            lvNonGroupedFiles.Items.Clear();

            var sourcesByGroup = session.Query<SpectrumSource>().Where(o => o.Group != null)
                                                                .ToLookup(o => (int)o.Group.Id.Value)
                                                                .ToDictionary(o => o.Key, o => o.ToList());

            var groups = session.Query<SpectrumSourceGroup>().ToList();
            var groupNode = new tlvBranch
            {
                Text = "/",
                Parent = new tlvBranch { Text = null },
                Children = new List<tlvBranch>(),
                Data = (from g in groups where g.Name == "/" select g).Single()
            };

            groups.Remove(groupNode.Data as SpectrumSourceGroup);
            groupNode.Text = (groupNode.Data as SpectrumSourceGroup).Name;
            groupNode.cms = cmRightClickGroupNode;

            AddAssociatedSpectra(ref groupNode, sourcesByGroup);

            groupNode = FillBranch(groupNode, groups, sourcesByGroup);

            tlvGroupedFiles.AddObject(groupNode);
            _rootNode = groupNode;

            tlvGroupedFiles.ExpandAll();

            // add ungrouped sources
            foreach (var ss in session.Query<SpectrumSource>().Where(g => g.Group == null))
            {
                var lvi = new ListViewItem { Text = ss.Name, Tag = ss };
                lvNonGroupedFiles.Items.Add(lvi);
            }
        }

        private int OrganizeNode(tlvBranch target)
        {
            var sources = from tlvBranch item in target.Children
                           where item.Data is SpectrumSource
                           select item;
            var groups = from tlvBranch item in target.Children
                          where item.Data is SpectrumSourceGroup
                          select item;
            if (!sources.Any() && !groups.Any())
                return -1;
            
            target.Children = new List<tlvBranch>();
            foreach (var item in groups)
                target.Children.Add(item);
            foreach (var item in sources)
                target.Children.Add(item);

            return groups.Count();
        }

        private static HashSet<string> _processedSources;
        public static List<SpectrumSourceGroup> SetInitialStructure(TreeNode node, ISession session)
        {
            if (node == null)
                return new List<SpectrumSourceGroup>();
            
            _processedSources = new HashSet<string>();
            tlvBranch convertedNode = GenericNodeToBranch(node, session, null);
            var transaction = session.BeginTransaction();
            var returnValue = SetGroupingHierarchy(convertedNode, session);
            transaction.Commit();
            return returnValue;
        }

        private static tlvBranch GenericNodeToBranch(TreeNode node, ISession session, tlvBranch parent)
        {
            var thisBranch = new tlvBranch
                                 {
                                     Text = node.Text,
                                     Parent = parent ?? new tlvBranch()
                                 };
            if (node.Tag.ToString() == "Source")
            {
                var source = session.QueryOver<SpectrumSource>().Where(x => x.Name == node.Text).List().FirstOrDefault();
                if (source == null || _processedSources.Contains(source.Name))
                    return null;
                _processedSources.Add(source.Name);
                thisBranch.Data = source;
                thisBranch.Children = null;
            }
            else
            {
                var trimmedName = node.Text == "/" ? "/" : Path.GetFileName(node.Text);
                var ssg = session.QueryOver<SpectrumSourceGroup>().Where(x => x.Name == trimmedName).SingleOrDefault() ??
                          new SpectrumSourceGroup { Name = trimmedName };
                thisBranch.Text = trimmedName;
                thisBranch.Data = ssg;
                thisBranch.Children = new List<tlvBranch>();
                foreach (TreeNode childNode in node.Nodes)
                {
                    var newChild = GenericNodeToBranch(childNode, session, thisBranch);
                    if (newChild != null)
                        thisBranch.Children.Add(newChild);
                }
                if (!thisBranch.Children.Any())
                    return null;
            }
            return thisBranch;
        }
    }
}
