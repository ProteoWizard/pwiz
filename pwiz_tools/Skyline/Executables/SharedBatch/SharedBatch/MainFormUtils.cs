using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SharedBatch.Properties;

namespace SharedBatch
{
    public class MainFormUtils
    {
        public static void OpenFileExplorer(string configName, bool configValid, string folderDescription, string folderPath, IMainUiControl mainUi)
        {
            if (CanOpen(configName, configValid, folderDescription, mainUi))
            {
                Process.Start(folderPath);
            }
        }


        public static bool CanOpen(string configName, bool configValid, string fileDescription, IMainUiControl mainUi)
        {
            if (!configValid)
            {
                mainUi.DisplayError(string.Format(Resources.MainFormUtils_OpenFileExplorer_Cannot_open_the__0__of_an_invalid_configuration_, fileDescription) + Environment.NewLine +
                                    string.Format(Resources.MainFormUtils_OpenFileExplorer_Please_fix___0___and_try_again_, configName));
            } 
            return configValid;
        }
    }


    public class ColumnWidthCalculator
    {
        private ListView _listView;
        private double[] _columnPercents; // ratio of column width to list view width (ie: column filling half the listView has value 0.5)
        private int _buffer = 5; // buffer on last column to prevent horizontal scrollbar appearing
        
        public ColumnWidthCalculator(ListView listView)
        {
            _listView = listView;

            _columnPercents = new double[listView.Columns.Count];
            for (int i = 0; i < _columnPercents.Length - 1; i++)
                _columnPercents[i] = (double)listView.Columns[i].Width / _listView.Width;
            _columnPercents[_columnPercents.Length - 1] = 1 - _columnPercents.Sum();
        }

        private int ScrollBarWidth()
        {
            if (_listView.Items.Count == 0) return 0;
            if (_listView.TopItem != _listView.Items[0] || _listView.Height < _listView.Items[_listView.Items.Count - 1].Bounds.Bottom + 10)
                return SystemInformation.VerticalScrollBarWidth;
            return 0;
        }

        private void UpdateListViewColumns()
        {
            // keeps the same column width ratios when the form is resized
            for (int i = 0; i < _listView.Columns.Count; i++)
                _listView.Columns[i].Width = Get(i);
        }

        private int Get(int index)
        {
            var width = (int)Math.Floor(_columnPercents[index] * _listView.Width);
            if (index == _columnPercents.Length - 1)
            {
                width -= ScrollBarWidth() + _buffer;
            }
            return width;
        }

        public void ListViewContainerResize()
        {
            UpdateListViewColumns();
        }

        public void WidthsChangedByUser()
        {
            for (int i = 0; i < _columnPercents.Length - 1; i++)
            {
                if (Get(i) != _listView.Columns[i].Width)
                {
                    // Only resize columns if it won't cover column to the right
                    if (_listView.Columns[i].Width - Get(i) < Get(i + 1))
                    {
                        _columnPercents[i + 1] += (double)(Get(i) - _listView.Columns[i].Width) / _listView.Width;
                        _columnPercents[i] = (double)_listView.Columns[i].Width / _listView.Width;
                    }
                    _columnPercents[_columnPercents.Length - 1] += 1 - _columnPercents.Sum(); // columnPercents must add up to 1
                    break;
                }
            }
            UpdateListViewColumns();
        }
    }
}
