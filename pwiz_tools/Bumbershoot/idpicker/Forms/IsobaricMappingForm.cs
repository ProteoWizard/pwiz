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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BrightIdeasSoftware;
using NHibernate;
using NHibernate.Linq;
using IDPicker.DataModel;
using pwiz.Common.Collections;
using System.Text;

namespace IDPicker.Forms
{
    public partial class IsobaricMappingForm : Form
    {
        NHibernate.ISession session;
        AutoCompleteStringCollection acStrings;
        Dictionary<QuantitationMethod, DataGridView> isobaricMappingTables;

        public IsobaricMappingForm(NHibernate.ISessionFactory sessionFactory)
        {
            InitializeComponent();

            this.session = sessionFactory.OpenSession();

            acStrings = new AutoCompleteStringCollection();
            acStrings.Add("Reference");
            acStrings.Add("Empty");

            isobaricMappingTables = new Dictionary<QuantitationMethod, DataGridView>();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // get the list of sources, the group they're in, and their quantitation method
            var spectrumSources = session.Query<SpectrumSource>().ToList();
            var sourcesByGroup = spectrumSources.GroupBy(o => o.Group);

            // find groups with more than 1 quantitation method; put these in a separate list for "invalid" groups
            var allGroupsByMethod = sourcesByGroup.GroupBy(o => o.Select(o2 => o2.QuantitationMethod));
            var invalidGroups = allGroupsByMethod.Where(o => o.Count() > 1).SelectMany(o2 => o2.Select(o3 => o3.Key));

            // divide valid source groups by quantitation method
            var validGroupsByMethod = sourcesByGroup.Where(o => !invalidGroups.Contains(o.Key)).GroupBy(o => o.Select(o2 => o2.QuantitationMethod).Distinct().Single());

            if (validGroupsByMethod.Any())
            {
                var existingIsobaricSampleMapping = Embedder.GetIsobaricSampleMapping(session.Connection.GetDataSource());

                // specialize the default mapping table for each quantitation method
                foreach (var method in validGroupsByMethod)
                {
                    var dgv = isobaricMappingDataGridView.CloneAsDesigned();
                    dgv.EditingControlShowing += isobaricMappingDataGridView_EditingControlShowing;
                    isobaricMappingDataGridView.Columns.Cast<DataGridViewColumn>().ForEach(o => dgv.Columns.Add((DataGridViewColumn)o.Clone()));

                    if (method.Key == QuantitationMethod.ITRAQ4plex)
                        foreach (int ion in new int[] { 114, 115, 116, 117 })
                        {
                            var column = new DataGridViewTextBoxColumn
                            {
                                HeaderText = "iTRAQ-" + ion.ToString(),
                                Name = "iTRAQ-" + ion.ToString() + "Column",
                                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                                FillWeight = 0.25f,
                                
                            };
                            dgv.Columns.Add(column);
                        }
                    else if (method.Key == QuantitationMethod.ITRAQ8plex)
                        foreach (int ion in new int[] { 113, 114, 115, 116, 117, 118, 119, 121 })
                        {
                            var column = new DataGridViewTextBoxColumn
                            {
                                HeaderText = "iTRAQ-" + ion.ToString(),
                                Name = "iTRAQ-" + ion.ToString() + "Column",
                                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                                FillWeight = 0.125f
                            };
                            dgv.Columns.Add(column);
                        }
                    else if (method.Key == QuantitationMethod.TMT2plex)
                        foreach (string ion in new string[] { "126", "127" })
                        {
                            var column = new DataGridViewTextBoxColumn
                            {
                                HeaderText = "TMT-" + ion,
                                Name = "TMT-" + ion + "Column",
                                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                                FillWeight = 0.5f
                            };
                            dgv.Columns.Add(column);
                        }
                    else if (method.Key == QuantitationMethod.TMT6plex)
                        foreach (string ion in new string[] { "126", "127", "128", "129", "130", "131" })
                        {
                            var column = new DataGridViewTextBoxColumn
                            {
                                HeaderText = "TMT-" + ion,
                                Name = "TMT-" + ion + "Column",
                                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                                FillWeight = 0.1666f
                            };
                            dgv.Columns.Add(column);
                        }
                    else if (method.Key != QuantitationMethod.None && method.Key != QuantitationMethod.LabelFree)
                    {
                        var tmtIonsTo16 = new [] { "126", "127N", "127C", "128N", "128C", "129N", "129C", "130N", "130C", "131N", "131C", "132N", "132C", "133N", "133C", "134" };
                        var tmtIons = new Func<IEnumerable<string>>(() =>
                        {
                            switch (method.Key)
                            {
                                case QuantitationMethod.TMT10plex: return tmtIonsTo16.Take(10);
                                case QuantitationMethod.TMT11plex: return tmtIonsTo16.Take(11);
                                case QuantitationMethod.TMTpro16plex: return tmtIonsTo16;
                                default: throw new ArgumentException("QuantitationMethod must be TMT 10-16");
                            }
                        })();
                        foreach (string ion in tmtIons)
                        {
                            var column = new DataGridViewTextBoxColumn
                            {
                                HeaderText = "TMT-" + ion,
                                Name = "TMT-" + ion + "Column",
                                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                                FillWeight = 0.1f
                            };
                            dgv.Columns.Add(column);
                        }}

                    // add source groups to the table
                    foreach (var group in method)
                    {
                        int newRowIndex = dgv.Rows.Add(group.Key.Name);

                        if (existingIsobaricSampleMapping.ContainsKey(group.Key.Name))
                        {
                            var existingSampleNames = existingIsobaricSampleMapping[group.Key.Name];
                            if (existingSampleNames.Count != dgv.Columns.Count - 1)
                                continue;

                            for (int i = 0; i < existingSampleNames.Count; ++i)
                                dgv.Rows[newRowIndex].Cells[i + 1].Value = existingSampleNames[i];
                        }
                    }

                    var tab = new TabPage(method.Key.ToString());
                    tab.Controls.Add(dgv);
                    quantitationMethodsTabPanel.TabPages.Add(tab);
                    isobaricMappingTables.Add(method.Key, dgv);
                }
                quantitationMethodsTabPanel.TabPages.Remove(noIsobaricMethodsTabPage);
            }
            quantitationMethodsTabPanel.TabPages.Remove(isobaricSampleMappingTabPage);
        }

        private void miReadAssembleTxt_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
                          {
                              CheckPathExists = true,
                              Filter = "Assemble.txt file|*.txt|All files|*.*",
                              FilterIndex = 0,
                              Title = "Select a text file describing your source hierarchy."
                          };

            if (ofd.ShowDialog(this) == DialogResult.Cancel)
                return;

            applyAssemblyText(session, ofd.FileName);
        }

        private List<SpectrumSourceGroup> applyAssemblyText(ISession session, string filepath)
        {
            var spectrumSources = session.Query<SpectrumSource>().ToList();
            var sourcesByGroup = new Map<string, List<SpectrumSource>>();
            var alreadyGroupedSources = new Set<string>();
            var sourceGroups = new List<SpectrumSourceGroup>();

            // open the assembly.txt file
            using (var assembleTxtFile = File.OpenText(filepath))
            {
                string line;
                while ((line = assembleTxtFile.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;

                    try
                    {
                        Regex groupFilemaskPair = new Regex("((\"(.+)\")|(\\S+))\\s+((\"(.+)\")|(\\S+))");
                        Match lineMatch = groupFilemaskPair.Match(line);
                        string group = lineMatch.Groups[3].ToString() + lineMatch.Groups[4].ToString();
                        string filemask = lineMatch.Groups[7].ToString() + lineMatch.Groups[8].ToString();

                        // for wildcards, use old style behavior
                        if (filemask.IndexOfAny("*?".ToCharArray()) > -1)
                        {
                            if (!Path.IsPathRooted(filemask))
                                filemask = Path.Combine(Path.GetDirectoryName(filepath), filemask);

                            if (!sourcesByGroup.Contains(group))
                                sourcesByGroup[group] = new List<SpectrumSource>();

                            if (!Directory.Exists(Path.GetDirectoryName(filemask)))
                                continue;

                            var files = Directory.GetFiles(Path.GetDirectoryName(filemask), Path.GetFileName(filemask));
                            var sourceNames = files.Select(o => Path.GetFileNameWithoutExtension(o));
                            foreach (string sourceName in sourceNames)
                            {
                                var spectrumSource = spectrumSources.SingleOrDefault(o => o.Name == sourceName);
                                if (spectrumSource == null)
                                    continue;

                                var insertResult = alreadyGroupedSources.Insert(sourceName);
                                if (insertResult.WasInserted)
                                    sourcesByGroup[group].Add(spectrumSource);
                            }
                        }
                        else
                        {
                            // otherwise, match directly to source names
                            string sourceName = Path.GetFileNameWithoutExtension(filemask);
                            var spectrumSource = spectrumSources.SingleOrDefault(o => o.Name == sourceName);
                            if (spectrumSource == null)
                                continue;

                            var insertResult = alreadyGroupedSources.Insert(sourceName);
                            if (insertResult.WasInserted)
                                sourcesByGroup[group].Add(spectrumSource);
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.HandleException(new Exception("Error reading assembly text from \"" + filepath + "\": " + ex.Message, ex));
                    }
                }
            }
            return null;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            var allWarnings = new StringBuilder();
            var allBlankCells = new List<DataGridViewCell>();

            // validate that all channels have names
            foreach (var methodTablePair in isobaricMappingTables)
            {
                var tableWarnings = new StringBuilder();
                foreach (var row in methodTablePair.Value.Rows.Cast<DataGridViewRow>())
                {
                    var blankCells = row.Cells.Cast<DataGridViewCell>().Where(o => o.Value == null || o.Value.ToString() == String.Empty);
                    var blankChannels = blankCells.Select(o => methodTablePair.Value.Columns[o.ColumnIndex].HeaderText);
                    if (blankChannels.Any())
                        tableWarnings.AppendFormat("\t{0}: {1}\r\n", row.Cells[0].Value, String.Join(", ", blankChannels));
                    allBlankCells.AddRange(blankCells);
                }
                if (tableWarnings.Length > 0)
                    allWarnings.AppendFormat("Some sample names for {0} were left blank:\r\n{1}\r\n\r\nDo you want to assign these channels to Empty?", methodTablePair.Key, tableWarnings);
            }

            if (allWarnings.Length > 0)
            {
                if (MessageBox.Show(this, allWarnings.ToString(), "Warning", MessageBoxButtons.YesNo) == DialogResult.No)
                    return;
                allBlankCells.ForEach(o => o.Value = "Empty");
            }

            IDictionary<string, IList<string>> isobaricSampleMapping = new Dictionary<string, IList<string>>();

            foreach (var methodTablePair in isobaricMappingTables)
            {
                foreach (var row in methodTablePair.Value.Rows.Cast<DataGridViewRow>())
                {
                    var sourceGroup = row.Cells[0].Value.ToString();
                    isobaricSampleMapping.Add(sourceGroup, new List<string>(row.Cells.Cast<DataGridViewCell>().Skip(1).Select(o => o.Value.ToString())));
                }
            }
            
            var idpDbFilepath = session.Connection.GetDataSource();
            var existingIsobaricSampleMapping = Embedder.GetIsobaricSampleMapping(idpDbFilepath);

            if (!existingIsobaricSampleMapping.SequenceEqual(isobaricSampleMapping))
            {
                Embedder.EmbedIsobaricSampleMapping(idpDbFilepath, isobaricSampleMapping);
                DialogResult = DialogResult.OK;
            }
            else
                DialogResult = DialogResult.Cancel;

            Close();
        }

        private void isobaricMappingDataGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox)
            {
                TextBox _with1 = (TextBox) e.Control;
                _with1.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                _with1.AutoCompleteSource = AutoCompleteSource.CustomSource;
                _with1.AutoCompleteCustomSource = acStrings;
            }
        }
    }
}
