using System;
using System.Collections.Generic;
using System.Globalization;
using EnvDTE;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkSequenceParser
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
        public static bool LooksLikeCrosslinkSequence(string str)
        {
            return FastaSequence.StripModifications(str).IndexOf('-') >= 0;
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
                            string message = string.Format("Unable to parse '{0}' as a number", value);
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
            return new ParseExceptionDetail(string.Format("Expected '{0}'", ch), position);
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
                    message += " at position " + (Position + 1);
                }
                return message;
            }
        }
    }
}
