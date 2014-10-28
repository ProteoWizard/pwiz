/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;

namespace ExampleInteractiveTool
{
    /// <summary>
    /// This is an example main window of an interactive Skyline tool.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly SkylineTool.SkylineToolClient _toolClient;
        private readonly Graph _graph;
        private List<string> _peptides;
        private List<string> _peptideLinks;
        private List<string> _replicateLinks;
        private string _selectedReplicate;

        public MainForm(string[] args)
        {
            InitializeComponent();

            // Create tool client and register for events.
            if (args.Length > 0)
            {
                _toolClient = new SkylineTool.SkylineToolClient(args[0], "Example Interactive Tool"); // Not L10N
                _toolClient.DocumentChanged += OnDocumentChanged;
            }

            _selectedReplicate = "All"; // Not L10N
            replicatesToolStripMenuItem.DropDownItemClicked += ItemClicked;

            // Create a graph and fill it with data.
            _graph = new Graph(graph);
            _graph.Click += GraphClick;
            CreateGraph();
        }

        private void GraphClick(object sender, ClickEventArgs e)
        {
            // Select the peptide in Skyline when the user clicks on it.
            _toolClient.Select(_selectedReplicate == "All" ? _peptideLinks[e.Index] : _replicateLinks[e.Index]); // Not L10N
        }

        private void CreateGraph()
        {
            if (_toolClient == null)
                return;

            // Retrieve the current report.
            var report = _toolClient.GetReport("Peak Area"); // Not L10N
            _peptides = new List<string>();
            _peptideLinks = new List<string>();
            _replicateLinks = new List<string>();
            var replicates = new List<string>();
            var peptideAreas = new Dictionary<string, double>();

            foreach (var row in report.Cells)
            {
                // Get report fields.
                var peptideName = row[0];
                var peakArea = row[1];
                var peptideLocation = row[2];
                var replicateName = row[3];
                var replicateLocation = row[4];

                // Get the name of this peptide.
                double area;
                if (string.IsNullOrWhiteSpace(peptideName) || !double.TryParse(peakArea, out area))
                    continue;

                if (_selectedReplicate == replicateName || _selectedReplicate == "All") // Not L10N
                {
                    // Add area to sum that was previously created.
                    if (peptideAreas.ContainsKey(peptideName))
                        peptideAreas[peptideName] += area;

                    // Create a sum for a new peptide.
                    else
                    {
                        _peptides.Add(peptideName);
                        _peptideLinks.Add(peptideLocation);
                        _replicateLinks.Add(replicateLocation);
                        peptideAreas[peptideName] = area;
                    }
                }

                // Record unique replicate names.
                if (!replicates.Contains(replicateName))
                    replicates.Add(replicateName);
            }

            // Rebuild Replicates menu.
            var items = replicatesToolStripMenuItem.DropDownItems;
            items.Clear();
            items.Add("All"); // Not L10N
            items.Add("-"); // Not L10N
            replicates.Sort();
            foreach (var replicate in replicates)
                items.Add(replicate);

            // Put a check on the selected replicate.
            if (!replicates.Contains(_selectedReplicate))
                _selectedReplicate = "All"; // Not L10N
            SelectReplicate(_selectedReplicate);

            // Create array of peak areas in same order as peptide names.
            var areas = new double[_peptides.Count];
            for (int i = 0; i < areas.Length; i++)
                areas[i] = peptideAreas[_peptides[i]];

            // Generate unique prefixes for each peptide name.
            var prefixGenerator = new UniquePrefixGenerator(_peptides, 3);

            // Create bar graph showing summed peak area for each peptide.
            _graph.CreateBars(_peptides.Select(prefixGenerator.GetUniquePrefix).ToArray(), areas);
        }

        private void SelectReplicate(string replicateName)
        {
            _selectedReplicate = replicateName;
            foreach (var item in replicatesToolStripMenuItem.DropDownItems)
            {
                var toolStripItem = item as ToolStripMenuItem;
                if (toolStripItem != null)
                    toolStripItem.Checked = (toolStripItem.Text == _selectedReplicate);
            }
        }

        void ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            SelectReplicate(e.ClickedItem.Text);
            CreateGraph();
        }

        private void OnDocumentChanged(object sender, EventArgs eventArgs)
        {
            // Create graph on UI thread.
            Invoke(new Action(CreateGraph));
        }

        private void InfoClick(object sender, EventArgs e)
        {
            if (_toolClient == null)
                return;
            var version = _toolClient.SkylineVersion;
            MessageBox.Show(string.Format("Skyline version: {0}.{1}.{2}.{3}\nDocument: {4}", // Not L10N
                version.Major, version.Minor, version.Build, version.Revision,
                _toolClient.DocumentPath));
        }
    }
}
