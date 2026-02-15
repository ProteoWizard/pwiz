/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util.Extensions;
using TestRunnerLib.PInvoke;

namespace pwiz.SkylineTestUtil
{
    public class MultiFormActivator : IDisposable
    {
        private readonly List<Form> _formsToActivate;

        public MultiFormActivator(params Form[] formsToActivate)
        {
            _formsToActivate = formsToActivate.ToList();
            foreach (var form in formsToActivate)
            {
                form.Activated += FormActivated;
            }
        }

        public void Reset(params Form[] formsToActivate)
        {
            Clear();
            foreach (var form in formsToActivate)
            {
                AddForm(form);
            }
        }

        public void AddForm(Form form)
        {
            lock (_formsToActivate)
            {
                form.Activated += FormActivated;
                _formsToActivate.Add(form);
            }
        }

        // This approach causes cross-thread exceptions
        // public void AddRange(IEnumerable<Form> forms)
        // {
        //     lock (_formsToActivate)
        //     {
        //         // Add only forms that do not parent another form. Track and activate only the
        //         // deepest child forms. They are the ones that will get activated and bringing them
        //         // forward will drag their parents with them.
        //         var listForms = forms.ToArray();
        //         var setParents = new HashSet<IntPtr>(listForms
        //             .Where(f => f.Parent != null)
        //             .Select(f => f.Parent.Handle));
        //         foreach (var form in listForms.Where(f => !setParents.Contains(f.Handle)))
        //             AddForm(form);
        //     }
        // }

        public void RemoveForm(Form form)
        {
            lock (_formsToActivate)
            {
                form.Activated -= FormActivated;
                _formsToActivate.Remove(form);
            }
        }

        public void Clear()
        {
            lock (_formsToActivate)
            {
                foreach (var form in _formsToActivate.ToArray())
                    RemoveForm(form);
            }
        }

        private void FormActivated(object sender, EventArgs e)
        {
            var activatedForm = (Form)sender;
            // DockableForms can get activated during DockableForm.Dispose()
            if (!activatedForm.IsHandleCreated)
                return;

            // Record the handle value for the activated form while on its thread.
            // It will not be possible to get this handle from the Form object on
            // any other thread without causing a CrossThreadOperationException
            var activatedFormHandle = activatedForm.Handle;

            lock (_formsToActivate)
            {
                foreach (var form in _formsToActivate.Where(form => !ReferenceEquals(form, activatedForm)))
                {
                    ActionUtil.RunAsync(() => ShowForm(form, activatedFormHandle));
                }
            }
        }

        private void ShowForm(Form form, IntPtr referenceFormHandle)
        {
            if (!form.IsHandleCreated)
                return;

            // Must be done on the form's thread
            form.Invoke((Action)(() =>
            {
                if (form.Visible)
                    form.BringWindowToSameLevelWithoutActivating(referenceFormHandle);
            }));
        }

        public void Dispose()
        {
            lock (_formsToActivate)
            {
                while (_formsToActivate.Any())
                    RemoveForm(_formsToActivate.First());
            }
        }
    }
}