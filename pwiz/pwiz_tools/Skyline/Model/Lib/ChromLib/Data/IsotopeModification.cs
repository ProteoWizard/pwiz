/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
namespace pwiz.Skyline.Model.Lib.ChromLib.Data
{
    public class IsotopeModification : ChromLibEntity<IsotopeModification>
    {
        public virtual string Name { get; set; }
        public virtual string IsotopeLabel { get; set; }
        public virtual char AminoAcid { get; set; }
        public virtual char Terminus { get; set; }
        public virtual string Formula { get; set; }
        public virtual double MassDiffMono { get; set; }
        public virtual double MassDiffAvg { get; set; }
        public virtual int Label13C { get; set; }
        public virtual int Label15N { get; set; }
        public virtual int Label18O { get; set; }
        public virtual int Label2H { get; set; }
        public virtual int UnimodId { get; set; }
    }
}
