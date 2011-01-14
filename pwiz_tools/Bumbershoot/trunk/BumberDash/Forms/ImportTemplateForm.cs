using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BumberDash.Model;
using NHibernate;

namespace BumberDash.Forms
{
    public sealed partial class ImportTemplateForm : Form
    {
        /// <summary>
        /// Takes main session and sets up currently avaialble templates
        /// to allow for specified exports
        /// </summary>
        /// <param name="session"></param>
        public ImportTemplateForm(ISession session)
        {
            InitializeComponent();
            
            var templateList = session.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
            foreach (var item in templateList)
            {
                AvailableDGV.Rows.Add(new[] { item.Name, item.DestinationProgram });
                AvailableDGV.Rows[AvailableDGV.Rows.Count - 1].Tag = item;
            }

            Text = "Export";
            okButton.Text = "Export";
            OutputLabel.Text = "To Export:";
        }

        private readonly ISession _tempSession;
        /// <summary>
        /// Gets list of available templates from provited file
        /// </summary>
        /// <param name="fileToImport"></param>
        public ImportTemplateForm(string fileToImport)
        {
            InitializeComponent();

            try
            {
                var manager = SessionManager.CreateSessionFactory(fileToImport);
                _tempSession = manager.OpenSession();
                var templateList = _tempSession.QueryOver<ConfigFile>().Where(x => x.FilePath == "Template").List();
                foreach (var item in templateList)
                {
                    AvailableDGV.Rows.Add(new[] { item.Name, item.DestinationProgram });
                    AvailableDGV.Rows[AvailableDGV.Rows.Count - 1].Tag = item;
                }
            }
            catch
            {
                MessageBox.Show("Could not open instrument template database");
                return;
            }

        }

        /// <summary>
        /// Adds all templates to OutputDGV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddAllButton_Click(object sender, EventArgs e)
        {
            var rowlist = AvailableDGV.Rows.Cast<DataGridViewRow>().ToList();
            foreach (var row in rowlist)
                AvailableDGV.Rows.Remove(row);
            OutputDGV.Rows.AddRange(rowlist.ToArray());
        }

        /// <summary>
        /// Adds selected template to OutputDGV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TemplateAdd_Click(object sender, EventArgs e)
        {
            if (AvailableDGV.SelectedRows.Count > 0)
            {
                var activeRow = AvailableDGV.SelectedRows[0];
                AvailableDGV.Rows.Remove(activeRow);
                OutputDGV.Rows.Add(activeRow);
            }
        }

        /// <summary>
        /// Removes selected tempalte from OutputDGV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TemplateRemove_Click(object sender, EventArgs e)
        {
            if (OutputDGV.SelectedRows.Count > 0)
            {
                var activeRow = OutputDGV.SelectedRows[0];
                OutputDGV.Rows.Remove(activeRow);
                AvailableDGV.Rows.Add(activeRow);
            }
        }

        /// <summary>
        /// Removes all templates from OutputDGV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveAllButton_Click(object sender, EventArgs e)
        {
            var rowlist = OutputDGV.Rows.Cast<DataGridViewRow>().ToList();
            foreach (var row in rowlist)
                OutputDGV.Rows.Remove(row);
            AvailableDGV.Rows.AddRange(rowlist.ToArray());
        }

        /// <summary>
        /// Shows contents of selected template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AvailableDGV_SelectionChanged(object sender, EventArgs e)
        {
            if (AvailableDGV.SelectedRows.Count == 0
                || AvailableDGV.SelectedRows[0].Tag == null)
                return;
            
            OutputDGV.ClearSelection();
            ValueBox.Clear();
            foreach (var item in ((ConfigFile) AvailableDGV.SelectedRows[0].Tag).PropertyList)
                ValueBox.AppendText(item.Name + "-  " + item.Value + Environment.NewLine);
        }

        /// <summary>
        /// Shows contents of selected template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OutputDGV_SelectionChanged(object sender, EventArgs e)
        {
            if (OutputDGV.SelectedRows.Count == 0
                || OutputDGV.SelectedRows[0].Tag == null)
                return;

            AvailableDGV.ClearSelection();
            ValueBox.Clear();
            foreach (var item in ((ConfigFile) OutputDGV.SelectedRows[0].Tag).PropertyList)
                ValueBox.AppendText(item.Name + "-  " + item.Value + Environment.NewLine);
        }

        /// <summary>
        /// Returns list of output templates
        /// </summary>
        /// <returns></returns>
        internal List<ConfigFile> GetConfigs()
        {
            var list = from DataGridViewRow row in OutputDGV.Rows
                       select (ConfigFile) row.Tag;
            var newList = new List<ConfigFile>();
            foreach (var item in list)
            {
                var newconfig = new ConfigFile
                                    {
                                        Name = item.Name,
                                        DestinationProgram = item.DestinationProgram,
                                        FilePath = item.FilePath,
                                        PropertyList = new List<ConfigProperty>()
                                    };
                foreach (var property in item.PropertyList)
                    newconfig.PropertyList.Add(new ConfigProperty
                                                   {
                                                       Name = property.Name,
                                                       Type = property.Type,
                                                       Value = property.Value,
                                                       ConfigAssociation = newconfig
                                                   });
                newList.Add(newconfig);
            }
            return newList;
        }

        private void ImportTemplateForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_tempSession != null)
                _tempSession.Close();
        }

    }
}
