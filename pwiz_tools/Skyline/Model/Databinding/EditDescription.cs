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

using System;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Generates a description string of the form "Set COLUMNNAME to 'value'", suitable for display
    /// in the undo user interface. 
    /// </summary>
    public class EditDescription : Immutable
    {
        string _message;
        public EditDescription(IColumnCaption columnCaption, string auditLogParseString, ElementRef elementRef, object value)
        {
            ColumnCaption = columnCaption;
            AuditLogParseString = auditLogParseString;
            ElementRef = elementRef;
            Value = value;
        }

        /// <summary>
        /// Returns an EditDescription corresponding to setting an annotation to a particular value.
        /// </summary>
        public static EditDescription SetAnnotation(AnnotationDef annotationDef, object value)
        {
            return new EditDescription(new ConstantCaption(annotationDef.Name), annotationDef.Name, null, value);
        }

        /// <summary>
        /// Returns an EditDescription object for setting a particular column to a particular value.
        /// </summary>
        /// <param name="column">The unlocalized (invariant) name of the column.</param>
        /// <param name="value">The new value that the user changed the property to.</param>
        public static EditDescription SetColumn(string column, object value)
        {
            return new EditDescription(new ColumnCaption(column),
                AuditLogParseHelper.GetParseString(ParseStringType.column_caption, column), null, value);
        }

        public EditDescription ChangeElementRef(ElementRef elementRef)
        {
            return ChangeProp(ImClone(this), im => im.ElementRef = elementRef);
        }

        public static EditDescription Message(ElementRef elementRef, string message)
        {
            return new EditDescription(null, message, elementRef, null) {_message = message};
        }

        public IColumnCaption ColumnCaption { get; private set; }
        public string AuditLogParseString { get; private set; }
        public ElementRef ElementRef { get; private set; }

        public string ElementRefName
        {
            get
            {
                if (ElementRef == null)
                    throw new NotImplementedException();

                return ElementRef.Name;
            }
        }

        public object Value { get; private set; }

        public string GetUndoText(DataSchemaLocalizer dataSchemaLocalizer)
        {
            if (_message != null)
            {
                return _message;
            }
            string fullMessage = string.Format(Resources.EditDescription_GetUndoText_Set__0__to___1__,
                ColumnCaption.GetCaption(dataSchemaLocalizer), Value);
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
