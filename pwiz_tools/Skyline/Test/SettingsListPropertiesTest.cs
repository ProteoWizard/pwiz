/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The settings lists are immutable, so a change is made by setting the property that holds
    /// the list. These check what that depends on.
    /// </summary>
    [TestClass]
    public class SettingsListPropertiesTest : AbstractUnitTest
    {
        /// <summary>
        /// The list editors store an edited list into the property that holds lists of its type,
        /// so no two properties may hold the same type.
        /// </summary>
        [TestMethod]
        public void TestSettingsListTypeHeldOnce()
        {
            var sharedTypes = Settings.GetSettingsListProperties()
                .GroupBy(property => property.PropertyType)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key.Name + @": " + string.Join(@", ", group.Select(property => property.Name)))
                .ToArray();
            Assert.AreEqual(0, sharedTypes.Length, string.Join(@"; ", sharedTypes));
        }

        /// <summary>
        /// A list class must name itself in <see cref="ISelfTypedList{TList,TItem}"/>, or the
        /// changes to it return a list of another type, and fail the cast to it.
        /// </summary>
        [TestMethod]
        public void TestSettingsListNamesItself()
        {
            var wrongTypes = new List<string>();
            foreach (var type in typeof(Settings).Assembly.GetTypes())
            {
                foreach (var selfTyped in type.GetInterfaces().Where(i =>
                             i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISelfTypedList<,>)))
                {
                    var listType = selfTyped.GetGenericArguments()[0];
                    // A subclass inherits its base class's declaration, and gets back lists of its
                    // own type at run time, since a change copies the list it is made to.
                    if (!listType.IsAssignableFrom(type))
                        wrongTypes.Add(type.Name + @" names " + listType.Name);
                }
            }
            Assert.AreEqual(0, wrongTypes.Count, string.Join(@"; ", wrongTypes));
        }

        /// <summary>
        /// Listeners filter Settings.PropertyChanged by property name, so the key each list
        /// property stores its value under must be the property's name.
        /// </summary>
        [TestMethod]
        public void TestSettingsListChangeNotifies()
        {
            var changedNames = new List<string>();
            PropertyChangedEventHandler onChanged = (sender, e) => changedNames.Add(e.PropertyName);
            Settings.Default.PropertyChanged += onChanged;
            try
            {
                foreach (var property in Settings.GetSettingsListProperties())
                {
                    changedNames.Clear();
                    var list = property.GetValue(Settings.Default);
                    property.SetValue(Settings.Default, list);
                    CollectionAssert.Contains(changedNames, property.Name);
                }
            }
            finally
            {
                Settings.Default.PropertyChanged -= onChanged;
            }
        }
    }
}
