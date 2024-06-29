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
namespace ResourcesOrganizer.ResourcesModel
{
    public record LocalizationCsvRecord
    {
        public string Name { get; init; } = string.Empty;
        public string Comment { get; init; } = string.Empty;
        public string English { get; init; } = string.Empty;
        public string Translation { get; init; } = string.Empty;
        public string Issue { get; init; } = string.Empty;
        public string OldEnglish { get; init; } = string.Empty;
        public string OldLocalized { get; init; } = string.Empty;
        public int FileCount { get; init; }
        public string File { get; init; } = string.Empty;
    }
}
