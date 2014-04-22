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
using System.Linq;
using System.Resources;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Maintains a list of <see cref="ResourceManager" /> so that localized column captions can be looked up.
    /// Also, holds onto the <see cref="CultureInfo"/> which is used to 
    /// </summary>
    public class DataSchemaLocalizer
    {
        public static readonly DataSchemaLocalizer INVARIANT = new DataSchemaLocalizer(CultureInfo.InvariantCulture);
        public DataSchemaLocalizer(CultureInfo formatProvider, params ResourceManager[] columnCaptionResourceManagers)
        {
            FormatProvider = formatProvider;
            ColumnCaptionResourceManagers = ImmutableList.ValueOf(columnCaptionResourceManagers);
        }
        public CultureInfo FormatProvider { get; private set; }
        public IList<ResourceManager> ColumnCaptionResourceManagers { get; private set; }

        public string LookupColumnCaption(ColumnCaption caption)
        {
            if (!caption.IsLocalizable)
            {
                return caption.InvariantCaption;
            }
            foreach (var columnCaptionResourceManager in ColumnCaptionResourceManagers)
            {
                string localizedCaption = columnCaptionResourceManager.GetString(caption.InvariantCaption);
                if (null != localizedCaption)
                {
                    return localizedCaption;
                }
            }
            return caption.InvariantCaption;
        }

        public bool HasEntry(ColumnCaption caption)
        {
            if (!caption.IsLocalizable)
            {
                return true;
            }
            return ColumnCaptionResourceManagers
                .Any(resourceManager => null != resourceManager.GetString(caption.InvariantCaption));
        }
    }
}
