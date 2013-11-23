/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// If the ChooseColumnsTab should show the user a different tree, then this can transform the view
    /// so that the root data type that the user chooses fields from can be different.
    /// </summary>
    public interface IViewTransformer
    {
        KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> TransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths);
        KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> UntransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths);
    }

    public class IdentityViewTransformer : IViewTransformer
    {
        public KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> TransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths)
        {
            return new KeyValuePair<ViewInfo, IEnumerable<PropertyPath>>(view, propertyPaths);
        }

        public KeyValuePair<ViewInfo, IEnumerable<PropertyPath>> UntransformView(ViewInfo view, IEnumerable<PropertyPath> propertyPaths)
        {
            return new KeyValuePair<ViewInfo, IEnumerable<PropertyPath>>(view, propertyPaths);
        }
    }
}
