/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;

namespace pwiz.Topograph.Search
{
    public class SpectrumLocator
    {
        public SpectrumLocator(String name)
        {
            int ichLastDot = name.LastIndexOf(".");
            int ichLastDot2 = name.LastIndexOf(".", ichLastDot - 1);
            int ichLastDot3 = name.LastIndexOf(".", ichLastDot2 - 1);
            Filename = name.Substring(0, ichLastDot3);
            StartScan = int.Parse(name.Substring(ichLastDot3 + 1, ichLastDot2 - ichLastDot3 - 1));
            EndScan = int.Parse(name.Substring(ichLastDot2 + 1, ichLastDot - ichLastDot2 - 1));
            Charge = int.Parse(name.Substring(ichLastDot + 1));
        }
        public String Filename
        {
            get;
            set;
        }

        public int StartScan
        {
            get;
            set;
        }
        public int EndScan
        {
            get;
            set;
        }
        public int Charge
        {
            get;
            set;
        }
        public String Id
        {
            get
            {
                return StartScan + "." + EndScan + "." + Charge;
            }
        }
    }

}
