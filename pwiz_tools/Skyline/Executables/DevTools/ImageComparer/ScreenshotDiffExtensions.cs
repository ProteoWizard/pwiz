/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>
 *                   MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using ImageComparer.Core;

namespace ImageComparer
{
    /// <summary>
    /// UI-specific extensions for ScreenshotDiff that depend on System.Windows.Forms.
    /// The core diff logic lives in ImageComparer.Core (no UI dependencies).
    /// </summary>
    internal static class ScreenshotDiffExtensions
    {
        public static void ShowBinaryDiff(this ScreenshotDiff diff, RichTextBox richTextBox)
        {
            ShowDiff(richTextBox, diff.MemoryOld, diff.MemoryNew);
        }

        private static void ShowDiff(RichTextBox richTextBox, byte[] array1, byte[] array2)
        {
            richTextBox.Clear();

            // Determine the longer array length
            int maxLength = Math.Max(array1.Length, array2.Length);
            int linesShown = 0;
            for (int i = 0; i < maxLength; i += 16)
            {
                if (ArraysMatch(i, 16, array1, array2))
                {
                    continue;
                }

                ShowLineDiff(richTextBox, i, 16, array1, array2, Color.Red, "<<");
                ShowLineDiff(richTextBox, i, 16, array2, array1, Color.Green, ">>");

                if (++linesShown >= 4)
                {
                    // Stop after 4 lines
                    AppendText(richTextBox, "...", Color.Black);
                    break;
                }
            }
        }

        private static bool ArraysMatch(int startIndex, int len, byte[] array1, byte[] array2)
        {
            for (int i = startIndex; i < startIndex + len; i++)
            {
                if (i >= array1.Length && i >= array2.Length)
                    return true;
                if (i >= array1.Length || i >= array2.Length)
                    return false;
                if (array1[i] != array2[i])
                    return false;
            }
            return true;
        }

        private static void ShowLineDiff(RichTextBox richTextBox, int startIndex, int lineLen, byte[] arrayShow, byte[] arrayCompare, Color color, string prefix)
        {
            AppendText(richTextBox, $"{prefix} {startIndex:X8} ", Color.Black); // address

            for (int n = 0; n < lineLen; n++)
            {
                if (n == lineLen / 2)
                    AppendText(richTextBox, "  ", Color.Black);

                int i = startIndex + n;
                if (i < arrayShow.Length && i < arrayCompare.Length && arrayShow[i] == arrayCompare[i])
                {
                    // Bytes are the same, display them in black
                    AppendText(richTextBox, $"{arrayShow[i]:X2} ", Color.Black);
                }
                else if (i < arrayShow.Length)
                {
                    // Bytes differ
                    AppendText(richTextBox, $"{arrayShow[i]:X2} ", color);
                }
                else
                {
                    // Bytes missing
                    AppendText(richTextBox, "-- ", color);
                }
            }
            AppendText(richTextBox, "    ", Color.Black);
            for (int n = 0; n < lineLen; n++)
            {
                int i = startIndex + n;
                if (i < arrayShow.Length && i < arrayCompare.Length && arrayShow[i] == arrayCompare[i])
                {
                    // Bytes are the same, display them in black
                    AppendText(richTextBox, GetChar(arrayShow[i]), Color.Black);
                }
                else if (i < arrayShow.Length)
                {
                    // Bytes differ
                    AppendText(richTextBox, GetChar(arrayShow[i]), color);
                }
                else
                {
                    // Bytes missing
                    AppendText(richTextBox, "^", color);
                }
            }
            AppendText(richTextBox, Environment.NewLine, Color.Black);
        }

        /// <summary>
        /// If a byte is printable ASCII the character as text is returned otherwise a period.
        /// </summary>
        private static string GetChar(byte b)
        {
            return (b >= 0x20 && b <= 0x7E ? (char)b : '.').ToString();
        }

        private static void AppendText(RichTextBox richTextBox, string text, Color color)
        {
            richTextBox.SelectionStart = richTextBox.TextLength;
            richTextBox.SelectionLength = 0;
            richTextBox.SelectionColor = color;
            richTextBox.AppendText(text);
            richTextBox.SelectionColor = richTextBox.ForeColor;
        }
    }
}
