/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// The Carafe model combo that Build Library's first page and the Osprey Search wizard's
    /// Library Model page share: the pretrained models, the saved fine-tuned models, Add...,
    /// Edit current... and Edit list..., over the document's Carafe model list. The list is
    /// filled when first asked for, since each saved model's display name reads its file, and a
    /// form may never show the combo. A fine-tune starts from the pretrained models, so while one
    /// is chosen the combo shows them, disabled, and the model chosen before comes back when the
    /// fine-tune is given up.
    /// </summary>
    public class CarafeModelCombo
    {
        private readonly ComboBox _combo;
        private SettingsListComboDriver<CarafeModelSpec> _driver;
        private CarafeModelSpec _modelBeforeTuning;

        public CarafeModelCombo(ComboBox combo)
        {
            _combo = combo;
        }

        private SettingsListComboDriver<CarafeModelSpec> Driver
        {
            get
            {
                if (_driver == null)
                {
                    _driver = new SettingsListComboDriver<CarafeModelSpec>(_combo, Settings.Default.CarafeModelList);
                    _driver.LoadList(null);
                }
                return _driver;
            }
        }

        /// <summary>Fills the combo, for a form about to show it.</summary>
        public void Load()
        {
            var unused = Driver;
        }

        /// <summary>
        /// The model chosen: the default, the pretrained AlphaPeptDeep models, or a saved
        /// fine-tuned model from the list; the default while a new model is fine-tuned. Setting
        /// it selects that model in the combo.
        /// </summary>
        public CarafeModelSpec Model
        {
            get => Driver.SelectedItem ?? CarafeModelSpec.DEFAULT;
            // The driver selects by the combo's text, which is the display name, not the key
            set => Driver.LoadList(Settings.Default.CarafeModelList.GetDisplayName(value));
        }

        /// <summary>
        /// The combo's SelectedIndexChanged, which Add, Edit current and Edit list fire again
        /// from within the driver's handling of them as the list reloads. True when the chosen
        /// model may have changed, for the form to react to.
        /// </summary>
        public bool SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_driver == null || _driver.IsInSelectedIndexChangedEvent)
                return false;
            _driver.SelectedIndexChangedEvent(sender, e);
            return true;
        }

        /// <summary>
        /// Fine-tuning starts from the pretrained models: the combo is set to them and disabled,
        /// so the form says what the tuning starts from, and the model chosen before comes back
        /// when the fine-tune is given up. (Osprey can tune a saved model further, but chaining
        /// fine-tunes compounds their drift, so it is not offered.)
        /// </summary>
        public void SetTuning(bool tune)
        {
            if (tune && _combo.Enabled)
            {
                _modelBeforeTuning = Model;
                Model = CarafeModelSpec.DEFAULT;
                _combo.Enabled = false;
            }
            else if (!tune && !_combo.Enabled)
            {
                _combo.Enabled = true;
                if (_modelBeforeTuning != null && Settings.Default.CarafeModelList.Contains(_modelBeforeTuning))
                    Model = _modelBeforeTuning;
            }
        }

        /// <summary>
        /// The combo's Add..., for tests: opens the model editor, which names a model file (a
        /// library's fine-tuned model, say) and adds it to the list, selected.
        /// </summary>
        public void AddItem()
        {
            Driver.AddItem();
        }

        /// <summary>The combo's Edit list..., for tests: opens the grid of models.</summary>
        public void EditList()
        {
            Driver.EditList();
        }
    }
}
