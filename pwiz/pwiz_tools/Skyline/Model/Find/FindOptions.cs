/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Specialized;
using System.Linq;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    public class FindOptions
    {
        public FindOptions()
        {
            Text = string.Empty;
            CustomFinders = new IFinder[0];
        }

        public FindOptions(FindOptions options)
        {
            Text = options.Text;
            CaseSensitive = options.CaseSensitive;
            Forward = options.Forward;
            CustomFinders = options.CustomFinders;
        }
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(Text) && CustomFinders.Count == 0;
            }
        }

        public string Text { get; private set; }
        public FindOptions ChangeText(string value)
        {
            return new FindOptions(this) { Text = value ?? string.Empty };
        }
        public bool CaseSensitive { get; private set; }
        public FindOptions ChangeCaseSensitive(bool value)
        {
            return new FindOptions(this) { CaseSensitive = value };
        }
        public bool Forward { get; private set; }
        public FindOptions ChangeForward(bool value)
        {
            return new FindOptions(this) { Forward = value };
        }

        public string GetDescription()
        {
            var strings = new List<string>();
            if (!string.IsNullOrEmpty(Text))
            {
                strings.Add(Text);
            }
            strings.AddRange(CustomFinders.Select(finder => finder.DisplayName));
            return string.Join(string.Empty, strings.ToArray());
        }

        public string GetNotFoundMessage()
        {
            if (CustomFinders.Count == 0)
            {
                return string.Format(Resources.FindOptions_GetNotFoundMessage_The_text__0__could_not_be_found, Text);
            }
            if (CustomFinders.Count == 1)
            {
                return string.Format(Resources.FindOptions_GetNotFoundMessage_Could_not_find__0__,
                                     CustomFinders[0].DisplayName);
            }
            return string.Format(Resources.FindOptions_GetNotFoundMessage_Could_not_find_any_of__0__items,
                                 CustomFinders.Count);
        }

        public IList<IFinder> CustomFinders
        {
            get;
            private set;
        }
        public FindOptions ChangeCustomFinders(IEnumerable<IFinder> finders)
        {
            return new FindOptions(this)
            {
                CustomFinders = finders == null
                    ? (IList<IFinder>)new IFinder[0]
                    : Array.AsReadOnly(finders.ToArray())
            };
        }

        public void WriteToSettings(Settings settings, bool includeDirection)
        {
            settings.EditFindText = Text;
            if (includeDirection)
            {
                settings.EditFindUp = !Forward;
            }
            settings.EditFindCase = CaseSensitive;
            var customFinders = new StringCollection();
            customFinders.AddRange(CustomFinders.Select(customFinder=>customFinder.Name).ToArray());
            settings.CustomFinders = customFinders;
        }

        public static FindOptions ReadFromSettings(Settings settings)
        {
            var finders = new List<IFinder>();
            if (settings.CustomFinders != null)
            {
                var finderNames = new HashSet<string>(settings.CustomFinders.Cast<string>());
                foreach (var finder in Finders.ListAllFinders())
                {
                    if (finderNames.Contains(finder.Name))
                    {
                        finders.Add(finder);
                    }
                }
            }
            return new FindOptions()
                .ChangeText(settings.EditFindText)
                .ChangeForward(!settings.EditFindUp)
                .ChangeCaseSensitive(settings.EditFindCase)
                .ChangeCustomFinders(finders);
        }
    }
}
