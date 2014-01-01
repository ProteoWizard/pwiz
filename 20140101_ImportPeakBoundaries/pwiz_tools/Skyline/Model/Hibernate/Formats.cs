/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System;

namespace pwiz.Skyline.Model.Hibernate
{
    public static class Formats
    {
        public const String RETENTION_TIME = "0.##"; // Not L10N
        public const String PEAK_FOUND_RATIO = "0.##"; // Not L10N
        public const String STANDARD_RATIO = "0.####"; // Not L10N
        public const String GLOBAL_STANDARD_RATIO = "0.######"; // Not L10N TODO: scientific notation
        public const String PEAK_AREA = "0"; // Not L10N
        public const String PEAK_AREA_NORMALIZED = "0.####%"; // Not L10N
        public const String OPT_PARAMETER = "0.#"; // Not L10N
        public const String MASS_ERROR = "0.#"; // Not L10N
        public const String CV = "0.#%"; // Not L10N
    }
}
