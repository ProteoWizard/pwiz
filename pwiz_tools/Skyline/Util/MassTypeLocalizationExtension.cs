/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    public static class MassTypeLocalizationExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.ExportStrategyExtension_LOCALIZED_VALUES_Monoisotopic,
                    Resources.ExportStrategyExtension_LOCALIZED_VALUES_Average
                };
            }
        }
        public static string GetLocalizedString(this MassType val)
        {
            return LOCALIZED_VALUES[(int)val & (int)MassType.Average]; // Strip off bMassH, bHeavy
        }

        public static MassType GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<MassType>(enumValue, LOCALIZED_VALUES);
        }

        public static MassType GetEnum(string enumValue, MassType defaultValue)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultValue);
        }

    }
}