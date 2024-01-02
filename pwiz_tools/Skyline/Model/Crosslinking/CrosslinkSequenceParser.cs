/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Crosslinking
{
    public static class CrosslinkSequenceParser
    {
        public static CrosslinkLibraryKey TryParseCrosslinkLibraryKey(string str, int charge)
        {
            if (!LooksLikeCrosslinkSequence(str))
            {
                return null;
            }

            try
            {
                return ParseCrosslinkLibraryKey(str, charge);
            }
            catch
            {
                return null;
            }
        }
        private static bool LooksLikeCrosslinkSequence(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            return str.IndexOf(@"-[", StringComparison.Ordinal) >= 0 || str.EndsWith(@"-", StringComparison.Ordinal);
        }

        public static CrosslinkLibraryKey ParseCrosslinkLibraryKey(string str, int charge)
        {
            List<PeptideLibraryKey> peptideSequences = new List<PeptideLibraryKey>();
            List<CrosslinkLibraryKey.Crosslink> crosslinks = new List<CrosslinkLibraryKey.Crosslink>();
            bool inCrosslinkers = false;
            int ich = 0;
            foreach (var token in Tokenize(str))
            {
                if (token.StartsWith(@"[") && token.EndsWith(@"]"))
                {
                    inCrosslinkers = true;
                }

                if (inCrosslinkers)
                {
                    crosslinks.Add(ParseCrosslink(token, ich));
                }
                else
                {
                    string sequence = token;
                    if (sequence.EndsWith(@"-"))
                    {
                        sequence = sequence.Substring(sequence.Length - 1);
                    }

                    if (!FastaSequence.IsExSequence(sequence))
                    {
                        throw CommonException.Create(new ParseExceptionDetail(CrosslinkingResources.CrosslinkSequenceParser_ParseCrosslinkLibraryKey_Invalid_peptide_sequence, ich));
                    }
                    peptideSequences.Add(new PeptideLibraryKey(sequence, 0));
                }

                ich += token.Length;
            }
            return new CrosslinkLibraryKey(peptideSequences, crosslinks, charge);
        }


        private static CrosslinkLibraryKey.Crosslink ParseCrosslink(string crosslink, int position)
        {
            if (!crosslink.StartsWith(@"["))
            {
                throw CommonException.Create(Expected('[', position));
            }

            if (!crosslink.EndsWith(@"]"))
            {
                throw CommonException.Create(Expected(']', position + crosslink.Length));
            }

            int ichAt = crosslink.IndexOf('@');
            if (ichAt < 0)
            {
                throw CommonException.Create(Expected('@', position + crosslink.Length));
            }

            string name = crosslink.Substring(1, ichAt - 1);
            var indexes = crosslink.Substring(ichAt + 1, crosslink.Length - ichAt - 2);
            var positionLists = new List<IList<int>>();
            foreach (var positions in indexes.Split(','))
            {
                if (positions == @"*")
                {
                    positionLists.Add(new int[0]);
                }
                else
                {
                    var list = new List<int>();
                    foreach (var value in positions.Split('-'))
                    {
                        try
                        {
                            list.Add(int.Parse(value, CultureInfo.InvariantCulture));
                        }
                        catch (Exception)
                        {
                            string message = string.Format(CrosslinkingResources.CrosslinkSequenceParser_ParseCrosslink_Unable_to_parse___0___as_a_number, value);
                            throw CommonException.Create(new ParseExceptionDetail(message, position + ichAt));
                        }
                    }
                    positionLists.Add(list);
                }
            }
            return new CrosslinkLibraryKey.Crosslink(name, positionLists);
        }

        private static ParseExceptionDetail Expected(char ch, int position)
        {
            return new ParseExceptionDetail(string.Format(CrosslinkingResources.CrosslinkSequenceParser_Expected_Expected___0__, ch), position);
        }

        private static IEnumerable<string> Tokenize(string str)
        {
            int ichLast = 0;
            bool inBracket = false;
            bool inCrosslinks = false;
            for (int ich = 0; ich < str.Length; ich++)
            {
                char ch = str[ich];
                if (inBracket)
                {
                    if (ch == ']')
                    {
                        inBracket = false;
                        if (inCrosslinks)
                        {
                            yield return str.Substring(ichLast, ich + 1 - ichLast);
                            ichLast = ich + 1;
                        }
                    }
                    continue;
                }

                if (ch == '[')
                {
                    inBracket = true;
                    if (ich == ichLast)
                    {
                        inCrosslinks = true;
                    }
                }
                if (ch == '-')
                {
                    yield return str.Substring(ichLast, ich - ichLast);
                    ichLast = ich + 1;
                }
            }

            if (ichLast < str.Length)
            {
                yield return str.Substring(ichLast);
            }
        }

        public class ParseExceptionDetail
        {
            public ParseExceptionDetail(string message, int? position)
            {
                Message = message;
                Position = position;
            }
            public string Message { get; private set; }
            public int? Position { get; private set; }

            public override string ToString()
            {
                string message = Message;
                if (Position.HasValue)
                {
                    message = string.Format(CrosslinkingResources.ParseExceptionDetail_ToString__at_position__0_, message, Position + 1);
                }
                return message;
            }
        }
    }
}
