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
using System.Linq;
using System.Text;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Collection of annotation values (and the Note) found on a document node, 
    /// or a ChromInfo.
    /// </summary>
    public sealed class Annotations
    {
        public static readonly Annotations Empty = new Annotations(null, null);
        private readonly IDictionary<string, string> _annotations;
        public Annotations(String note, IEnumerable<KeyValuePair<string,string>> annotations)
        {
            Note = note == "" ? null : note;
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
        public bool IsEmpty { 
            get
            {
                return Note == null && _annotations == null;
            }
        }
        public IEnumerable<KeyValuePair<string,string>> ListAnnotations()
        {
            if (_annotations == null)
            {
                return new KeyValuePair<string, string>[0];
            }
            return _annotations.ToArray();
        }
        public String GetAnnotation(String name)
        {
            if (_annotations == null)
            {
                return null;
            }
            string value;
            _annotations.TryGetValue(name, out value);
            return value;
        }
        public object GetAnnotation(AnnotationDef annotationDef)
        {
            if (annotationDef.Type == AnnotationDef.AnnotationType.true_false)
            {
                return GetAnnotation(annotationDef.Name) == null ? false : true;
            }
            return GetAnnotation(annotationDef.Name);
        }
        public Annotations ChangeNote(String note)
        {
            if (note == "")
            {
                note = null;
            }
            return new Annotations(note, _annotations);
        }

        public Annotations ChangeAnnotation(string name, string value)
        {
            var newAnnotations = new Dictionary<string, string>();
            if (_annotations != null)
            {
                foreach (var entry in _annotations)
                {
                    if (entry.Key == name)
                    {
                        continue;
                    }
                    newAnnotations.Add(entry.Key, entry.Value);
                }
            }
            newAnnotations[name] = value;
            return new Annotations(Note, newAnnotations);
        }
        public Annotations ChangeAnnotation(AnnotationDef annotationDef, object value)
        {
            string strValue;
            if (annotationDef.Type == AnnotationDef.AnnotationType.true_false)
            {
                if (value == null || false.Equals(value))
                {
                    strValue = null;
                }
                else
                {
                    strValue = annotationDef.Name;
                }
            }
            else
            {
                strValue = Convert.ToString(value);
            }
            return ChangeAnnotation(annotationDef.Name, strValue);
        }

        public bool Equals(Annotations other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualsDict(other._annotations, _annotations) && Equals(other.Note, Note);
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

        private static int GetHashCodeDict<K,V>(IDictionary<K,V> dictionary)
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
        private static int GetHashCode<T>(T value)
        {
            return Equals(value, default(T)) ? 0 : value.GetHashCode();
        }
        private static bool EqualsDict<K,V>(IDictionary<K,V> dictionary1, IDictionary<K,V> dictionary2)
        {
            if (dictionary1 == dictionary2)
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
                V value;
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
