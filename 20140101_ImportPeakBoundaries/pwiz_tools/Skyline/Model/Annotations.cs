/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Collection of annotation values (and the Note) found on a document node, 
    /// or a ChromInfo.
    /// </summary>
    public sealed class Annotations
    {
        public static readonly List<Brush> COLOR_BRUSHES = new List<Brush> {
                    Brushes.OrangeRed,
                    Brushes.Brown,
                    /*Pink*/ new SolidBrush(Color.FromArgb(255, 128, 255)), 
                    /*Dark Yellow*/ new SolidBrush(Color.FromArgb(206, 206, 0)),
                    /*Bright Green*/ new SolidBrush(Color.FromArgb(0, 255, 0)), 
                    Brushes.Green,
                    /*Bright Blue*/ new SolidBrush(Color.FromArgb(3, 184, 252)),
                    Brushes.Blue,
                    Brushes.Black, 
                    /*Purple*/ new SolidBrush(Color.FromArgb(128, 0, 255))
        };

        public static readonly Annotations EMPTY = new Annotations(null, null, -1);
        private readonly IDictionary<string, string> _annotations;
        
        public Annotations(String note, IEnumerable<KeyValuePair<string, string>> annotations, int colorIndex)
        {
            ColorIndex = colorIndex;
            Note = note == string.Empty ? null : note;
            if (annotations != null)
            {
                var annotationsDict = new Dictionary<string, string>();
                foreach (var entry in annotations)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        annotationsDict.Add(entry.Key, entry.Value);
                    }
                }
                if (annotationsDict.Count > 0 )
                {
                    _annotations = annotationsDict;
                }
            }
        }

        public String Note { get; private set; }

        public int ColorIndex { get; private set; }

        public Brush ColorBrush
        {
            get { return COLOR_BRUSHES[Math.Max(0, Math.Min(COLOR_BRUSHES.Count - 1, ColorIndex))]; }
        }

        public bool IsEmpty
        { 
            get
            {
                return Note == null && _annotations == null;
            }
        }
        public KeyValuePair<string,string>[] ListAnnotations()
        {
            if (_annotations == null)
            {
                return new KeyValuePair<string, string>[0];
            }
            return _annotations.ToArray();
        }
        public String GetAnnotation(String name)
        {           
            string value;
            if (_annotations == null || !_annotations.TryGetValue(name, out value))
            {
                return null;
            }
            return value;
        }
        public object GetAnnotation(AnnotationDef annotationDef)
        {
            return annotationDef.ParsePersistedString(GetAnnotation(annotationDef.Name));
        }
        public Annotations ChangeNote(String note)
        {
            if (note == string.Empty)
            {
                note = null;
            }
            return new Annotations(note, _annotations, ColorIndex);
        }

        public Annotations ChangeAnnotation(string name, string value)
        {
            var newAnnotations = (_annotations != null ?
                new Dictionary<string, string>(_annotations) :
                new Dictionary<string, string>());
            if (newAnnotations.ContainsKey(name))
                newAnnotations[name] = value;
            else
                newAnnotations.Add(name, value);      
            return new Annotations(Note, newAnnotations, ColorIndex);
        }

        public Annotations ChangeAnnotation(AnnotationDef annotationDef, object value)
        {
            return ChangeAnnotation(annotationDef.Name, annotationDef.ToPersistedString(value));
        }

        public Annotations MergeNewAnnotations(string newText, int newColorIndex, IList<KeyValuePair<string, string>> newAnnotations)
        {
            IList<KeyValuePair<string, string>> listNodeAnnotations =
                new List<KeyValuePair<string, string>>(ListAnnotations());
            var dictNodeAnnotations = listNodeAnnotations.ToDictionary(
                nodeAnnotation => nodeAnnotation.Key,
                nodeAnnotation => nodeAnnotation.Value);
            if (newAnnotations != null)
            {
                foreach (KeyValuePair<string, string> annotation in newAnnotations)
                {
                    string value;
                    if (dictNodeAnnotations.TryGetValue(annotation.Key, out value))
                        dictNodeAnnotations[annotation.Key] = annotation.Value;
                    else
                        dictNodeAnnotations.Add(annotation.Key, annotation.Value);
                }
            }
            newColorIndex = newColorIndex != -1 ? newColorIndex : ColorIndex;
            newText = newText ?? Note;
            var annotations = new Annotations(newText, dictNodeAnnotations, newColorIndex);
            if (annotations.IsEmpty)
                annotations.ColorIndex = -1;
            return annotations;
        }

        public Annotations Merge(Annotations annotations)
        {
            string note = Note;
            if (string.IsNullOrEmpty(note))
                note = annotations.Note;
            else if (!string.IsNullOrEmpty(annotations.Note))
            {
                // Be careful not to creat repetitive notes during document merging
                var newNoteBuilder = new StringBuilder();
                foreach(var notePart in SplitNotes(note).Union(SplitNotes(annotations.Note)))
                {
                    if (newNoteBuilder.Length > 0)
                        newNoteBuilder.AppendLine().AppendLine();
                    newNoteBuilder.Append(notePart);
                }
                note = newNoteBuilder.ToString();
            }
            var annotationsNew = _annotations;
            if (annotationsNew == null)
                annotationsNew = annotations._annotations;
            else if (annotations._annotations != null)
            {
                // With annotations implemented as only a set of name, value pairs
                // there is no way to know in this code which annotations are full-text,
                // an so no way to do annotation merging as above.
                annotationsNew = new Dictionary<string, string>(annotationsNew);
                foreach (var annotation in annotations._annotations)
                {
                    if (annotationsNew.ContainsKey(annotation.Key))
                    {
                        if (Equals(annotation.Value, annotationsNew[annotation.Key]))
                            continue;
                        throw new InvalidDataException(string.Format(Resources.Annotations_Merge_Annotation_conflict_for__0__found_attempting_to_merge_annotations,
                                                                     annotation.Key));
                    }
                    annotationsNew.Add(annotation);
                }
            }
            int colorIndex = (ColorIndex != -1 ? ColorIndex : annotations.ColorIndex);
            var merged = new Annotations(note, annotationsNew, colorIndex);
            if (Equals(merged, this))
                return this;
            if (Equals(merged, annotations))
                return annotations;
            return merged;
        }

        private static IEnumerable<string> SplitNotes(string note)
        {
            return note.Split(new[] {"\r\n\r\n"}, StringSplitOptions.RemoveEmptyEntries); // Not L10N
        }

        public bool Equals(Annotations other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualsDict(other._annotations, _annotations) && Equals(other.Note, Note) 
                && Equals(other.ColorIndex, ColorIndex);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Annotations);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return GetHashCodeDict(_annotations) * 397 + (Note != null ? Note.GetHashCode() : 0);
            }
        }

        private static int GetHashCodeDict<TK,TV>(IEnumerable<KeyValuePair<TK, TV>> dictionary)
        {
            if (dictionary == null)
            {
                return 0;
            }
            var result = 0;
            foreach (var entry in dictionary)
            {
                result += GetHashCode(entry.Key)*31 + GetHashCode(entry.Value);
            }
            return result;
        }
        private static int GetHashCode<TVal>(TVal value)
        {
            return Equals(value, default(TVal)) ? 0 : value.GetHashCode();
        }
        private static bool EqualsDict<TK,TV>(ICollection<KeyValuePair<TK, TV>> dictionary1, IDictionary<TK,TV> dictionary2)
        {
            if (ReferenceEquals(dictionary1, dictionary2))
            {
                return true;
            }
            if (dictionary1 == null || dictionary2 == null)
            {
                return false;
            }
            if (dictionary1.Count != dictionary2.Count)
            {
                return false;
            }
            foreach (var entry in dictionary1)
            {
                TV value;
                if (!dictionary2.TryGetValue(entry.Key, out value))
                {
                    return false;
                }
                if (!Equals(entry.Value, value))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
