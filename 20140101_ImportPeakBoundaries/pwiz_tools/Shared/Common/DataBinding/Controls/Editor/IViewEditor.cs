using System;
using System.Collections.Generic;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public interface IViewEditor
    {
        ViewInfo ViewInfo { get; }
        IEnumerable<PropertyPath> SelectedPaths { get; }
        void SetViewInfo(ViewInfo viewInfo, IEnumerable<PropertyPath> selectectedPaths);
        event EventHandler ViewChange;
        bool ShowHiddenFields { get; }
        IViewTransformer ViewTransformer { get; }
    }
}
