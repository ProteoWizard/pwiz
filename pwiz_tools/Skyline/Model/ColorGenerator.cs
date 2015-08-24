/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using System;
using System.Collections.Generic;
using System.Drawing;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Generate peptide colors that are maximally distinguishable from each other,
    /// while conforming to other constraints (like not too dark, not too light, etc.)
    /// </summary>
    public static class ColorGenerator
    {
        private const int CollisionThreshold = 30;

        /// <summary>
        /// Generate a color for the given protein.  We try to make colors within a 
        /// protein distinguishable, worrying less about differentiation between colors 
        /// from different proteins.
        /// </summary>
        public static Color GetColor(string peptideName, IList<Color> siblingColors)
        {
            if (peptideName == null)
                return PeptideDocNode.UNKNOWN_COLOR;

            // Get hashed color index for this peptide
            int index = GetColorIndex(peptideName);

            // Check for collision with other peptides in this protein.  A collision happens
            // when two colors are close enough that they would be hard to distinguish.
            if (siblingColors.Count < _colors.Length)    // collisions can't be avoided beyond the size of the color array
            {
                for (int i = 0; i < _colors.Length; i++)
                {
                    var color = _colors[index];
                    bool collision = false;
                    foreach (var existingColor in siblingColors)
                    {
                        if (Math.Abs(color.R - existingColor.R) < CollisionThreshold &&
                            Math.Abs(color.G - existingColor.G) < CollisionThreshold &&
                            Math.Abs(color.B - existingColor.B) < CollisionThreshold)
                        {
                            collision = true;
                            break;
                        }
                    }
                    if (!collision)
                        break;

                    // Step to next index value and re-check for collisions.
                    index = (index + 1)%_colors.Length;
                }
            }

            return _colors[index];
        }

        private static int GetColorIndex(string peptideName)
        {
            // Get hash code for peptide name, then XOR the bytes to make a smaller hash,
            // then modulo to our color array size.
            int hash = peptideName.GetHashCode();
            int index = ((hash) ^ (hash >> 8) ^ (hash >> 16) ^ (hash >> 24))%_colors.Length;
            return index;
        }

        // These colors were generated using the SkylinePeptideColorGenerator utility.
        private static readonly Color[] _colors =
        {
            Color.FromArgb(85,106,104),
            Color.FromArgb(212,126,0),
            Color.FromArgb(200,0,161),
            Color.FromArgb(0,194,255),
            Color.FromArgb(0,200,0),
            Color.FromArgb(116,125,0),
            Color.FromArgb(163,166,255),
            Color.FromArgb(159,35,33),
            Color.FromArgb(0,121,94),
            Color.FromArgb(142,68,147),
            Color.FromArgb(27,172,98),
            Color.FromArgb(205,144,76),
            Color.FromArgb(196,110,138),
            Color.FromArgb(0,126,175),
            Color.FromArgb(178,184,102),
            Color.FromArgb(237,140,255),
            Color.FromArgb(108,0,186),
            Color.FromArgb(33,193,188),
            Color.FromArgb(0,92,0),
            Color.FromArgb(175,170,221),
            Color.FromArgb(110,68,0),
            Color.FromArgb(0,108,188),
            Color.FromArgb(179,0,110),
            Color.FromArgb(162,0,174),
            Color.FromArgb(0,216,151),
            Color.FromArgb(0,105,122),
            Color.FromArgb(96,205,0),
            Color.FromArgb(0,122,0),
            Color.FromArgb(0,193,255),
            Color.FromArgb(0,78,189),
            Color.FromArgb(51,81,0),
            Color.FromArgb(117,85,56),
            Color.FromArgb(247,135,65),
            Color.FromArgb(251,130,236),
            Color.FromArgb(211,181,0),
            Color.FromArgb(255,134,157),
            Color.FromArgb(0,198,210),
            Color.FromArgb(190,182,169),
            Color.FromArgb(0,97,104),
            Color.FromArgb(255,79,255),
            Color.FromArgb(96,73,110),
            Color.FromArgb(225,162,227),
            Color.FromArgb(0,99,0),
            Color.FromArgb(135,45,0),
            Color.FromArgb(0,93,57),
            Color.FromArgb(110,164,1),
            Color.FromArgb(175,0,146),
            Color.FromArgb(112,167,131),
            Color.FromArgb(249,109,191),
            Color.FromArgb(49,75,135),
            Color.FromArgb(0,207,185),
            Color.FromArgb(128,61,0),
            Color.FromArgb(255,114,244),
            Color.FromArgb(114,99,0),
            Color.FromArgb(0,182,227),
            Color.FromArgb(120,172,236),
            Color.FromArgb(220,163,147),
            Color.FromArgb(0,192,75),
            Color.FromArgb(0,119,56),
            Color.FromArgb(125,44,55),
            Color.FromArgb(147,23,109),
            Color.FromArgb(66,77,23),
            Color.FromArgb(191,166,25),
            Color.FromArgb(168,115,148),
            Color.FromArgb(0,128,173),
            Color.FromArgb(126,173,177),
            Color.FromArgb(195,89,213),
            Color.FromArgb(159,199,0),
            Color.FromArgb(162,36,84),
            Color.FromArgb(134,61,172),
            Color.FromArgb(153,84,0),
            Color.FromArgb(77,131,163),
            Color.FromArgb(247,149,135),
            Color.FromArgb(231,149,0),
            Color.FromArgb(75,198,236),
            Color.FromArgb(137,67,0),
            Color.FromArgb(53,95,0),
            Color.FromArgb(0,134,214),
            Color.FromArgb(173,181,0),
            Color.FromArgb(201,0,195),
            Color.FromArgb(0,148,0),
            Color.FromArgb(174,139,223),
            Color.FromArgb(52,200,159),
            Color.FromArgb(0,164,190),
            Color.FromArgb(243,152,203),
            Color.FromArgb(0,143,134),
            Color.FromArgb(15,140,222),
            Color.FromArgb(17,116,30),
            Color.FromArgb(140,135,92),
            Color.FromArgb(112,101,226),
            Color.FromArgb(90,73,0),
            Color.FromArgb(217,47,212),
            Color.FromArgb(0,170,243),
            Color.FromArgb(0,100,123),
            Color.FromArgb(137,198,117),
            Color.FromArgb(228,106,89),
            Color.FromArgb(87,68,68),
            Color.FromArgb(161,119,0),
            Color.FromArgb(139,134,166),
            Color.FromArgb(103,167,255),
            Color.FromArgb(0,165,119),
            Color.FromArgb(36,128,120),
            Color.FromArgb(0,139,136),
            Color.FromArgb(200,172,129),
            Color.FromArgb(0,128,223),
            Color.FromArgb(225,77,136),
            Color.FromArgb(183,94,36),
            Color.FromArgb(170,117,237),
            Color.FromArgb(220,27,164),
            Color.FromArgb(0,99,75),
            Color.FromArgb(197,94,0),
            Color.FromArgb(99,68,135),
            Color.FromArgb(0,127,0),
            Color.FromArgb(131,60,94),
            Color.FromArgb(102,62,0),
            Color.FromArgb(0,154,180),
            Color.FromArgb(165,187,135),
            Color.FromArgb(0,100,162),
            Color.FromArgb(170,129,130),
            Color.FromArgb(66,137,0),
            Color.FromArgb(207,174,203),
            Color.FromArgb(57,68,166),
            Color.FromArgb(0,204,135),
            Color.FromArgb(173,58,219),
            Color.FromArgb(0,118,51),
            Color.FromArgb(138,108,0),
            Color.FromArgb(196,78,133),
            Color.FromArgb(8,193,255),
            Color.FromArgb(0,82,156),
            Color.FromArgb(168,99,92),
            Color.FromArgb(255,145,0),
            Color.FromArgb(92,108,0),
            Color.FromArgb(240,40,205),
            Color.FromArgb(226,160,175),
            Color.FromArgb(138,0,146),
            Color.FromArgb(124,132,193),
            Color.FromArgb(0,145,110),
            Color.FromArgb(186,76,0),
            Color.FromArgb(0,149,0),
            Color.FromArgb(0,104,0),
            Color.FromArgb(0,85,117),
            Color.FromArgb(255,121,255),
            Color.FromArgb(0,196,255),
            Color.FromArgb(112,131,11),
            Color.FromArgb(41,169,187),
            Color.FromArgb(132,116,217),
            Color.FromArgb(255,109,212),
            Color.FromArgb(87,122,96),
            Color.FromArgb(0,203,90),
            Color.FromArgb(167,0,78),
            Color.FromArgb(101,144,80),
            Color.FromArgb(152,66,0),
            Color.FromArgb(233,172,0),
            Color.FromArgb(204,89,177),
            Color.FromArgb(60,196,80),
            Color.FromArgb(175,145,69),
            Color.FromArgb(201,97,101),
            Color.FromArgb(144,189,218),
            Color.FromArgb(145,104,0),
            Color.FromArgb(0,98,95),
            Color.FromArgb(48,73,97),
            Color.FromArgb(0,166,87),
            Color.FromArgb(0,89,0),
            Color.FromArgb(179,174,0),
            Color.FromArgb(255,139,195),
            Color.FromArgb(35,105,0),
            Color.FromArgb(240,153,160),
            Color.FromArgb(0,192,167),
            Color.FromArgb(0,212,251),
            Color.FromArgb(0,166,145),
            Color.FromArgb(0,109,138),
            Color.FromArgb(209,157,0),
            Color.FromArgb(41,95,40),
            Color.FromArgb(150,65,128),
            Color.FromArgb(166,123,189),
            Color.FromArgb(161,164,173),
            Color.FromArgb(86,81,0),
            Color.FromArgb(0,109,171),
            Color.FromArgb(171,124,83),
            Color.FromArgb(0,171,179),
            Color.FromArgb(212,78,98),
            Color.FromArgb(0,82,88),
            Color.FromArgb(0,116,0),
            Color.FromArgb(169,107,0),
            Color.FromArgb(160,73,0),
            Color.FromArgb(132,39,0),
            Color.FromArgb(215,150,250),
            Color.FromArgb(224,135,225),
            Color.FromArgb(172,112,0),
            Color.FromArgb(0,132,166),
            Color.FromArgb(0,138,205),
            Color.FromArgb(255,114,255),
            Color.FromArgb(255,125,230),
            Color.FromArgb(0,182,0),
            Color.FromArgb(0,153,53),
            Color.FromArgb(71,162,55),
            Color.FromArgb(253,144,0),
            Color.FromArgb(242,0,222),
            Color.FromArgb(54,116,189),
            Color.FromArgb(177,0,156),
            Color.FromArgb(254,141,222),
            Color.FromArgb(138,34,47),
            Color.FromArgb(171,0,115),
            Color.FromArgb(131,89,105),
            Color.FromArgb(0,121,106),
            Color.FromArgb(134,129,114),
            Color.FromArgb(0,139,106),
            Color.FromArgb(148,31,0),
            Color.FromArgb(204,130,103),
            Color.FromArgb(160,185,166),
            Color.FromArgb(179,91,0),
            Color.FromArgb(116,58,111),
            Color.FromArgb(161,170,251),
            Color.FromArgb(0,133,146),
            Color.FromArgb(120,195,172),
            Color.FromArgb(0,104,63),
            Color.FromArgb(255,89,255),
            Color.FromArgb(0,103,0),
            Color.FromArgb(109,175,0),
            Color.FromArgb(150,142,0),
            Color.FromArgb(145,28,128),
            Color.FromArgb(145,45,94),
            Color.FromArgb(0,105,0),
            Color.FromArgb(159,195,86),
            Color.FromArgb(215,155,195),
            Color.FromArgb(221,135,52),
            Color.FromArgb(0,140,188),
            Color.FromArgb(0,209,255),
            Color.FromArgb(112,57,42),
            Color.FromArgb(0,96,183),
            Color.FromArgb(141,124,0),
            Color.FromArgb(231,117,149),
            Color.FromArgb(0,215,171),
            Color.FromArgb(173,79,175),
            Color.FromArgb(0,154,98),
            Color.FromArgb(0,131,193),
            Color.FromArgb(0,124,0),
            Color.FromArgb(0,161,253),
            Color.FromArgb(220,49,183),
            Color.FromArgb(128,78,0),
            Color.FromArgb(37,114,227),
            Color.FromArgb(114,185,255),
            Color.FromArgb(184,152,255),
            Color.FromArgb(186,7,175),
            Color.FromArgb(0,213,205),
            Color.FromArgb(53,166,0),
            Color.FromArgb(138,0,171),
            Color.FromArgb(252,161,117),
            Color.FromArgb(243,132,255),
            Color.FromArgb(85,92,133),
            Color.FromArgb(59,74,58),
            Color.FromArgb(203,48,147),
            Color.FromArgb(179,146,0),
            Color.FromArgb(15,86,70),
            Color.FromArgb(153,0,95),
            Color.FromArgb(176,68,15)
        };
    }
}
