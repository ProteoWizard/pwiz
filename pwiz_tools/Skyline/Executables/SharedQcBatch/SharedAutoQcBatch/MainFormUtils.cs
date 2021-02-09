using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedBatch
{
    class MainFormUtils
    {
    }


    public class ColumnWidthCalculator
    {
        private int[] _columnWidths;
        //private double[] _columnPercents;
        private int _listViewWidth;

        public ColumnWidthCalculator(int[] initialColumnWidths)
        {
            _listViewWidth = initialColumnWidths.Sum();
            _columnWidths = new int[initialColumnWidths.Length];
            initialColumnWidths.CopyTo(_columnWidths, 0);
            /*_columnPercents = new double[initialColumnWidths.Length];
            for (int i = 0; i < initialColumnWidths.Length - 1; i++)
                _columnPercents[i] = (double)initialColumnWidths[i] / _listViewWidth;
            _columnPercents[initialColumnWidths.Length - 1] = 1 - _columnPercents.Sum();*/
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
