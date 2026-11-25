/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Names of the UI modes that the user can switch between in Skyline. The images and labels
    /// the interface shows for them are UiModeChoices, in the Skyline UI.
    /// </summary>
    public static class UiModes
    {
        public const string PROTEOMIC = "proteomic";
        public const string SMALL_MOLECULES = "small_molecules";
        public const string MIXED = "mixed";

        public static readonly IEnumerable<string> ALL =
            ImmutableList.ValueOf(new[] {PROTEOMIC, SMALL_MOLECULES, MIXED});

        public static string FromDocumentType(SrmDocument.DOCUMENT_TYPE documentType)
        {
            switch (documentType)
            {
                case SrmDocument.DOCUMENT_TYPE.proteomic:
                    return PROTEOMIC;
                case SrmDocument.DOCUMENT_TYPE.small_molecules:
                    return SMALL_MOLECULES;
                default:
                    return MIXED;
            }
        }
    }
}
