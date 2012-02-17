//
// $Id: UnimodControl.cs 374 2012-01-19 20:57:39Z holmanjd $
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
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;
using pwiz.CLI.data;

namespace IDPicker.Controls
{
    public partial class UnimodControl : UserControl
    {
        #region Name to Abbreviation dictionary

        private static Dictionary<string, char> _nameToAbbreviation = new Dictionary<string, char>
                                                                          {
                                                                              {"All Sites", '*'},
                                                                              {"NTerminus", '('},
                                                                              {"CTerminus", ')'},
                                                                              {"Alanine", 'A'},
                                                                              {"Arginine", 'R'},
                                                                              {"Asparagine", 'N'},
                                                                              {"AsparticAcid", 'D'},
                                                                              {"Cysteine", 'C'},
                                                                              {"GlutamicAcid", 'E'},
                                                                              {"Glutamine", 'Q'},
                                                                              {"Glycine", 'G'},
                                                                              {"Histidine", 'H'},
                                                                              {"Isoleucine", 'I'},
                                                                              {"Leucine", 'L'},
                                                                              {"Lysine", 'K'},
                                                                              {"Methionine", 'M'},
                                                                              {"Phenylalanine", 'F'},
                                                                              {"Proline", 'P'},
                                                                              {"Serine", 'S'},
                                                                              {"Threonine", 'T'},
                                                                              {"Tryptophan", 'W'},
                                                                              {"Tyrosine", 'Y'},
                                                                              {"Valine", 'V'}
                                                                          };
        #endregion

        #region UnimodTreeNode class
        private class UnimodNode : TreeNode
        {
            public double Mass { get; set; }
            public string Site { get; set; }
            public bool Hidden { get; set; }

            public char SiteChar
            {
                get
                {
                    return _nameToAbbreviation.ContainsKey(Site)
                               ? _nameToAbbreviation[Site]
                               : '?';
                }
            }
        }
        #endregion

        private HashSet<double> _savedMasses;
        private HashSet<char> _savedSites;
        private bool _automated;
        private Dictionary<TreeNode, TreeNode> _hiddenNodes;
        private Dictionary<double, int> _currentMasses;
        private Dictionary<char, int> _currentSites;
        private Dictionary<TreeNode, int> _categoryChecked;
        private TreeNode _rootNode;

        public UnimodControl()
        {
            InitializeComponent();

            //Initialize members
            SetUnimodDefaults(null, null, null);
        }

        public void SetUnimodDefaults(HashSet<char> relevantSites, HashSet<double> relevantMasses, DistinctMatchFormat distinctMatchFormat)
        {
            UnimodTree.Nodes.Clear();
            _hiddenNodes = new Dictionary<TreeNode, TreeNode>();
            _currentMasses = new Dictionary<double, int>();
            _currentSites = new Dictionary<char, int>();
            _categoryChecked = new Dictionary<TreeNode, int>();
            _savedMasses = new HashSet<double>();
            _savedSites = new HashSet<char>();
            
            //get unimod values
            var refDict = new Dictionary<unimod.Classification, List<UnimodNode>>();
            foreach (unimod.Classification classification in Enum.GetValues(typeof (unimod.Classification)))
                refDict.Add(classification, new List<UnimodNode>());
            foreach (var mod in unimod.modifications())
            {
                double mass = mod.deltaMonoisotopicMass;
                if (distinctMatchFormat != null)
                    mass = distinctMatchFormat.Round(mod.deltaMonoisotopicMass);

                foreach (var spec in mod.specificities)
                {
                    //'if' statement broken up to reduce complexity
                    if (!refDict.ContainsKey(spec.classification))
                        continue;
                    if (relevantSites != null
                        && (!_nameToAbbreviation.ContainsKey(spec.site.ToString())
                        || !relevantSites.Contains(_nameToAbbreviation[spec.site.ToString()])))
                        continue;
                    if (relevantMasses != null && !relevantMasses.Contains(mass))
                        continue;

                    refDict[spec.classification].Add(new UnimodNode()
                                                         {
                                                             Text = mod.name + " - " + spec.site,
                                                             Mass = mass,
                                                             Site = spec.site.ToString(),
                                                             Hidden = spec.hidden
                                                         });
                }
            }

            //Put nodes in their proper places
            _rootNode = new TreeNode("Unimod Modifications");
            UnimodTree.Nodes.Add(_rootNode);
            foreach (unimod.Classification classification in Enum.GetValues(typeof (unimod.Classification)))
            {
                if (classification == unimod.Classification.Any || !refDict[classification].Any())
                    continue;
                var newNode = new TreeNode(classification.ToString()) {Tag = classification};
                _categoryChecked.Add(newNode,0);
                foreach (var mod in refDict[classification])
                    if (!mod.Hidden)
                        newNode.Nodes.Add(mod);
                    else
                        _hiddenNodes.Add(mod, newNode);
                if (newNode.Nodes.Count > 0)
                    _rootNode.Nodes.Add(newNode);
                else
                    _hiddenNodes.Add(newNode, null);
            }
            _rootNode.Expand();

            //Populate filter box
            SiteFilterBox.Items.Clear();
            foreach (var kvp in _nameToAbbreviation)
            {
                if (kvp.Key == "All Sites" || relevantSites == null || relevantSites.Contains(kvp.Value))
                    SiteFilterBox.Items.Add(kvp.Key);
            }
            SiteFilterBox.Text = "All Sites";
        }

        private void UnimodTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_automated)
                return;
            try
            {
                if (e.Node == _rootNode)
                {
                    foreach (TreeNode categoryNode in e.Node.Nodes)
                        categoryNode.Checked = e.Node.Checked;
                    return;
                }
                //disable event handling
                _automated = true;

                //modify node record if the is any information in it
                if (e.Node is UnimodNode)
                {
                    if (e.Node.Checked)
                        AddNodeToList((UnimodNode) e.Node);
                    else
                        RemoveNodeFromList((UnimodNode) e.Node);

                    //modify parent
                    if (e.Node.Parent != null)
                        RelabelSingleCategory(e.Node.Parent);
                }
                else //if it is a category
                {
                    //modify subnode records if they exist
                    foreach (TreeNode subNode in e.Node.Nodes)
                    {
                        if (subNode.Checked != e.Node.Checked && subNode is UnimodNode)
                        {
                            subNode.Checked = e.Node.Checked;
                            if (e.Node.Checked)
                                AddNodeToList((UnimodNode)subNode);
                            else
                                RemoveNodeFromList((UnimodNode)subNode);
                        }
                    }
                    RelabelSingleCategory(e.Node);
                }
            }
            finally
            {
                //allow event to trigger normally again
                _automated = false;
                SitesLabel.Text = _currentSites.Count.ToString();
                MassesLabel.Text = _currentMasses.Count.ToString();
            }
        }

        public bool ChangesMade(bool resetChangeLog)
        {
            var massList = new HashSet<double>();
            var siteList = new HashSet<char>();
            foreach (var kvp in _currentMasses)
                massList.Add(kvp.Key);
            foreach (var kvp in _currentSites)
                siteList.Add(kvp.Key);

            bool confirmedChanges = !massList.SetEquals(_savedMasses) ||
                                    !siteList.SetEquals(_savedSites);
            if (resetChangeLog)
            {
                _savedMasses = new HashSet<double>(massList);
                _savedSites = new HashSet<char>(siteList);
            }
            return confirmedChanges;
        }

        public List<char> GetUnimodSites()
        {
            return _currentSites.Select(kvp => kvp.Key).ToList();
        }

        public List<double> GetUnimodMasses()
        {
            return _currentMasses.Select(kvp => kvp.Key).ToList();
        }

        private void AddNodeToList(UnimodNode baseNode)
        {
            //add mass
            if (_currentMasses.ContainsKey(baseNode.Mass))
                _currentMasses[baseNode.Mass]++;
            else
                _currentMasses.Add(baseNode.Mass, 1);

            //add site
            if (_currentSites.ContainsKey(baseNode.SiteChar))
                _currentSites[baseNode.SiteChar]++;
            else
                _currentSites.Add(baseNode.SiteChar, 1);

            _categoryChecked[baseNode.Parent]++;
        }

        private void RemoveNodeFromList(UnimodNode baseNode)
        {
            if (!_currentMasses.ContainsKey(baseNode.Mass) || !_currentSites.ContainsKey(baseNode.SiteChar))
                return;

            //remove mass
            if (_currentMasses[baseNode.Mass] > 1)
                _currentMasses[baseNode.Mass]--;
            else
                _currentMasses.Remove(baseNode.Mass);

            //remove site
            if (_currentSites[baseNode.SiteChar] > 1)
                _currentSites[baseNode.SiteChar]--;
            else
                _currentSites.Remove(baseNode.SiteChar);

            _categoryChecked[baseNode.Parent]--;
        }

        private void RestoreHiddenNodes()
        {
            foreach (var kvp in _hiddenNodes)
            {
                if (kvp.Value == null)
                    _rootNode.Nodes.Add(kvp.Key);
                else
                {
                    kvp.Value.Nodes.Add(kvp.Key);
                    if (kvp.Key is UnimodNode && kvp.Key.Checked)
                        AddNodeToList((UnimodNode) kvp.Key);
                }
            }
            _hiddenNodes = new Dictionary<TreeNode, TreeNode>();
        }

        private void FilterNodes(bool showHidden, string modifiedSite)
        {
            //HACK: no clue why _rootNode is registering as null when it's defined in the initializer
            if (_rootNode == null)
                return;

            //show all nodes
            RestoreHiddenNodes();
            var siteFilter = _nameToAbbreviation[modifiedSite];
            var siteFilterActive = siteFilter != '*';

            //remove mods that dont match criteria
            foreach (TreeNode categoryNode in _rootNode.Nodes)
            {
                foreach (UnimodNode modNode in categoryNode.Nodes)
                {
                    if (!showHidden && modNode.Hidden)
                    {
                        _hiddenNodes.Add(modNode,categoryNode);
                        if (modNode.Checked)
                            RemoveNodeFromList(modNode);
                    }
                    else if (siteFilterActive 
                        && modNode.SiteChar != siteFilter)
                    {
                        _hiddenNodes.Add(modNode,categoryNode);
                        if (modNode.Checked)
                            RemoveNodeFromList(modNode);
                    }
                }
            }

            //remove categories that are now empty
            var emptyCategories = new List<TreeNode>();
            foreach (var kvp in _hiddenNodes)
            {
                kvp.Key.Remove();
                if (kvp.Value.Nodes.Count == 0)
                {
                    emptyCategories.Add(kvp.Value);
                    kvp.Value.Remove();
                }
            }
            foreach (var item in emptyCategories)
                _hiddenNodes.Add(item,null);

            //update total activated mod count
            RelabelAllCategories();
            SitesLabel.Text = _currentSites.Count.ToString();
            MassesLabel.Text = _currentMasses.Count.ToString();
        }

        private void RelabelAllCategories()
        {
            var savedAutomation = _automated;
            _automated = true;
            foreach (TreeNode category in _rootNode.Nodes)
                RelabelSingleCategory(category);
            _automated = savedAutomation;
        }

        private void RelabelSingleCategory(TreeNode category)
        {
            var checkedNodes = _categoryChecked[category];
            if (checkedNodes == 0)
            {
                category.Checked = false;
                category.Text = category.Tag.ToString();
            }
            else
            {
                if (checkedNodes == category.Nodes.Count)
                    category.Checked = true;
                category.Text = category.Tag + " ("
                                + checkedNodes + " checked)";
            }
        }

        private void FilterEventRaised(object sender, EventArgs e)
        {
            FilterNodes(HiddenModBox.Checked, SiteFilterBox.Text);
        }
    }
}
