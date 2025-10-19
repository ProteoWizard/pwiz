/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding.Internal
{
    public class WrappedDataPropertyDescriptor : DataPropertyDescriptor
    {
        public WrappedDataPropertyDescriptor(DataSchema dataSchema, string name, IColumnCaption columnCaption, PropertyDescriptor innerPropertyDescriptor) 
            : base(name, columnCaption, dataSchema.DataSchemaLocalizer, IndexedPropertyDescriptor.MergeAttributes(dataSchema, columnCaption, IndexedPropertyDescriptor.GetAttributes(innerPropertyDescriptor)))
        {
            InnerPropertyDescriptor = innerPropertyDescriptor;
        }

        public PropertyDescriptor InnerPropertyDescriptor { get; }
        public override bool CanResetValue(object component)
        {
            return InnerPropertyDescriptor.CanResetValue(component);
        }

        public override object GetValue(object component)
        {
            return InnerPropertyDescriptor.GetValue(component);
        }

        public override void ResetValue(object component)
        {
            InnerPropertyDescriptor.ResetValue(component);
        }

        public override void SetValue(object component, object value)
        {
            InnerPropertyDescriptor.SetValue(component, value);
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotSupportedException();
        }

        public override Type ComponentType
        {
            get
            {
                return InnerPropertyDescriptor.ComponentType;
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                return InnerPropertyDescriptor.IsReadOnly;
            }
        }

        public override Type PropertyType
        {
            get
            {
                return InnerPropertyDescriptor.PropertyType;
            }
        }
    }
}
