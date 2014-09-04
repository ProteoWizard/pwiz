/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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
namespace pwiz.Skyline.Model.Lib.ChromLib.Data
{
    public class IrtLibrary : ChromLibEntity<IrtLibrary>
    {
        public virtual string PeptideModSeq { get; set; }
        public virtual bool Standard { get; set; }
        public virtual double Irt { get; set; }
        public virtual int TimeSource { get; set; }
    }
}
