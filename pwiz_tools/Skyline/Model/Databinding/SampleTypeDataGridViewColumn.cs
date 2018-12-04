using System;
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.Skyline.Model.Databinding
{
    public class SampleTypeDataGridViewColumn : DataGridViewComboBoxColumn
    {
        public SampleTypeDataGridViewColumn()
        {
            foreach (var sampleType in SampleType.ListSampleTypes())
            {
                Items.Add(new KeyValuePair<String, SampleType>(sampleType.ToString(), sampleType));
            }
            ValueMember = @"Value";
            DisplayMember = @"Key";
            DisplayStyleForCurrentCellOnly = true;
            FlatStyle = FlatStyle.Flat;
        }
    }
}
