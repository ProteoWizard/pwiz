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

        void ActivatePropertyPath(PropertyPath propertyPath);
        event EventHandler<PropertyPathEventArgs> PropertyPathActivated;
        
        bool ShowHiddenFields { get; }
        IViewTransformer ViewTransformer { get; }
    }

    public class PropertyPathEventArgs : EventArgs
    {
        public PropertyPathEventArgs(PropertyPath propertyPath)
        {
            PropertyPath = propertyPath;
        }
        public PropertyPath PropertyPath { get; private set; }
    }
}
