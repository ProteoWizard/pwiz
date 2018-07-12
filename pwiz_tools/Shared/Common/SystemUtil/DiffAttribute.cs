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

namespace pwiz.Common.SystemUtil
{
    public interface IAuditLogObject
    {
        string AuditLogText { get; }
        bool IsName { get; }
    }

    public abstract class DiffAttributeBase : Attribute
    {
        protected DiffAttributeBase(bool isTab, bool ignoreName, Type customLocalizer)
        {
            IsTab = isTab;
            IgnoreName = ignoreName;
            CustomLocalizer = customLocalizer;
        }

        public bool IsTab { get; protected set; }
        public bool IgnoreName { get; protected set; }

        public virtual bool DiffProperties { get { return false; } }

        public Type CustomLocalizer;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DiffAttribute : DiffAttributeBase
    {
        public DiffAttribute(bool isTab = false,
            bool ignoreName = false,
            Type customLocalizer = null)
            : base(isTab, ignoreName, customLocalizer) { }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DiffParentAttribute : DiffAttributeBase
    {
        public DiffParentAttribute(bool isTab = false,
            bool ignoreName = false,
            Type customLocalizer = null)
            : base(isTab, ignoreName, customLocalizer) { }

        public override bool DiffProperties { get { return true; } }
    }
}
