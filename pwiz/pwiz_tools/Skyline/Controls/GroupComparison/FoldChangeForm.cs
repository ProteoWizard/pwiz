/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class FoldChangeForm : DockableFormEx
    {
        private IDocumentContainer _documentContainer;
        private string _groupComparisonName;
        private Form _owner;
        public FoldChangeForm()
        {
            InitializeComponent();
            Icon = Resources.Skyline;
        }

        public void SetBindingSource(FoldChangeBindingSource bindingSource)
        {
            FoldChangeBindingSource = bindingSource;
            string groupComparisonName = bindingSource.GroupComparisonModel.GroupComparisonName ??
                                         bindingSource.GroupComparisonModel.GroupComparisonDef.Name;
            SetGroupComparisonName(bindingSource.GroupComparisonModel.DocumentContainer, groupComparisonName);
        }

        public void SetGroupComparisonName(IDocumentContainer documentContainer, string groupComparisonName)
        {
            _documentContainer = documentContainer;
            _groupComparisonName = groupComparisonName;
            Text = TabText = GetTitle(groupComparisonName);
        }

        public virtual string GetTitle(string groupComparisonName)
        {
            if (string.IsNullOrEmpty(groupComparisonName))
            {
                return GroupComparisonStrings.FoldChangeForm_GetTitle_New_Group_Comparison;
            }
            return groupComparisonName;
        }

        public FoldChangeBindingSource FoldChangeBindingSource { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != FoldChangeBindingSource)
            {
                FoldChangeBindingSource.AddRef();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (null == FoldChangeBindingSource)
            {
                if (null != _documentContainer)
                {
                    FoldChangeBindingSource = FindOrCreateBindingSource(_documentContainer, _groupComparisonName);
                    if (IsHandleCreated)
                    {
                        FoldChangeBindingSource.AddRef();
                    }
                }
            }
            _owner = Owner;
            if (null != _owner)
            {
                _owner.FormClosed += OwnerFormClosed;
            }
        }

        private void OwnerFormClosed(object sender, EventArgs args)
        {
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (null != _owner)
            {
                _owner.FormClosed -= OwnerFormClosed;
            }
            Dispose();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != FoldChangeBindingSource)
            {
                FoldChangeBindingSource.Release();
            }
            base.OnHandleDestroyed(e);
        }

        protected override string GetPersistentString()
        {

            return base.GetPersistentString() + '|' + Uri.EscapeDataString(_groupComparisonName);
        }

        public static T FindForm<T>(IDocumentContainer documentContainer, string groupComparisonName) 
            where T : FoldChangeForm
        {
            foreach (var form in Application.OpenForms.OfType<T>())
            {
                var foldChangeBindingSource = form.FoldChangeBindingSource;
                if (null == foldChangeBindingSource)
                {
                    continue;
                }
                if (groupComparisonName == foldChangeBindingSource.GroupComparisonModel.GroupComparisonName
                    && ReferenceEquals(documentContainer, foldChangeBindingSource.GroupComparisonModel.DocumentContainer))
                {
                    return form;
                }
            }
            return null;
        }

        public bool SameBindingSource(FoldChangeForm foldChangeForm)
        {
            if (null != FoldChangeBindingSource && null != foldChangeForm.FoldChangeBindingSource)
            {
                return ReferenceEquals(FoldChangeBindingSource, foldChangeForm.FoldChangeBindingSource);
            }
            if (!ReferenceEquals(_documentContainer, foldChangeForm._documentContainer))
            {
                return false;
            }
            if (string.IsNullOrEmpty(_groupComparisonName))
            {
                return false;
            }
            return _groupComparisonName == foldChangeForm._groupComparisonName;
        }

        public static FoldChangeBindingSource FindOrCreateBindingSource(IDocumentContainer documentContainer,
            string groupComparisonName)
        {
            var form = FindForm<FoldChangeForm>(documentContainer, groupComparisonName);
            if (null != form)
            {
                return form.FoldChangeBindingSource;
            }

            return new FoldChangeBindingSource(new GroupComparisonModel(documentContainer, groupComparisonName));
        }

        public static FoldChangeForm RestoreFoldChangeForm(IDocumentContainer documentContainer, string persistentString)
        {
            var formContructors = new[]
            {
                FormConstructor.MakeFormConstructor(()=>new FoldChangeGrid()),
                FormConstructor.MakeFormConstructor(()=>new FoldChangeBarGraph()),
            };
            foreach (var formConstructor in formContructors)
            {
                string prefix = formConstructor.FormType.ToString() + '|';
                if (persistentString.StartsWith(prefix))
                {
                    string groupComparisonName = Uri.UnescapeDataString(persistentString.Substring(prefix.Length));
                    var form = formConstructor.Constructor();
                    form.SetGroupComparisonName(documentContainer, groupComparisonName);
                    return form;
                }
            }
            return null;
        }

        public static void CloseInapplicableForms(IDocumentContainer documentContainer)
        {
            var groupComparisonNames = new HashSet<string>(
                documentContainer.Document.Settings.DataSettings.GroupComparisonDefs.Select(def => def.Name));
            foreach (var form in Application.OpenForms.OfType<FoldChangeForm>())
            {
                if (!ReferenceEquals(documentContainer, form._documentContainer))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(form._groupComparisonName) &&
                    !groupComparisonNames.Contains(form._groupComparisonName))
                {
                    form.BeginInvoke(new Action(form.Close));
                }
            }
        }

        private class FormConstructor
        {
            public Type FormType { get; private set; }
            public Func<FoldChangeForm> Constructor { get; private set; }

            public static FormConstructor MakeFormConstructor<T>(Func<T> constructor) where T:FoldChangeForm
            {
                return new FormConstructor
                {
                    FormType = typeof (T),
                    Constructor = constructor
                };
            }
        }
    }
}
