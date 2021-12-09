using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.FileUI
{
    public partial class FilterLibraryDlg : Form
    {

        private MspParser _parser;
        private Dictionary<string, HashSet<string>> _categoryDict;

        public FilterLibraryDlg()
        {
            InitializeComponent();
            var filePath = @"D:\henrytsanford\testing_files\Test library explorer files\smallmol.MSP";

            _parser = new MspParser(filePath);
            _categoryDict = _parser.CreateCategories();
            InitializeCategoryCombo();
            InitializeValueCombo();

        }

        private void InitializeCategoryCombo()
        {
            foreach (var category in _categoryDict.Keys)
            {
                comboCategories.Items.Add(category);
            }

            // Select the first item on the list
            comboCategories.SelectedIndex = 0;
        }

        private void InitializeValueCombo()
        {
            var category = comboCategories.SelectedItem.ToString();
            var values = _categoryDict[category];
            foreach (var value in values)
            {
                comboValues.Items.Add(value);
            }

            comboValues.SelectedIndex = 0;
        }
    }
}
