using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.Controls
{
    /// <inheritdoc />
    /// <summary>
    /// This is a column which can be added to a DataGridView which shows a 
    /// warning that additional columns might not be shown.
    /// </summary>
    public class ColumnLimitExceededColumn : DataGridViewColumn
    {
        private int _columnsNotShownCount;
        public ColumnLimitExceededColumn(int columnsNotShownCount) : base(new DataGridViewTextBoxCell())
        {
            HeaderText = Resources.ColumnLimitExceededColumn_ColumnLimitExceededColumn_Column_Limit_Exceeded;
            ColumnsNotShownCount = columnsNotShownCount;
            FillWeight = 1;
        }

        public int ColumnsNotShownCount 
        { 
            get { return _columnsNotShownCount; }
            set
            {
                _columnsNotShownCount = value;
                CellTemplate.ErrorText = string.Format(
                    Resources.ColumnLimitExceededColumn_TruncatedColumnCount__0__additional_columns_not_shown_, 
                    ColumnsNotShownCount);
            }
        }

        public override bool ReadOnly
        {
            get { return true; }
            set { }
        }
    }
}
