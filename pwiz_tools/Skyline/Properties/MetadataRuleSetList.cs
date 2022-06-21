/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Properties
{
    public class MetadataRuleSetList : SettingsList<MetadataRuleSet>
    {
        public override IEnumerable<MetadataRuleSet> GetDefaults(int revisionIndex)
        {
            yield break;
        }

        public override string Title
        {
            get { return Resources.MetadataRuleSetList_Title_Rule_Sets; }
        }
        public override string Label
        {
            get { return Resources.MetadataRuleSetList_Label_Rule_Set; }
        }

        public override MetadataRuleSet CopyItem(MetadataRuleSet item)
        {
            return item.ChangeName(string.Empty);
        }

        public override MetadataRuleSet EditItem(Control owner, MetadataRuleSet item, IEnumerable<MetadataRuleSet> existing, object tag)
        {
            var documentContainer = (IDocumentContainer) tag;
            using (var dlg = new MetadataRuleSetEditor(documentContainer, item, existing))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    return dlg.MetadataRuleSet;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}