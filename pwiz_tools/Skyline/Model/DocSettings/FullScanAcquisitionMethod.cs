/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.DocSettings
{
    public class FullScanAcquisitionMethod
    {
        public static readonly FullScanAcquisitionMethod None = new FullScanAcquisitionMethod("None", // Not L10N
            ()=>Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_None);
        public static readonly FullScanAcquisitionMethod Targeted = new FullScanAcquisitionMethod("Targeted", // Not L10N
            ()=>Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_Targeted);

        public static readonly FullScanAcquisitionMethod DIA = new FullScanAcquisitionMethod("DIA", // Not L10N
            () => Resources.FullScanAcquisitionExtension_LOCALIZED_VALUES_DIA);
        public static readonly FullScanAcquisitionMethod DDA = new FullScanAcquisitionMethod("DDA", // Not L10N
            ()=>Resources.FullScanAcquisitionMethod_DDA_DDA);

        public static readonly ImmutableList<FullScanAcquisitionMethod> ALL =
            ImmutableList.ValueOf(new[] {None, Targeted, DIA, DDA});

        private readonly Func<string> _getLabelFunc;
        private FullScanAcquisitionMethod(string name, Func<string> getLabelFunc)
        {
            Name = name;
            _getLabelFunc = getLabelFunc;
        }

        public string Name {get; private set; }
        public string Label { get { return _getLabelFunc(); } }

        public override string ToString()
        {
            return Label;
        }

        public static FullScanAcquisitionMethod FromName(string name)
        {
            foreach (var method in ALL)
            {
                if (method.Name == name)
                {
                    return method;
                }
            }
            return None;
        }

        public static FullScanAcquisitionMethod FromLegacyName(string legacyName)    // Skyline 1.2 and earlier // Not L10N
        {
            if (legacyName == null)
            {
                return null;
            }
            if (legacyName == "Single") // Not L10N
            {
                return Targeted;
            }
            if (legacyName == "Multiple") // Not L10N
            {
                return DIA;
            }
            return None;
        }
    }
}