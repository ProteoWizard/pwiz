/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
 */using System;
using System.Collections.Generic;
using System.Globalization;
using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class ShrinkPeakBoundaries : AbstractCommand
    {
        public ShrinkPeakBoundaries(SkylineToolClient skylineToolClient) : base(skylineToolClient)
        {
        }

        public override void RunCommand()
        {
            var selectedPrecursor = SkylineToolClient.GetSelectedElementLocator("Precursor");
            var selectedReplicate = SkylineToolClient.GetSelectedElementLocator("Replicate");
            if (selectedPrecursor == null || selectedReplicate == null)
            {
                Console.Out.WriteLine("No precursor result selected");
                return;
            }
            var peakBoundaries = SkylineToolClient.GetReportFromDefinition(PeakBoundariesReport);
            var newPeakBoundariesRows = new List<string>()
                { "FileName\tPeptideModifiedSequence\tMinStartTime\tMaxEndTime" };
            for (int i = 0; i < peakBoundaries.Cells.Length; i++)
            {
                var row = peakBoundaries.Cells[i];
                if (row[4] == selectedPrecursor && row[5] == selectedReplicate)
                {
                    double minStartTime = double.Parse(row[2], CultureInfo.InvariantCulture);
                    double maxEndTime = double.Parse(row[3], CultureInfo.InvariantCulture);
                    double newMinStartTime = (minStartTime * 3 + maxEndTime) / 4;
                    double newMaxEndTime = (minStartTime + maxEndTime * 3) / 4;
                    newPeakBoundariesRows.Add(string.Join("\t", row[0], row[1],
                        newMinStartTime.ToString(CultureInfo.InvariantCulture),
                        newMaxEndTime.ToString(CultureInfo.InvariantCulture)));
                }
            }

            var peakBoundariesCsv = string.Join(Environment.NewLine, newPeakBoundariesRows);
            Console.Out.WriteLine("Importing peak boundaries: {0}", peakBoundariesCsv);
            SkylineToolClient.ImportPeakBoundaries(peakBoundariesCsv);
        }

        private const string PeakBoundariesReport = @"<views>
  <view name='Peak Boundaries' rowsource='pwiz.Skyline.Model.Databinding.Entities.Precursor' sublist='Results!*'>
    <column name='Results!*.Value.PeptideResult.ResultFile.FileName' />
    <column name='Peptide.ModifiedSequence' />
    <column name='Results!*.Value.MinStartTime' />
    <column name='Results!*.Value.MaxEndTime' />
    <column name='Locator' />
    <column name='Results!*.Value.PeptideResult.ResultFile.Replicate.Locator' />
  </view>
</views>";
    }
}
