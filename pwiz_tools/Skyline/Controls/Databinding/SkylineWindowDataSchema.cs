/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using System;

namespace pwiz.Skyline.Controls.Databinding
{
    /// <summary>
    /// Data schema associated with a SkylineWindow.
    /// </summary>
    public class SkylineWindowDataSchema : SkylineDataSchema
    {
        /// <summary>
        /// Returns either a SkylineDataSchema or a SkylineWindowDataSchema depending on whether
        /// the IDocumentContainer is a SkylineWindow.
        /// </summary>
        /// <param name="documentContainer"></param>
        /// <returns></returns>
        public static SkylineDataSchema FromDocumentContainer(IDocumentContainer documentContainer)
        {
            if (documentContainer is SkylineWindow skylineWindow)
            {
                return new SkylineWindowDataSchema(skylineWindow);
            }

            return new SkylineDataSchema(documentContainer, GetLocalizedSchemaLocalizer());
        }
        
        public SkylineWindowDataSchema(SkylineWindow skylineWindow) : this(skylineWindow, GetLocalizedSchemaLocalizer())
        {
        }
        public SkylineWindowDataSchema(SkylineWindow skylineWindow, DataSchemaLocalizer dataSchemaLocalizer) : base(
            skylineWindow, dataSchemaLocalizer)
        {
            SkylineWindow = skylineWindow;
        }

        public override SkylineWindow SkylineWindow { get; }

        protected override SrmDocument EndDeferSettingsChanges(SrmDocument document, SrmDocument originalDocument)
        {
            string message = Resources.DataGridViewPasteHandler_EndDeferSettingsChangesOnDocument_Updating_settings;
            using (var longWaitDlg = new LongWaitDlg
                   {
                       Message = message
                   })
            {
                SrmDocument newDocument = document;
                longWaitDlg.PerformWork(SkylineWindow, 1000, progressMonitor =>
                {
                    var srmSettingsChangeMonitor = new SrmSettingsChangeMonitor(progressMonitor,
                        message);
                    newDocument = document.EndDeferSettingsChanges(originalDocument, srmSettingsChangeMonitor);
                });
                return newDocument;
            }
        }

        protected override void AttachDocumentChangeEventHandler(EventHandler<DocumentChangedEventArgs> handler)
        {
            ((IDocumentUIContainer) SkylineWindow).ListenUI(handler);
        }

        protected override void DetachDocumentChangeEventHandler(EventHandler<DocumentChangedEventArgs> handler)
        {
            ((IDocumentUIContainer)SkylineWindow).UnlistenUI(handler);
        }
        /// <summary>
        /// Returns true if SkylineWindow.DocumentUI is the same as SkylineWindow.Document
        /// </summary>
        public override bool IsDocumentUpToDate()
        {

            if (!SkylineWindow.InvokeRequired)
            {
                return ReferenceEquals(SkylineWindow.DocumentUI, SkylineWindow.Document);
            }
            bool result = false;
            SkylineWindow.BeginInvoke(new Action(() =>
                result = ReferenceEquals(SkylineWindow.DocumentUI, SkylineWindow.Document)));
            return result;
        }
    }
}
