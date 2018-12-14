/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding
{
    public class FormatSuggestion : Immutable
    {
        private readonly Func<string> _getLabelFunc;
        public FormatSuggestion(string formatString, Func<string> getLabelFunc)
        {
            FormatString = formatString;
            CultureInfo = CultureInfo.InvariantCulture;
            _getLabelFunc = getLabelFunc;
        }

        public string FormatString { get; private set; }
        public CultureInfo CultureInfo { get; private set; }

        public FormatSuggestion ChangeCultureInfo(CultureInfo newCultureInfo)
        {
            return ChangeProp(ImClone(this), im => im.CultureInfo = newCultureInfo);
        }

        public override string ToString()
        {
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            var oldUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = CultureInfo;
                return _getLabelFunc();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = oldUiCulture;
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }
        }

        // ReSharper disable LocalizableElement
        public static readonly FormatSuggestion FullPrecision = new FormatSuggestion("R", 
            ()=>TextUtil.SpaceSeparate(Resources.FormatSuggestion_FullPrecision_Full_Precision, Math.PI.ToString("R")));
        public static readonly FormatSuggestion Scientific = new FormatSuggestion("0.0000E+0", ()=>TextUtil.SpaceSeparate(Resources.FormatSuggestion_Scientific_Scientific, Math.PI.ToString("0.0000E+0")));
        public static readonly FormatSuggestion Integer = new FormatSuggestion("0", ()=>TextUtil.SpaceSeparate(Resources.FormatSuggestion_Integer_Integer, Math.PI.ToString("0")));
        public static readonly FormatSuggestion Percent = new FormatSuggestion("0.#%", ()=>TextUtil.SpaceSeparate(Resources.FormatSuggestion_Percent_Percent, Math.PI.ToString("0.#%")));
        // ReSharper restore LocalizableElement
        private static ImmutableList<FormatSuggestion> ALL = ImmutableList.ValueOf(new[]
        {
            FullPrecision, Scientific, Integer, Percent
        });

        public static IEnumerable<FormatSuggestion> ListFormatSuggestions(CultureInfo cultureInfo)
        {
            return ALL.Select(formatSuggestion => formatSuggestion.ChangeCultureInfo(cultureInfo));
        }
    }
}
