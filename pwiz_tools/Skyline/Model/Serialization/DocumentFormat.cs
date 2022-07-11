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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    /// <summary>
    /// <para>Constants listing version numbers which identify times when changes were made to the way
    /// that Skyline saves documents in the .sky file.</para>
    /// <para>Whenever we release we make a changeless format version update to match
    /// Install.MajorVersion.Install.MinorVersion.
    /// Between releases we increment by 0.01 from the last release format version.
    /// e.g. 21.1 (release), 21.11 (daily), 21.12 (daily), ... 21.2 (release)</para>
    /// <para>The current schema is described in a file called "Skyline_Current.xsd".
    /// The current version number should be changed if you make a change to "Skyline_Current.xsd", and there
    /// is already a released build of Skyline-Daily that is using the current version.
    /// When changing the current version number, you should copy "Skyline_Current.xsd" to "Skyline_###.xsd" representing
    /// the old version number.</para>
    /// </summary>
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
        public static readonly DocumentFormat VERSION_4_22 = new DocumentFormat(4.22); // Moves explicit CE, explicit ion mobility high energy offset etc to transition instead of peptide level
        public static readonly DocumentFormat VERSION_19_1 = new DocumentFormat(19.1); // Release format
        public static readonly DocumentFormat VERSION_19_11 = new DocumentFormat(19.11); // Annotation expressions
        public static readonly DocumentFormat VERSION_19_12 = new DocumentFormat(19.12); // Adds sample_id and serial_number
        public static readonly DocumentFormat VERSION_20_1 = new DocumentFormat(20.1); // Release format
        public static readonly DocumentFormat VERSION_20_11 = new DocumentFormat(20.11);
        public static readonly DocumentFormat VERSION_20_12 = new DocumentFormat(20.12); // Crosslinked peptides
        public static readonly DocumentFormat VERSION_20_13 = new DocumentFormat(20.13); // Add decoy_match_proportion
        public static readonly DocumentFormat VERSION_20_14 = new DocumentFormat(20.14); // Moves ion mobility settings from PeptideSettings to TransitionSettings, introduces ion mobility libraries (.imsdb files)
        public static readonly DocumentFormat TRANSITION_SETTINGS_ION_MOBILITY = VERSION_20_14; // First version with ion mobility settings moved from PeptideSettings to TransitionSettings, and using .imsdb IMS libraries
        public static readonly DocumentFormat VERSION_20_2 = new DocumentFormat(20.2); // Release format
        public static readonly DocumentFormat VERSION_20_21 = new DocumentFormat(20.21); // Sequential audit log hash calculation
        public static readonly DocumentFormat SEQUENTIAL_LOG_HASH = VERSION_20_21;
        public static readonly DocumentFormat VERSION_20_22 = new DocumentFormat(20.22); // Flat crosslinks
        public static readonly DocumentFormat FLAT_CROSSLINKS = VERSION_20_22;
        public static readonly DocumentFormat VERSION_21_1 = new DocumentFormat(21.1); // Release format
        public static readonly DocumentFormat VERSION_21_11 = new DocumentFormat(21.11); // Import Time
        public static readonly DocumentFormat VERSION_21_12 = new DocumentFormat(21.12); // Add synchronized integration, auto-train peak scoring model
        public static readonly DocumentFormat VERSION_21_2 = new DocumentFormat(21.2); // Release format
        public static readonly DocumentFormat VERSION_22_1 = new DocumentFormat(22.1); // Add Cu' to list of supported heavy isotopes
        public static readonly DocumentFormat VERSION_22_11 = new DocumentFormat(22.11); // zh and zhh ion types added to the schema.
        public static readonly DocumentFormat VERSION_22_12 = new DocumentFormat(22.12); // ignore_sim_scans added
        public static readonly DocumentFormat CURRENT = VERSION_22_12;

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

        /// <summary>
        /// Return the name of the Skyline version corresponding to this format.
        /// If this format is not an official Skyline release then it will be just "Version: " and the number.
        /// </summary>
        public string GetDescription()
        {
            foreach (var skylineVersion in SkylineVersion.SupportedForSharing())
            {
                if (Equals(skylineVersion.SrmDocumentVersion))
                {
                    if (Equals(SkylineVersion.CURRENT, skylineVersion) && Install.Type == Install.InstallType.developer)
                    {
                        break;
                    }
                    return skylineVersion.Label;
                }
            }

            return string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Version__0__, ToString());
        }
    }
}
