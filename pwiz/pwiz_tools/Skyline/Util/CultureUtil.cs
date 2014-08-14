/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Resources;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Answers questions about Cultures that are available in Skyline
    /// </summary>
    public static class CultureUtil
    {
        /// <summary>
        /// Returns the language which actually be used if localized resources of
        /// the specified CultureInfo are requested.
        /// If the CultureInfo is a sub-language (e.g. "en-UK") which does not have
        /// any specific resources, then the parent language ("en") gets returned.
        /// If the language is not at all supported, then resources are handled
        /// by the InvariantCulture, which this method called "English".
        /// </summary>
        public static CultureInfo GetDisplayLanguage(CultureInfo cultureInfo)
        {
            while (null != cultureInfo)
            {
                if (IsDisplayLanguageSupported(cultureInfo))
                {
                    break;
                }
                cultureInfo = cultureInfo.Parent;
            }
            if (Equals(cultureInfo, CultureInfo.InvariantCulture))
            {
                return English;
            }
            return cultureInfo ?? English;
        }

        /// <summary>
        /// Returns the list of languages for which there are localized resources in Skyline.
        /// </summary>
        public static IEnumerable<CultureInfo> AvailableDisplayLanguages()
        {
            yield return English;
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                if (culture.Equals(English) || culture.Equals(CultureInfo.InvariantCulture))
                {
                    continue;
                }
                if (!IsDisplayLanguageSupported(culture))
                {
                    continue;
                }
                yield return culture;
            }
        }

        public static CultureInfo English { get { return CultureInfo.GetCultureInfo("en"); }} // Not L10N

        private static bool IsDisplayLanguageSupported(CultureInfo cultureInfo)
        {
            ResourceManager resourceManager = new ResourceManager(typeof(Properties.Resources));
            var resourceSet = resourceManager.GetResourceSet(cultureInfo, true, false);
            return null != resourceSet;
        }
    }
}
