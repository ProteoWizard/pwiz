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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    public class DescendantPropertyDescriptor : DataPropertyDescriptor
    {
        // public static DescendantPropertyDescriptor MakePropertyDescriptor(DataSchema dataSchema, string name, IColumnCaption columnCaption, DataPropertyDescriptor ancestor,
        //     PropertyPath ancestorPropertyPath, ColumnPropertyDescriptor original)
        // {
        //     var reflectedPropertyDescriptors = new List<PropertyDescriptor>();
        //     var columnDescriptor = original.DisplayColumn.ColumnDescriptor;
        //     while (!Equals(columnDescriptor.PropertyPath, ancestorPropertyPath))
        //     {
        //         var reflectedPropertyDescriptor = columnDescriptor.ReflectedPropertyDescriptor;
        //         if (reflectedPropertyDescriptor == null)
        //         {
        //             return null;
        //         }
        //         reflectedPropertyDescriptors.Add(reflectedPropertyDescriptor);
        //         columnDescriptor = columnDescriptor.Parent;
        //     }
        //     reflectedPropertyDescriptors.Reverse();
        //
        //     return new DescendantPropertyDescriptor(dataSchema, name, columnCaption, ancestor, reflectedPropertyDescriptors, original);
        // }
        //
        public DescendantPropertyDescriptor(DataSchema dataSchema, string name, IColumnCaption columnCaption, DataPropertyDescriptor ancestor,
            IEnumerable<PropertyDescriptor> reflectedPropertyDescriptors, ColumnPropertyDescriptor original) : base(name, columnCaption, original.DataSchemaLocalizer, IndexedPropertyDescriptor.MergeAttributes(dataSchema, columnCaption, IndexedPropertyDescriptor.GetAttributes(original)))
        {
            Ancestor = ancestor;
            ReflectedPropertyDescriptors = reflectedPropertyDescriptors.ToImmutable();
            Original = original;
        }

        public DataPropertyDescriptor Ancestor { get; }
        public ImmutableList<PropertyDescriptor> ReflectedPropertyDescriptors { get; }
        public ColumnPropertyDescriptor Original { get; }
        public override bool CanResetValue(object component)
        {
            var parentValue = GetParentValue(component);
            if (parentValue == null)
            {
                return false;
            }

            return ReflectedPropertyDescriptors[ReflectedPropertyDescriptors.Count - 1].CanResetValue(parentValue);
        }

        public override object GetValue(object component)
        {
            var parentValue = GetParentValue(component);
            if (parentValue == null)
            {
                return null;
            }

            return ReflectedPropertyDescriptors[ReflectedPropertyDescriptors.Count - 1].GetValue(parentValue);
        }

        public override void ResetValue(object component)
        {
            var parentValue = GetParentValue(component);
            if (parentValue != null)
            {
                ReflectedPropertyDescriptors[ReflectedPropertyDescriptors.Count - 1].ResetValue(parentValue);
            }
        }

        public override void SetValue(object component, object value)
        {
            var parentValue = GetParentValue(component);
            if (parentValue != null)
            {
                ReflectedPropertyDescriptors[ReflectedPropertyDescriptors.Count - 1].SetValue(parentValue, value);
            }
        }

        public override bool ShouldSerializeValue(object component)
        {
            throw new NotSupportedException();
        }

        public override Type ComponentType
        {
            get
            {
                return typeof(RowItem);
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                return Original.IsReadOnly;
            }
        }

        public override Type PropertyType
        {
            get
            {
                return Original.PropertyType;
            }
        }

        private object GetParentValue(object component)
        {
            var parentValue = Ancestor.GetValue(component);
            foreach (var pd in ReflectedPropertyDescriptors.Take(ReflectedPropertyDescriptors.Count - 1))
            {
                if (parentValue == null)
                {
                    return null;
                }

                parentValue = pd.GetValue(parentValue);
            }

            return parentValue;
        }
    }
}
