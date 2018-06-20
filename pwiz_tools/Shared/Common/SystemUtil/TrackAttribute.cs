/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    public interface IAuditLogObject
    {
        string AuditLogText { get; }
        // Determines whether the AuditLogText is a name or a string representation
        // of the object
        bool IsName { get; }
    }

    public interface IAuditLogComparable
    {
        object DefaultObject { get; }
    }

    public abstract class DefaultValues
    {
        protected virtual IEnumerable<object> _values
        {
            get { return Enumerable.Empty<object>(); }
        }

        public virtual bool IsDefault(object obj, object parentObject)
        {
            return _values.Contains(obj);
        }

        public static DefaultValues CreateInstance(Type defaultValuesType)
        {
            return (DefaultValues) Activator.CreateInstance(defaultValuesType);
        }
    }

    public class DefaultValuesNull : DefaultValues
    {
        protected override IEnumerable<object> _values
        {
            get { yield return null; }
        }
    }

    public abstract class TrackAttributeBase : Attribute
    {
        protected TrackAttributeBase(bool isTab, bool ignoreName, Type defaultValues, Type customLocalizer)
        {
            IsTab = isTab;
            IgnoreName = ignoreName;
            DefaultValues = defaultValues;
            CustomLocalizer = customLocalizer;
        }

        public bool IsTab { get; protected set; }
        public bool IgnoreName { get; protected set; }
        

        public bool IgnoreNull { get; protected set; }
        public virtual bool DiffProperties { get { return false; } }

        public Type DefaultValues { get; protected set; }
        public Type CustomLocalizer { get; protected set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class TrackAttribute : TrackAttributeBase
    {
        public TrackAttribute(bool isTab = false,
            bool ignoreName = false,
            Type defaultValues = null,
            Type customLocalizer = null)
            : base(isTab, ignoreName, defaultValues, customLocalizer) { }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class TrackChildrenAttribute : TrackAttributeBase
    {
        public TrackChildrenAttribute(bool isTab = false,
            bool ignoreName = false,
            Type defaultValues = null,
            Type customLocalizer = null)
            : base(isTab, ignoreName, defaultValues, customLocalizer) { }

        public override bool DiffProperties { get { return true; } }
    }
}
