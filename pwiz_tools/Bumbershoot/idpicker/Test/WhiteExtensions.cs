//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestStack.White.Configuration;
using TestStack.White.UIItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.WindowItems;

namespace Test
{
    public static class WhiteExtensionMethods
    {
        /// <summary>
        /// Retrieves a White element using RawElementBasedSearch with the given MaxElementSearchDepth, which can be much faster than a normal search for large control trees.
        /// </summary>
        public static T RawGet<T>(this Window window, SearchCriteria criteria, int searchDepth) where T : UIItem
        {
            CoreAppXmlConfiguration.Instance.RawElementBasedSearch = true;
            int oldDepth = CoreAppXmlConfiguration.Instance.MaxElementSearchDepth;
            CoreAppXmlConfiguration.Instance.MaxElementSearchDepth = searchDepth;

            try
            {
                return window.Get<T>(criteria);
            }
            finally
            {
                CoreAppXmlConfiguration.Instance.RawElementBasedSearch = false;
                CoreAppXmlConfiguration.Instance.MaxElementSearchDepth = oldDepth;
            }
        }
    }
}
