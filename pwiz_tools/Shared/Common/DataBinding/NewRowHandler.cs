namespace pwiz.Common.DataBinding
{
    public interface INewRowHandler
    {
        RowItem AddNewRow();
        RowItem CommitAddNew(RowItem rowItem);
        bool IsNewRowEmpty(RowItem rowItem);
        bool ValidateNewRow(RowItem rowItem, out bool cancel);
    }
}
