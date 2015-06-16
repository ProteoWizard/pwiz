/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Generates a description string of the form "Set COLUMNNAME to 'value'", suitable for display
    /// in the undo user interface. 
    /// </summary>
    public class EditDescription
    {
        public EditDescription(ColumnCaption columnCaption, object value)
        {
            ColumnCaption = columnCaption;
            Value = value;
        }

        /// <summary>
        /// Returns an EditDescription corresponding to setting an annotation to a particular value.
        /// </summary>
        public static EditDescription SetAnnotation(AnnotationDef annotationDef, object value)
        {
            return new EditDescription(ColumnCaption.UnlocalizableCaption(annotationDef.Name), value);
        }

        /// <summary>
        /// Returns an EditDescription object for setting a particular column to a particular value.
        /// </summary>
        /// <param name="column">The unlocalized (invariant) name of the column.</param>
        /// <param name="value">The new value that the user changed the property to.</param>
        public static EditDescription SetColumn(string column, object value)
        {
            return new EditDescription(new ColumnCaption(column), value);
        }

        public ColumnCaption ColumnCaption { get; private set; }
        public object Value { get; private set; }

        public string GetUndoText(DataSchemaLocalizer dataSchemaLocalizer)
        {
            string fullMessage = string.Format(Resources.EditDescription_GetUndoText_Set__0__to___1__,
                dataSchemaLocalizer.LookupColumnCaption(ColumnCaption), Value);
            return TruncateLongMessage(fullMessage);
        }

        public static string TruncateLongMessage(string message)
        {
            if (message.Length < 100)
            {
                return message;
            }
            return message.Substring(0, 100);
        }
    }
}
