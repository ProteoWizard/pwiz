using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedBatch.Properties;

namespace SharedBatch
{
    public class MainFormUtils
    {
        public static void OpenFileExplorer(string configName, bool configValid, string folderDescription, string folderPath, IMainUiControl mainUi)
        {
            if (CanOpen(configName, configValid, folderDescription, mainUi))
            {
                Process.Start("explorer.exe", "/n," + folderPath);
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
        private int[] _columnWidths;
        private int _listViewWidth;

        public ColumnWidthCalculator(int[] initialColumnWidths)
        {
            _listViewWidth = initialColumnWidths.Sum();
            _columnWidths = new int[initialColumnWidths.Length];
            initialColumnWidths.CopyTo(_columnWidths, 0);
        }

        public int Get(int index)
        {
            return _columnWidths[index];
        }

        public void ListViewContainerResize(int newWidth)
        {
            for (int i = 0; i < _columnWidths.Length - 1; i++)
            {
                _columnWidths[i] = Math.Max(10, (int) Math.Floor(((double) _columnWidths[i] / _listViewWidth) * newWidth));
            }
            _columnWidths[_columnWidths.Length - 1] = 0;
            _columnWidths[_columnWidths.Length - 1] = newWidth - _columnWidths.Sum();
            _listViewWidth = newWidth;
        }

        public void WidthsChangedByUser(int[] changedColumnWidths)
        {
            for (int i = 0; i < _columnWidths.Length - 1; i++)
            {
                if (_columnWidths[i] != changedColumnWidths[i])
                {
                    if (changedColumnWidths[i] - _columnWidths[i] > _columnWidths[i + 1]) return;
                    _columnWidths[i + 1] += _columnWidths[i] - changedColumnWidths[i];
                    _columnWidths[i] = changedColumnWidths[i];
                    return;
                }
            }
        }
    }
}
