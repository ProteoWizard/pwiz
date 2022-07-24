﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class SkylineObject
    {
        [Browsable(false)]
        public SkylineDataSchema DataSchema
        {
            get { return GetDataSchema(); }
        }

        protected abstract SkylineDataSchema GetDataSchema();

        [Browsable(false)]
        protected SrmDocument SrmDocument
        {
            get { return DataSchema.Document; }
        }

        public virtual ElementRef GetElementRef()
        {
            return null;
        }

        public string GetLocator()
        {
            var elementRef = GetElementRef();
            if (elementRef == null)
            {
                return null;
            }
            return elementRef.ToString();
        }

        public virtual object GetAnnotation(AnnotationDef annotationDef)
        {
            return null;
        }
        public virtual void SetAnnotation(AnnotationDef annotationDef, object value)
        {
        }
        protected void ModifyDocument(EditDescription editDescription, Func<SrmDocument, SrmDocument> action)
        {
            DataSchema.ModifyDocument(editDescription, action);
        }

        protected EditDescription EditColumnDescription(string propertyName, object value)
        {
            var columnCaption = DataSchema.GetColumnCaption(DataSchema.DefaultUiMode, GetType(), propertyName);
            string auditLogParseString = AuditLogParseHelper.GetParseString(ParseStringType.column_caption, 
                columnCaption.GetCaption(DataSchemaLocalizer.INVARIANT));
            return new EditDescription(columnCaption, auditLogParseString, GetElementRef(), value);
        }
    }

    /// <summary>
    /// <see cref="SkylineObject" /> which holds onto a reference to the <see cref="SkylineDataSchema"/>
    /// and returns it in <see cref="GetDataSchema"/>.
    /// </summary>
    public class RootSkylineObject : SkylineObject
    {
        private SkylineDataSchema _dataSchema;
        public RootSkylineObject(SkylineDataSchema dataSchema)
        {
            _dataSchema = dataSchema;
        }

        protected override SkylineDataSchema GetDataSchema()
        {
            return _dataSchema;
        }
    }
}
