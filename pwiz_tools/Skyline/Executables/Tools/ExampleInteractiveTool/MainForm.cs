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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SkylineTool;

namespace ExampleInteractiveTool
{
    /// <summary>
    /// This is an example main window of an interactive Skyline tool.
    /// </summary>
    public partial class MainForm : Form
    {
        private SkylineToolClient _toolClient;
        private readonly Graph _graph;
        private readonly Graph _chromatogramGraph;
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
                _toolClient = new SkylineToolClient(args[0], "Example Interactive Tool"); // Not L10N
                _toolClient.DocumentChanged += OnDocumentChanged;
                _toolClient.SelectionChanged += OnSelectionChanged;
            }

            _selectedReplicate = "All"; // Not L10N
            replicatesToolStripMenuItem.DropDownItemClicked += ItemClicked;

            // Create a graph and fill it with data.
            _graph = new Graph(graph, "Peptide", "Peak Area"); // Not L10N
            _graph.Click += GraphClick;
            CreateGraph();

            // Create chromatogram graph.
            _chromatogramGraph = new ChromatogramGraph(chromatogramGraph, "Retention Time", "Intensity"); // Not L10N
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Load initial chromatogram when window loads.
            OnSelectionChanged(null, null);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            try
            {
                _toolClient.DocumentChanged -= OnDocumentChanged;
                _toolClient.SelectionChanged -= OnSelectionChanged;
                _toolClient.Dispose();
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            _toolClient = null;
        }

        /// <summary>
        /// Select a peptide in Skyline when the user clicks on the corresponding area bar.
        /// </summary>
        private void GraphClick(object sender, ClickEventArgs e)
        {
            // Select the peptide in Skyline when the user clicks on it.
            var documentLocation =
                DocumentLocation.Parse(_selectedReplicate == "All" ? _peptideLinks[e.Index] : _replicateLinks[e.Index]); // Not L10N
            _toolClient.SetDocumentLocation(documentLocation);
        }

        /// <summary>
        /// Choose a replicate.
        /// </summary>
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

        /// <summary>
        /// Choose a replicate from drop-down menu.
        /// </summary>
        void ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            SelectReplicate(e.ClickedItem.Text);
            CreateGraph();
        }

        /// <summary>
        /// Recreate graph when the Skyline document changes.
        /// </summary>
        private void OnDocumentChanged(object sender, EventArgs eventArgs)
        {
            // Create graph on UI thread.
            Invoke(new Action(CreateGraph));
        }

        /// <summary>
        /// Change the chromatogram graph when the selection changes.
        /// </summary>
        private void OnSelectionChanged(object sender, EventArgs eventArgs)
        {
            // Create graph on UI thread.
            var documentLocation = _toolClient.GetDocumentLocation();
            var chromatograms = _toolClient.GetChromatograms(documentLocation);
            var documentLocationName = _toolClient.GetDocumentLocationName();
            Invoke(new Action(() =>
                _chromatogramGraph.CreateChromatograms(chromatograms, documentLocationName)));
        }

        /// <summary>
        /// Show Skyline version information from within the tool.
        /// </summary>
        private void InfoClick(object sender, EventArgs e)
        {
            if (_toolClient == null)
                return;
            var version = _toolClient.GetSkylineVersion();
            MessageBox.Show(string.Format("Skyline version: {0}.{1}.{2}.{3}\nDocument: {4}", // Not L10N
                version.Major, version.Minor, version.Build, version.Revision,
                _toolClient.GetDocumentPath()));
        }

        /// <summary>
        /// Create bar graph showing peak areas.
        /// </summary>
        private void CreateGraph()
        {
            if (_toolClient == null)
                return;

            // Retrieve the current report.
            IReport report = _toolClient.GetReport("Peak Area"); // Not L10N

            // Get the same report, more dynamically.
            var reportStream = typeof(MainForm).Assembly.GetManifestResourceStream("ExampleInteractiveTool.tool_inf.ExampleTool_report.skyr"); // Not L10N
            if (reportStream == null)
                return;
            var reader = new StreamReader(reportStream);
            IReport report2 = _toolClient.GetReportFromDefinition(reader.ReadToEnd());
            AssertReportsEquals(report, report2);

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

        /// <summary>
        /// Make sure two reports are the same.
        /// </summary>
        // ReSharper disable once UnusedParameter.Local
        private static void AssertReportsEquals(IReport report1, IReport report2)
        {
            Debug.Assert(report1.Cells.Length == report2.Cells.Length);
            for (int iRow = 0; iRow < report1.Cells.Length; iRow++)
            {
                Debug.Assert(report1.Cells[iRow].SequenceEqual(report2.Cells[iRow]));
            }
        }

        private void selectEndNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _toolClient.SetDocumentLocation(null);
        }

        private void insertFASTAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            const string fasta =
                // ReSharper disable once NonLocalizedString
@">YAL001C TFC3 SGDID:S000000001, Chr I from 151168-151099,151008-147596, reverse complement, Verified ORF, ""Largest of six subunits of the RNA polymerase III transcription initiation factor complex (TFIIIC); part of the TauB domain of TFIIIC that binds DNA at the BoxB promoter sites of tRNA and similar genes; cooperates with Tfc6p in DNA binding""
MVLTIYPDELVQIVSDKIASNKGKITLNQLWDISGKYFDLSDKKVKQFVLSCVILKKDIE
VYCDGAITTKNVTDIIGDANHSYSVGITEDSLWTLLTGYTKKESTIGNSAFELLLEVAKS
GEKGINTMDLAQVTGQDPRSVTGRIKKINHLLTSSQLIYKGHVVKQLKLKKFSHDGVDSN
PYINIRDHLATIVEVVKRSKNGIRQIIDLKRELKFDKEKRLSKAFIAAIAWLDEKEYLKK
VLVVSPKNPAIKIRCVKYVKDIPDSKGSPSFEYDSNSADEDSVSDSKAAFEDEDLVEGLD
NFNATDLLQNQGLVMEEKEDAVKNEVLLNRFYPLQNQTYDIADKSGLKGISTMDVVNRIT
GKEFQRAFTKSSEYYLESVDKQKENTGGYRLFRIYDFEGKKKFFRLFTAQNFQKLTNAED
EISVPKGFDELGKSRTDLKTLNEDNFVALNNTVRFTTDSDGQDIFFWHGELKIPPNSKKT
PNKNKRKRQVKNSTNASVAGNISNPKRIKLEQHVSTAQEPKSAEDSPSSNGGTVVKGKVV
NFGGFSARSLRSLQRQRAILKVMNTIGGVAYLREQFYESVSKYMGSTTTLDKKTVRGDVD
LMVESEKLGARTEPVSGRKIIFLPTVGEDAIQRYILKEKDSKKATFTDVIHDTEIYFFDQ
TEKNRFHRGKKSVERIRKFQNRQKNAKIKASDDAISKKSTSVNVSDGKIKRRDKKVSAGR
TTVVVENTKEDKTVYHAGTKDGVQALIRAVVVTKSIKNEIMWDKITKLFPNNSLDNLKKK
WTARRVRMGHSGWRAYVDKWKKMLVLAIKSEKISLRDVEELDLIKLLDIWTSFDEKEIKR
PLFLYKNYEENRKKFTLVRDDTLTHSGNDLAMSSMIQREISSLKKTYTRKISASTKDLSK
SQSDDYIRTVIRSILIESPSTTRNEIEALKNVGNESIDNVIMDMAKEKQIYLHGSKLECT
DTLPDILENRGNYKDFGVAFQYRCKVNELLEAGNAIVINQEPSDISSWVLIDLISGELLN
MDVIPMVRNVRPLTYTSRRFEIRTLTPPLIIYANSQTKLNTARKSAVKVPLGKPFSRLWV
NGSGSIRPNIWKQVVTMVVNEIIFHPGITLSRLQSRCREVLSLHEISEICKWLLERQVLI
TTDFDGYWVNHNWYSIYEST*";

            _toolClient.ImportFasta(fasta);
        }

        private void addSpectralLibraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _toolClient.AddSpectralLibrary("Test library", dialog.FileName); // Not L10N
                }
            }
        }
    }
}
