/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System;
using System.Drawing;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Controls.Startup
{
    /// <summary>
    /// Single authoritative catalog of Skyline tutorials, matching the wiki tutorials page.
    /// Consumed by StartPage (UI) and JsonTutorialCatalog (MCP).
    ///
    /// To add a new tutorial:
    /// 1. Add entries to TutorialTextResources.resx (Caption, Description)
    /// 2. Add entries to TutorialLinkResources.resx (zip, pdf)
    /// 3. Add entry to TutorialImageResources.resx (start icon)
    /// 4. Add a TutorialInfo entry to the Tutorials array below
    /// 5. Update the tutorials wiki page on skyline.ms to match
    /// </summary>
    public static class TutorialCatalog
    {
        // Section resource keys from TutorialTextResources.resx.
        // Use GetSectionDisplayName() to get the localized display text.
        public const string SECTION_INTRODUCTORY = @"Section_Introductory";
        public const string SECTION_INTRO_FULL_SCAN = @"Section_Intro_Full_Scan";
        public const string SECTION_FULL_SCAN = @"Section_Full_Scan_Acquisition_Data";
        public const string SECTION_SMALL_MOLECULES = @"Section_Small_Molecules";
        public const string SECTION_REPORTS = @"Section_Reports_Topics";
        public const string SECTION_ADVANCED = @"Section_Advanced_Topics";

        /// <summary>
        /// Master tutorial list in wiki page order. This is the single source of truth
        /// for which tutorials exist, their section grouping, and their ordering.
        /// </summary>
        public static readonly TutorialInfo[] Tutorials =
        {
            // Introductory
            new TutorialInfo(SECTION_INTRODUCTORY, @"MethodEdit", @"MethodEdit"),
            new TutorialInfo(SECTION_INTRODUCTORY, @"MethodRefine", @"MethodRefine"),
            new TutorialInfo(SECTION_INTRODUCTORY, @"GroupedStudy", @"GroupedStudies"),
            new TutorialInfo(SECTION_INTRODUCTORY, @"ExistingQuant", @"ExistingQuant"),
            // Introduction to Full-Scan Acquisition Data
            new TutorialInfo(SECTION_INTRO_FULL_SCAN, @"AcquisitionComparison", @"AcquisitionComparison"),
            new TutorialInfo(SECTION_INTRO_FULL_SCAN, @"PRMOrbitrap", @"PRMOrbitrap"),
            new TutorialInfo(SECTION_INTRO_FULL_SCAN, @"DIA", @"DIA"),
            // Full-Scan Acquisition Data
            new TutorialInfo(SECTION_FULL_SCAN, @"MS1Filtering", @"MS1Filtering"),
            new TutorialInfo(SECTION_FULL_SCAN, @"DDASearch", @"DDASearch"),
            new TutorialInfo(SECTION_FULL_SCAN, @"TargetedMSMS", @"PRM"),
            new TutorialInfo(SECTION_FULL_SCAN, @"DIA_TTOF", @"DIA-TTOF"),
            new TutorialInfo(SECTION_FULL_SCAN, @"DIA_PASEF", @"DIA-PASEF"),
            new TutorialInfo(SECTION_FULL_SCAN, @"DIA_Umpire_TTOF", @"DIA-Umpire-TTOF"),
            new TutorialInfo(SECTION_FULL_SCAN, @"PeakBoundaryImputation", @"PeakBoundaryImputation-DIA"),
            // Small Molecules
            new TutorialInfo(SECTION_SMALL_MOLECULES, @"SmallMolecule", @"SmallMolecule"),
            new TutorialInfo(SECTION_SMALL_MOLECULES, @"SmallMoleculeMethodDevCEOpt", @"SmallMoleculeMethodDevCEOpt"),
            new TutorialInfo(SECTION_SMALL_MOLECULES, @"SmallMoleculeQuantification", @"SmallMoleculeQuantification"),
            new TutorialInfo(SECTION_SMALL_MOLECULES, @"HiResMetabolomics", @"HiResMetabolomics"),
            new TutorialInfo(SECTION_SMALL_MOLECULES, @"SmallMolLibraries", @"SmallMoleculeIMSLibraries"),
            // Reports
            new TutorialInfo(SECTION_REPORTS, @"CustomReports", @"CustomReports"),
            new TutorialInfo(SECTION_REPORTS, @"LiveReports", @"LiveReports"),
            // Advanced Topics
            new TutorialInfo(SECTION_ADVANCED, @"AbsoluteQuant", @"AbsoluteQuant"),
            new TutorialInfo(SECTION_ADVANCED, @"PeakPicking", @"PeakPicking"),
            new TutorialInfo(SECTION_ADVANCED, @"iRT", @"iRT"),
            new TutorialInfo(SECTION_ADVANCED, @"OptimizeCE", @"OptimizeCE"),
            new TutorialInfo(SECTION_ADVANCED, @"IMSFiltering", @"IMSFiltering"),
            new TutorialInfo(SECTION_ADVANCED, @"LibraryExplorer", @"LibraryExplorer"),
            new TutorialInfo(SECTION_ADVANCED, @"AuditLog", @"AuditLog"),
        };

        /// <summary>
        /// Section display order for proteomic mode.
        /// In small molecule mode, SECTION_SMALL_MOLECULES moves before SECTION_INTRODUCTORY.
        /// </summary>
        public static readonly string[] SectionOrder =
        {
            SECTION_INTRODUCTORY,
            SECTION_INTRO_FULL_SCAN,
            SECTION_FULL_SCAN,
            SECTION_SMALL_MOLECULES,
            SECTION_REPORTS,
            SECTION_ADVANCED,
        };

        /// <summary>
        /// Get the localized display name for a section resource key.
        /// </summary>
        public static string GetSectionDisplayName(string sectionKey)
        {
            string name = TutorialTextResources.ResourceManager.GetString(sectionKey);
            Assume.IsNotNull(name, @"Missing TutorialTextResources entry for section: " + sectionKey);
            return name;
        }

        /// <summary>
        /// Get the invariant (English) display name for a section, for LLM consumers.
        /// </summary>
        public static string GetSectionDisplayNameInvariant(string sectionKey)
        {
            string name = TutorialTextResources.ResourceManager.GetString(sectionKey,
                System.Globalization.CultureInfo.InvariantCulture);
            Assume.IsNotNull(name, @"Missing TutorialTextResources entry for section: " + sectionKey);
            return name;
        }

        /// <summary>
        /// Find a tutorial by folder name or resource prefix (case-insensitive).
        /// </summary>
        public static TutorialInfo? FindTutorial(string name)
        {
            foreach (var t in Tutorials)
            {
                if (string.Equals(t.FolderName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.ResourcePrefix, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }
    }

    /// <summary>
    /// Describes a single Skyline tutorial in the catalog.
    /// </summary>
    public struct TutorialInfo
    {
        /// <summary>Section name (e.g., "Introductory", "Advanced Topics").</summary>
        public readonly string Section;

        /// <summary>Resource prefix for .resx lookups (e.g., "MethodEdit").</summary>
        public readonly string ResourcePrefix;

        /// <summary>GitHub folder name under Documentation/Tutorials/ (e.g., "MethodEdit", "DIA-TTOF").</summary>
        public readonly string FolderName;

        public TutorialInfo(string section, string resourcePrefix, string folderName)
        {
            Section = section;
            ResourcePrefix = resourcePrefix;
            FolderName = folderName;
        }

        /// <summary>Display title from TutorialTextResources.</summary>
        public string Caption
        {
            get
            {
                return TutorialTextResources.ResourceManager.GetString(ResourcePrefix + @"_Caption")
                       ?? ResourcePrefix;
            }
        }

        /// <summary>Full description from TutorialTextResources.</summary>
        public string Description
        {
            get
            {
                return TutorialTextResources.ResourceManager.GetString(ResourcePrefix + @"_Description")
                       ?? string.Empty;
            }
        }

        /// <summary>Tutorial ZIP download URL from TutorialLinkResources.</summary>
        public string ZipUrl
        {
            get
            {
                return TutorialLinkResources.ResourceManager.GetString(ResourcePrefix + @"_zip")
                       ?? string.Empty;
            }
        }

        /// <summary>Tutorial wiki page URL from TutorialLinkResources.</summary>
        public string WikiUrl
        {
            get
            {
                return TutorialLinkResources.ResourceManager.GetString(ResourcePrefix + @"_pdf")
                       ?? string.Empty;
            }
        }

        /// <summary>Optional .sky file path within the ZIP. Empty if tutorial has no starting .sky file.</summary>
        public string SkyFileInZip
        {
            get
            {
                return TutorialLinkResources.ResourceManager.GetString(ResourcePrefix + @"_sky")
                       ?? string.Empty;
            }
        }

        /// <summary>Start page icon from TutorialImageResources.</summary>
        public Bitmap Icon
        {
            get
            {
                return TutorialImageResources.ResourceManager.GetObject(ResourcePrefix + @"_start") as Bitmap;
            }
        }
    }
}
