/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

namespace TutorialLocalization
{
    /// <summary>
    /// Entry for text which needs to be localized
    /// </summary>
    public class LocalizationRecord
    {
        public LocalizationRecord(string tutorialName, string xPath, string english)
        {
            TutorialName = tutorialName;
            XPath = xPath;
            English = english;
        }
        public string TutorialName { get; }
        public string XPath { get; }
        public string English { get; }
        public string Localized { get; private set; }
        public string OriginalEnglish { get; private set; }

        public LocalizationRecord ChangeOriginalEnglish(string originalEnglish, string originalLocalized)
        {
            var localizationRecord = (LocalizationRecord)MemberwiseClone();
            if (originalEnglish != English)
            {
                localizationRecord.OriginalEnglish = originalEnglish;
            }
            localizationRecord.Localized = originalLocalized;
            return localizationRecord;
        }
    }
}
