/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Drawing;
using System.Globalization;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public class FoldChangeRowTipProvider : ITipProvider
    {
        private readonly FoldChangeBindingSource.FoldChangeRow _row;

        public FoldChangeRowTipProvider(FoldChangeBindingSource.FoldChangeRow row)
        {
            _row = row;
        }

        public bool HasTip { get { return true; } }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (var rt = new RenderTools())
            {
                if (_row.Peptide != null)
                    table.AddDetailRow(Helpers.PeptideToMoleculeTextMapper.Translate(GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_Peptide, _row.Peptide.IsSmallMolecule()), 
                        _row.Peptide.ModifiedSequence == null ? _row.Peptide.ToString() : _row.Peptide.ModifiedSequence.ToString(), rt);
                if (_row.Protein != null)
                    table.AddDetailRow(Helpers.PeptideToMoleculeTextMapper.Translate(GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_Protein, _row.Protein.IsNonProteomic()), 
                        ProteinMetadataManager.ProteinModalDisplayText(_row.Protein.DocNode), rt);

                table.AddDetailRow(GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_Fold_Change, _row.FoldChangeResult.FoldChange.ToString(Formats.FoldChange, CultureInfo.CurrentCulture), rt);
                table.AddDetailRow(GroupComparisonStrings.FoldChange_Log2_Fold_Change_, _row.FoldChangeResult.Log2FoldChange.ToString(Formats.FoldChange, CultureInfo.CurrentCulture), rt);
                table.AddDetailRow(GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_P_Value, _row.FoldChangeResult.AdjustedPValue.ToString(Formats.PValue, CultureInfo.CurrentCulture), rt);
                table.AddDetailRow(GroupComparisonStrings.FoldChange__Log10_P_Value_, (-Math.Log10(_row.FoldChangeResult.AdjustedPValue)).ToString(Formats.PValue, CultureInfo.CurrentCulture), rt);

                var size = table.CalcDimensions(g);

                if (draw)
                    table.Draw(g);

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }
        }
    }
}