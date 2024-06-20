/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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

using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI
{
    internal static class PeakBoundaryImporterUI
    {
        /// <summary>
        /// UI for warning about unrecognized peptides in imported file
        /// </summary>
        public static bool UnrecognizedPeptidesCancel(this PeakBoundaryImporter importer, IWin32Window parent)
        {
            const int itemsToShow = 10;
            if (!ShowMissingMessage(parent, itemsToShow, importer.UnrecognizedPeptides, p => p.ToString(), PeptideMessages))
                return false;
            if (!ShowMissingMessage(parent, itemsToShow, importer.UnrecognizedFiles, f => f.ToString(), FileMessages))
                return false;
            if (!ShowMissingMessage(parent, itemsToShow, importer.UnrecognizedChargeStates, c => c.PrintLine(' '), ChargeMessages))
                return false;
            return true;
        }

        private class MissingMessageLines
        {
            public MissingMessageLines(string first, string last)
            {
                First = first;
                Last = last;
            }

            public string First { get; private set; }
            public string Last { get; private set; }
        }

        private static MissingMessageLines PeptideMessages(int count)
        {
            if (count == 1)
            {
                return new MissingMessageLines(
                    Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_peptide_in_the_peak_boundaries_file_was_not_recognized_,
                    Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_peptide_);
            }
            else
            {
                return new MissingMessageLines(
                    string.Format(FileUIResources.SkylineWindow_ImportPeakBoundaries_The_following__0__peptides_in_the_peak_boundaries_file_were_not_recognized__, count),
                    FileUIResources.SkylineWindow_ImportPeakBoundaries_Continue_peak_boundary_import_ignoring_these_peptides_);
            }
        }

        private static MissingMessageLines FileMessages(int count)
        {
            if (count == 1)
            {
                return new MissingMessageLines(
                    Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_file_name_in_the_peak_boundaries_file_was_not_recognized_,
                    Resources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_file_);
            }
            else
            {
                return new MissingMessageLines(
                    string.Format(FileUIResources.SkylineWindow_ImportPeakBoundaries_The_following__0__file_names_in_the_peak_boundaries_file_were_not_recognized_, count),
                    FileUIResources.SkylineWindow_ImportPeakBoundaries_Continue_peak_boundary_import_ignoring_these_files_);
            }
        }

        private static MissingMessageLines ChargeMessages(int count)
        {
            if (count == 1)
            {
                return new MissingMessageLines(
                    FileUIResources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following_peptide__file__and_charge_state_combination_was_not_recognized_,
                    FileUIResources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_these_charge_states_);
            }
            else
            {
                return new MissingMessageLines(
                    string.Format(FileUIResources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_The_following__0__peptide__file__and_charge_state_combinations_were_not_recognized_, count),
                    FileUIResources.PeakBoundaryImporter_UnrecognizedPeptidesCancel_Continue_peak_boundary_import_ignoring_this_charge_state_);
            }
        }

        private static bool ShowMissingMessage<TItem>(IWin32Window parent, int maxItems, HashSet<TItem> items,
            Func<TItem, string> printLine, Func<int, MissingMessageLines> getMessageLines)
        {
            if (items.Any())
            {
                var sb = new StringBuilder();
                var messageLines = getMessageLines(items.Count);
                sb.AppendLine(messageLines.First);
                sb.AppendLine();
                int itemsToShow = Math.Min(items.Count, maxItems);
                var itemsList = items.ToList();
                for (int i = 0; i < itemsToShow; ++i)
                    sb.AppendLine(printLine(itemsList[i]));
                if (itemsToShow < items.Count)
                    sb.AppendLine(@"...");
                sb.AppendLine();
                sb.Append(messageLines.Last);
                var dlgFiles = MultiButtonMsgDlg.Show(parent, sb.ToString(), MultiButtonMsgDlg.BUTTON_OK);
                if (dlgFiles == DialogResult.Cancel)
                {
                    return false;
                }
            }

            return true;
        }

    }
}
