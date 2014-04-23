/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    public class ViewEditorWidget : UserControl
    {
        private readonly IList<ViewEditorWidget> _subEditors = new List<ViewEditorWidget>();
        public virtual void SetViewEditor(IViewEditor viewEditor)
        {
            if (ReferenceEquals(ViewEditor, viewEditor))
            {
                return;
            }
            if (null != ViewEditor)
            {
                ViewEditor.ViewChange -= ViewEditorOnViewChange;
                ViewEditor.PropertyPathActivated -= ViewEditorOnPropertyPathActivate;
            }
            ViewEditor = viewEditor;
            if (null != ViewEditor)
            {
                ViewEditor.ViewChange += ViewEditorOnViewChange;
                ViewEditor.PropertyPathActivated += ViewEditorOnPropertyPathActivate;
                ViewEditorOnViewChange(this, new EventArgs());
            }
            foreach (var subEditor in _subEditors)
            {
                subEditor.SetViewEditor(viewEditor);
            }
        }

        public IViewEditor ViewEditor { get; private set; }
        protected virtual bool UseTransformedView { get { return false; } }

        private void ViewEditorOnViewChange(object sender, EventArgs eventArgs)
        {
            if (InChangeView)
            {
                return;
            }
            try
            {
                InChangeView = true;
                OnViewChange();
            }
            finally
            {
                InChangeView = false;
            }
        }

        private void ViewEditorOnPropertyPathActivate(object sender, PropertyPathEventArgs eventArgs)
        {
            IEnumerable<PropertyPath> propertyPaths = new []{eventArgs.PropertyPath};
            if (UseTransformedView && ViewEditor.ViewTransformer != null)
            {
                propertyPaths = ViewEditor.ViewTransformer.TransformView(ViewInfo, propertyPaths).Value;
            }
            OnActivatePropertyPath(propertyPaths.First());
        }

        protected virtual void OnActivatePropertyPath(PropertyPath propertyPath)
        {
        }

        protected virtual void OnViewChange()
        {
        }

        protected bool InChangeView { get; private set; }
        public ViewInfo ViewInfo 
        {
            get
            {
                if (ViewEditor == null)
                {
                    return null;
                }
                if (!UseTransformedView || ViewEditor.ViewTransformer == null)
                {
                    return ViewEditor.ViewInfo;
                }
                return ViewEditor.ViewTransformer.TransformView(ViewEditor.ViewInfo, new PropertyPath[0]).Key;
            }
        }

        protected void SetViewInfo(ViewInfo viewInfo, IEnumerable<PropertyPath> selectedPaths)
        {
            if (UseTransformedView && null != ViewEditor.ViewTransformer)
            {
                var keyValuePair = ViewEditor.ViewTransformer.UntransformView(viewInfo, selectedPaths);
                viewInfo = keyValuePair.Key;
                selectedPaths = keyValuePair.Value;
            }
            ViewEditor.SetViewInfo(viewInfo, selectedPaths);
        }

        protected IEnumerable<PropertyPath> SelectedPaths
        {
            get
            {
                if (ViewEditor == null)
                {
                    return new PropertyPath[0];
                }
                if (UseTransformedView && ViewEditor.ViewTransformer != null)
                {
                    return ViewEditor.ViewTransformer.TransformView(ViewEditor.ViewInfo, ViewEditor.SelectedPaths).Value;
                }
                return ViewEditor.SelectedPaths;
            }
        }

        protected virtual IEnumerable<PropertyPath> GetSelectedPaths()
        {
            return null;
        }

        public ViewSpec ViewSpec
        {
            get
            {
                var viewInfo = ViewInfo;
                return viewInfo == null ? null : viewInfo.GetViewSpec();
            }
            set
            {
                if (Equals(ViewSpec, value))
                {
                    return;
                }
                SetViewSpec(value, GetSelectedPaths());
            }
        }

        protected void SetViewSpec(ViewSpec viewSpec, IEnumerable<PropertyPath> selectedPaths)
        {
            SetViewInfo(new ViewInfo(ViewInfo.ParentColumn, viewSpec), selectedPaths);
        }

        public IEnumerable<ViewEditorWidget> SubEditors
        {
            get
            {
                return _subEditors.AsEnumerable();
            }
        }

        public void AddSubEditor(ViewEditorWidget subEditor)
        {
            if (_subEditors.Contains(subEditor))
            {
                throw new ArgumentException("Editor already added"); // Not L10N
            }
            _subEditors.Add(subEditor);
            if (null != ViewEditor)
            {
                subEditor.SetViewEditor(ViewEditor);
            }
        }

        public void RemoveSubEditor(ViewEditorWidget subEditor)
        {
            if (!_subEditors.Remove(subEditor))
            {
                throw new ArgumentException("Editor not added"); // Not L10N
            }
            if (null != ViewEditor)
            {
                subEditor.SetViewEditor(null);
            }
        }

        protected void ActivatePropertyPath(PropertyPath propertyPath)
        {
            IEnumerable<PropertyPath> activatedPaths = new[] {propertyPath};
            if (UseTransformedView && ViewEditor.ViewTransformer != null)
            {
                activatedPaths = ViewEditor.ViewTransformer.TransformView(ViewInfo, activatedPaths).Value;
            }
            ViewEditor.ActivatePropertyPath(activatedPaths.First());
        }
    }
}
