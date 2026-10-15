/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class NoteColorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestNoteColor()
        {
            VerifyNoteColorList();
            VerifyParse();
            VerifyChangeNoteColor();
            VerifyPlaceholderNoteInOtherLanguage();
        }

        private static void VerifyNoteColorList()
        {
            var noteColors = NoteColor.ListNoteColors();
            AssertEx.AreEqual(Annotations.COLOR_BRUSHES.Count, noteColors.Count);
            for (int i = 0; i < noteColors.Count; i++)
            {
                AssertEx.AreEqual(i, noteColors[i].Index);
                AssertEx.AreEqual(noteColors[i], NoteColor.FromIndex(i));
            }
            // The Targets tree draws -1 (no color chosen) and out of range values with the nearest color
            AssertEx.AreEqual(NoteColor.ORANGE_RED, NoteColor.FromIndex(-1));
            AssertEx.AreEqual(NoteColor.PURPLE, NoteColor.FromIndex(noteColors.Count));
            AssertEx.AreEqual(noteColors.Count, noteColors.Select(c => c.Name).Distinct().Count());
            AssertEx.AreEqual(noteColors.Count, noteColors.Select(c => c.Title).Distinct().Count());
        }

        private static void VerifyParse()
        {
            var formatter = new NoteColor.PropertyFormatter();
            foreach (var noteColor in NoteColor.ListNoteColors())
            {
                AssertEx.AreEqual(noteColor, NoteColor.Parse(noteColor.Name));
                AssertEx.AreEqual(noteColor, NoteColor.Parse(noteColor.Title));
                AssertEx.AreEqual(noteColor, NoteColor.Parse(noteColor.Name.ToUpperInvariant()));
                var text = formatter.FormatValue(CultureInfo.CurrentCulture, noteColor);
                AssertEx.AreEqual(noteColor.Name, text);
                AssertEx.AreEqual(noteColor, formatter.ParseValue(CultureInfo.CurrentCulture, text));
            }
            Assert.IsNull(NoteColor.Parse(string.Empty));
            Assert.IsNull(NoteColor.Parse(@"NotAColor"));
            Assert.IsNull(formatter.ParseValue(CultureInfo.CurrentCulture, string.Empty));
            AssertEx.ThrowsException<FormatException>(() => formatter.ParseValue(CultureInfo.CurrentCulture, @"NotAColor"));
        }

        private static void VerifyChangeNoteColor()
        {
            // No flag is drawn without a note, so there is no color
            Assert.IsNull(Annotations.EMPTY.NoteColor);
            Assert.AreSame(Annotations.EMPTY, Annotations.EMPTY.ChangeNoteColor(null));

            // A color on a node with no note adds a placeholder note, and removing the color removes it
            var colored = Annotations.EMPTY.ChangeNoteColor(NoteColor.GREEN);
            AssertEx.AreEqual(NoteColor.GREEN, colored.NoteColor);
            AssertEx.AreEqual(NoteColor.PlaceholderNote, colored.Note);
            var recolored = colored.ChangeNoteColor(NoteColor.BLUE);
            AssertEx.AreEqual(NoteColor.BLUE, recolored.NoteColor);
            AssertEx.AreEqual(NoteColor.PlaceholderNote, recolored.Note);
            var uncolored = recolored.ChangeNoteColor(null);
            Assert.IsTrue(uncolored.IsEmpty);
            Assert.IsNull(uncolored.NoteColor);

            // A note the user wrote is kept when the color changes or is removed
            const string userNote = "UserNote";
            var noted = Annotations.EMPTY.ChangeNote(userNote);
            AssertEx.AreEqual(NoteColor.ORANGE_RED, noted.NoteColor);
            // Choosing the color already drawn does not change anything
            Assert.AreSame(noted, noted.ChangeNoteColor(NoteColor.ORANGE_RED));
            var notedBlue = noted.ChangeNoteColor(NoteColor.BLUE);
            AssertEx.AreEqual(userNote, notedBlue.Note);
            AssertEx.AreEqual(NoteColor.BLUE.Index, notedBlue.ColorIndex);
            var notedUncolored = notedBlue.ChangeNoteColor(null);
            AssertEx.AreEqual(userNote, notedUncolored.Note);
            AssertEx.AreEqual(Annotations.EMPTY.ColorIndex, notedUncolored.ColorIndex);

            // A node with only annotations does not need a placeholder note
            var annotated = Annotations.EMPTY.ChangeAnnotation(@"MyAnnotation", @"MyValue");
            var annotatedBlack = annotated.ChangeNoteColor(NoteColor.BLACK);
            Assert.IsNull(annotatedBlack.Note);
            AssertEx.AreEqual(NoteColor.BLACK, annotatedBlack.NoteColor);
        }

        private static void VerifyPlaceholderNoteInOtherLanguage()
        {
            var originalCulture = Thread.CurrentThread.CurrentUICulture;
            Annotations colored;
            try
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(@"ja");
                colored = Annotations.EMPTY.ChangeNoteColor(NoteColor.PINK);
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalCulture;
            }
            Assert.IsTrue(NoteColor.IsPlaceholderNote(colored.Note));
            Assert.IsTrue(colored.ChangeNoteColor(null).IsEmpty);
        }
    }
}
