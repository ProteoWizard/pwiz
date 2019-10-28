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
using System.Collections.Generic;
using System.Globalization;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public class PeptideAnnotationPairFinder : AbstractFinder
    {
        private readonly List<PeptideAnnotationPair> _pairs;
        private readonly double _displayCV;

        public PeptideAnnotationPairFinder(List<PeptideAnnotationPair> pairs, double displayCV)
        {
            _pairs = pairs;
            _displayCV = displayCV;
        }
        public override string Name
        {
            get { return @"peptide_annotation_pair_finder"; }
        }

        public override string DisplayName
        {
            get { return Resources.PeptideAnnotationPairFinder_DisplayName_Peptides; }
        }

        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            if (bookmarkEnumerator.ResultsIndex == -1)
            {
                var pair = _pairs.Find(p => ReferenceEquals(p.Peptide, bookmarkEnumerator.CurrentDocNode));
                if (pair != null)
                {
                    var displayText = GetDisplayText(_displayCV, pair.Annotation);
                    return new FindMatch(displayText);
                }  
            }

            return null;
        }

        public static string GetDisplayText(double cv, string annotation)
        {
            // ReSharper disable LocalizableElement
            var cvString = (cv * AreaGraphController.GetAreaCVFactorToDecimal()).ToString(CultureInfo.CurrentCulture) + (Settings.Default.AreaCVShowDecimals ? "" : "%");
            // ReSharper restore LocalizableElement
            if (annotation != null)
                return string.Format(Resources.PeptideAnnotationPairFinder_GetDisplayText__0__CV_in__1_, cvString, annotation);
            else
                return string.Format(Resources.PeptideAnnotationPairFinder_GetDisplayText__0__CV, cvString);
        }
    }
}
