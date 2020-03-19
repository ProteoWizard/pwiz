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
using System.Diagnostics.Contracts;
using System.Globalization;

namespace pwiz.Skyline.Model.Serialization
{
    public struct DocumentFormat : IComparable<DocumentFormat>
    {
        public static readonly DocumentFormat VERSION_0_1 = new DocumentFormat(0.1);
        public static readonly DocumentFormat VERSION_0_2 = new DocumentFormat(0.2);
        public static readonly DocumentFormat VERSION_0_8 = new DocumentFormat(0.8);
        public static readonly DocumentFormat  VERSION_1_2 = new DocumentFormat(1.2);   // Used briefly during development of v1.3
        public static readonly DocumentFormat  VERSION_1_3 = new DocumentFormat(1.3);
        public static readonly DocumentFormat  VERSION_1_4 = new DocumentFormat(1.4);
        public static readonly DocumentFormat  VERSION_1_5 = new DocumentFormat(1.5);
        public static readonly DocumentFormat  VERSION_1_6 = new DocumentFormat(1.6);   // Adds richer protein metadata
        public static readonly DocumentFormat  VERSION_1_7 = new DocumentFormat(1.7);   // Adds Ion Mobility handling
        public static readonly DocumentFormat  VERSION_1_8 = new DocumentFormat(1.8);   // Adds Reporter Ions and non proteomic transitions
        public static readonly DocumentFormat  VERSION_1_9 = new DocumentFormat(1.9);   // Adds sequence lookup key for decoys
        public static readonly DocumentFormat  VERSION_2_61 = new DocumentFormat(2.61);   // Adds drift time high energy offsets for Waters IMS
        public static readonly DocumentFormat  VERSION_2_62 = new DocumentFormat(2.62);   // Revised small molecule support
        public static readonly DocumentFormat  VERSION_3_1 = new DocumentFormat(3.1);   // Release format. No change from 2.62
        public static readonly DocumentFormat  VERSION_3_11 = new DocumentFormat(3.11); // Adds compensation voltage optimization support
        public static readonly DocumentFormat  VERSION_3_12 = new DocumentFormat(3.12); // Adds small molecule ion labels and multiple charge states
        public static readonly DocumentFormat  VERSION_3_5 = new DocumentFormat(3.5); // Release format
        public static readonly DocumentFormat  VERSION_3_51 = new DocumentFormat(3.51); // Adds document GUID and Panorama URI
        public static readonly DocumentFormat  VERSION_3_52 = new DocumentFormat(3.52); // Cleans up potential ambiguity around explicit vs calculated slens and cone voltage
        public static readonly DocumentFormat  VERSION_3_53 = new DocumentFormat(3.53); // Adds MIDAS library support
        public static readonly DocumentFormat  VERSION_3_54 = new DocumentFormat(3.54); // Native q values
        public static readonly DocumentFormat  VERSION_3_55 = new DocumentFormat(3.55); // Adds linear width option for drift time filtering
        public static readonly DocumentFormat  VERSION_3_6 = new DocumentFormat(3.6); // Improved full-scan settings for DIA isolation schemes and add selective extraction
        public static readonly DocumentFormat  VERSION_3_61 = new DocumentFormat(3.61); //  Semi-cleavage enzymes and ion mobility CCS work, points across peak
        public static readonly DocumentFormat VERSION_3_62 = new DocumentFormat(3.62); // Adds binary results 
        public static readonly DocumentFormat BINARY_RESULTS = VERSION_3_62; // First version that had binary results
        public static readonly DocumentFormat VERSION_3_7 = new DocumentFormat(3.7); // Release format
        public static readonly DocumentFormat VERSION_3_71 = new DocumentFormat(3.71); // Adds EncyclopeDIA lib support
        public static readonly DocumentFormat VERSION_3_72 = new DocumentFormat(3.72); // Adds small molecule library support
        public static readonly DocumentFormat VERSION_3_73 = new DocumentFormat(3.73); // Adds ion mobility units, including Bruker TIMS support (and volcano plot formatting options)
        public static readonly DocumentFormat VERSION_4_1 = new DocumentFormat(3.73); // Release format : unfortunately 4.1 was released with 3.73 as its format
        public static readonly DocumentFormat VERSION_4_11 = new DocumentFormat(4.11); // sample_dilution_factor
        public static readonly DocumentFormat VERSION_4_12 = new DocumentFormat(4.12); // Adds audit log
        public static readonly DocumentFormat VERSION_4_13 = new DocumentFormat(4.13); // Adds new audit log format
        public static readonly DocumentFormat VERSION_4_2 = new DocumentFormat(4.2); // Release format
        public static readonly DocumentFormat VERSION_4_21 = new DocumentFormat(4.21); // Adds Lists feature
        public static readonly DocumentFormat VERSION_4_22 = new DocumentFormat(4.22); // Moves explicite CE, explicit ion mobility high energy offset etc to transition instead of peptide level
        public static readonly DocumentFormat VERSION_19_1 = new DocumentFormat(19.1); // Release format
        public static readonly DocumentFormat VERSION_19_11 = new DocumentFormat(19.11); // Annotation expressions
        public static readonly DocumentFormat VERSION_19_12 = new DocumentFormat(19.12); // Adds sample_id and serial_number
        public static readonly DocumentFormat VERSION_20_1 = new DocumentFormat(20.1); // Release format
        public static readonly DocumentFormat VERSION_20_11 = new DocumentFormat(20.11);
        public static readonly DocumentFormat CURRENT = VERSION_20_11;

        private readonly double _versionNumber;
        public DocumentFormat(double versionNumber)
        {
            _versionNumber = versionNumber;
        }

        [Pure]
        public double AsDouble()
        {
            return _versionNumber;
        }

        public int CompareTo(DocumentFormat other)
        {
            return _versionNumber.CompareTo(other._versionNumber);
        }

        public static bool operator <(DocumentFormat left, DocumentFormat right)
        {
            return left.CompareTo(right) < 0;
        }
        public static bool operator <=(DocumentFormat left, DocumentFormat right)
        {
            return left.CompareTo(right) <= 0;
        }
        public static bool operator >(DocumentFormat left, DocumentFormat right)
        {
            return left.CompareTo(right) > 0;
        }
        public static bool operator >=(DocumentFormat left, DocumentFormat right)
        {
            return left.CompareTo(right) >= 0;
        }

        public override string ToString()
        {
            return _versionNumber.ToString(CultureInfo.InvariantCulture);
        }
    }
}
