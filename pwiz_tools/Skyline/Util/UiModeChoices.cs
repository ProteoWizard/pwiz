/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// The UI modes (<see cref="UiModes"/>) as the interface offers them, with an image and a label.
    /// </summary>
    public static class UiModeChoices
    {
        public static IEnumerable<IUiModeInfo> AllModes
        {
            get
            {
                yield return new UiModeInfo(UiModes.PROTEOMIC, () => ImageResources.UIModeProteomic,
                    () => UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Proteomics_interface);
                yield return new UiModeInfo(UiModes.SMALL_MOLECULES, () => ImageResources.UIModeSmallMolecules,
                    () => UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Small_Molecules_interface);
                yield return new UiModeInfo(UiModes.MIXED, () => ImageResources.UIModeMixed,
                    () => UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Mixed_interface);
            }
        }

        public static IEnumerable<IUiModeInfo> AvailableModes(SrmDocument.DOCUMENT_TYPE documentType)
        {
            if (documentType == SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                return AllModes.Where(mode => mode.Name != UiModes.SMALL_MOLECULES);
            }

            if (documentType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                return AllModes.Where(mode => mode.Name != UiModes.PROTEOMIC);
            }

            return AllModes;
        }

        private class UiModeInfo : IUiModeInfo
        {
            private readonly Func<Image> _getImageFunc;
            private readonly Func<string> _getLabelFunc;
            public UiModeInfo(string name, Func<Image> getImageFunc, Func<string> getLabelFunc)
            {
                Name = name;
                _getImageFunc = getImageFunc;
                _getLabelFunc = getLabelFunc;
            }

            public string Name { get; private set; }
            public Image Image
            {
                get { return _getImageFunc(); }
            }
            public string Label { get {return _getLabelFunc();} }
            public Color TransparentColor
            {
                get { return Color.White; }
            }
        }
    }
}
