/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace pwiz.Skyline.Model
{
    [ProvideProperty("Mode", typeof(IComponent))]
    public partial class ModeComponent : UserControl, IExtenderProvider
    {
        private Dictionary<IComponent, SkylineWindow.UIMode> _modes =
                                   new Dictionary<IComponent, SkylineWindow.UIMode>();

 
        public bool CanExtend(object extendee)
        {
            return extendee is ToolStripMenuItem || extendee is Control;
        }

        [DefaultValue((SkylineWindow.UIMode)7)]
        [Editor(typeof(FlagEnumUIEditor), typeof(UITypeEditor))]
        [Category("Misc")]
        public SkylineWindow.UIMode GetMode(IComponent component)
        {
            SkylineWindow.UIMode mode;
            if (!_modes.TryGetValue(component, out mode))
            {
                return (SkylineWindow.UIMode) 7;
            }
            return mode;
        }

        public void SetMode(IComponent component, SkylineWindow.UIMode? value)
        {
            if (value.HasValue)
            {
                _modes[component] = value.Value;
            }
        }

        public void Render(SkylineWindow.UIMode selectedMode)
        {
                foreach (IComponent component in _modes.Keys)
                {
                    Control c = component as Control;
                    if (c == null)
                    {
                        ToolStripMenuItem m = component as ToolStripMenuItem;
                        if (m != null)
                        {
                            if (!_modes[component].HasFlag(selectedMode))
                                m.Visible = false;
                            else
                                m.Visible = true;
                        }
                    } else {
                        if (!_modes[component].HasFlag(selectedMode))
                            c.Hide();
                        else
                            c.Show();
                    }
                }
        }
    }
}
