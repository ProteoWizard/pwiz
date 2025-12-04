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

using System.Diagnostics.CodeAnalysis;

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

        
        /// <summary>
        /// Constructor used by <see cref="CsvHelper.CsvReader.GetRecords{T}()"/>
        /// The names of the constructor parameters must exactly match the column names in the CSV file.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public LocalizationRecord(string TutorialName, string XPath, string English, string Localized,
            string OriginalEnglish)
        {
            this.TutorialName = TutorialName;
            this.XPath = XPath;
            this.English = English;
            this.Localized = Localized;
            this.OriginalEnglish = OriginalEnglish;
        }
        public string TutorialName { get; private set; }
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
