using System.ComponentModel;

namespace pwiz.Common.DataBinding.RowSources
{
    /// <summary>
    /// Interface which has a ListChanged event, but which 
    /// is less complicated than <see cref="IBindingList"/>.
    /// </summary>
    public interface IListChanged
    {
        event ListChangedEventHandler ListChanged;
    }
}
