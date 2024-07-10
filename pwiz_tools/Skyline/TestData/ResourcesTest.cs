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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.MSGraph;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class ResourcesTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that none of the public resource properties in any of the Resources
        /// classes returns null.
        /// If any of the properties are null it usually means that the .designer.cs file
        /// is out of sync with the .resx file and the "Run Custom Tool" command should be used.
        /// </summary>
        [TestMethod]
        public void CheckForMissingResources()
        {
            Assert.IsTrue(IsResourceType(typeof(Skyline.Properties.Resources)));
            foreach (var assembly in new[]
                     {
                         typeof(SkylineWindow).Assembly, // Skyline
                         typeof(ProteomeDb).Assembly, // ProteomeDb
                         typeof(FormUtil).Assembly, // CommonUtil
                         typeof(ViewEditor).Assembly, // Common
                         typeof(PanoramaClient.AbstractPanoramaClient).Assembly, // PanoramaClient
                         typeof(MsDataFileImpl).Assembly, // ProteowizardWrapper
                         typeof(MSGraphPane).Assembly, // MSGraph
                         typeof(BiblioSpec.BlibBuild).Assembly // BiblioSpec
                     })
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!IsResourceType(type))
                    {
                        continue;
                    }
                    
                    // Verify that none of the public properties returns null
                    foreach (var property in type.GetProperties(BindingFlags.Public|BindingFlags.Static))
                    {
                        if (_resourceProperties.ContainsKey(property.Name))
                        {
                            // Ignore the "ResourceManager" and "CultureInfo" properties that all of these Types have.
                            continue;
                        }
                        Assert.IsNotNull(property.GetValue(null), "Property {0} in type {1} should not be null", property.Name, type.FullName);
                    }
                }
            }
        }


        /// <summary>
        /// Public static properties that we expect to find in every Resource type
        /// </summary>
        private Dictionary<string, Type> _resourceProperties = new Dictionary<string, Type>
        {
            { "ResourceManager", typeof(ResourceManager) }, 
            { "Culture", typeof(CultureInfo) }
        };

        /// <summary>
        /// Returns true if the Type is a resource type.
        /// </summary>
        public bool IsResourceType(Type type)
        {
            foreach (var requiredField in _resourceProperties)
            {
                if (requiredField.Value != type.GetProperty(requiredField.Key, BindingFlags.Static | BindingFlags.Public)?.PropertyType)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
