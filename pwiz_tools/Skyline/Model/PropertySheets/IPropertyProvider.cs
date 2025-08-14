/*
 * Original author: Aaron Banse <acbanse .at. icloud.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.PropertySheets
{
    public interface IPropertyProvider
    {
        /// <summary>
        /// Should be called by the implementer when the selection changes and when the owner gains focus.
        /// Implement to invoke <see cref="SkylineWindow.PotentialPropertySheetOwnerGotFocus"/> with the proper arguments.
        /// </summary>
        /// <param name="skylineWindow">The main SkylineWindow instance to notify.</param>
        /// <param name="e">The event args associated with the selection/focus change.</param>
        void NotifyPropertySheetOwnerGotFocus(SkylineWindow skylineWindow, EventArgs e);

        public GlobalizedObject GetSelectedObjectProperties();
    }
}
