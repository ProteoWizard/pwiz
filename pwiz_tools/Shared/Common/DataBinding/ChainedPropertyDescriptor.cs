/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// PropertyDescriptor which gets its value by using the <see cref="PropertyDescriptor.GetValue" /> methods
    /// of two <see cref="PropertyDescriptor"/> objects.
    /// </summary>
    public class ChainedPropertyDescriptor : PropertyDescriptor 
    {
        public ChainedPropertyDescriptor(string name, PropertyDescriptor parent, PropertyDescriptor child) : base(name, child.Attributes.Cast<Attribute>().ToArray())
        {
            Parent = parent;
            Child = child;
        }

        public PropertyDescriptor Parent { get; private set; }
        public PropertyDescriptor Child { get; private set; }
        public override bool CanResetValue(object component)
        {
            object parentValue = Parent.GetValue(component);
            return parentValue != null && Child.CanResetValue(parentValue);
        }

        public override object GetValue(object component)
        {
            object parentValue = Parent.GetValue(component);
            if (parentValue == null)
            {
                return null;
            }
            return Child.GetValue(parentValue);
        }

        public override void ResetValue(object component)
        {
            var parentComponent = Parent.GetValue(component);
            if (null != parentComponent)
            {
                Child.ResetValue(parentComponent);
            }
        }

        public override void SetValue(object component, object value)
        {
            object parentValue = Parent.GetValue(component);
            if (parentValue != null)
            {
                Child.SetValue(parentValue, value);
            }
        }

        public override bool ShouldSerializeValue(object component)
        {
            return Child.ShouldSerializeValue(component);
        }

        public override Type ComponentType
        {
            get { return Parent.ComponentType; }
        }

        public override bool IsReadOnly
        {
            get { return Child.IsReadOnly; }
        }

        public override Type PropertyType
        {
            get { return Child.PropertyType; }
        }
    }
}
