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

using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.AuditLog
{
    /// <summary>
    /// Similar to <see cref="PropertyPath"/> but much more simple and different formatting.
    /// PropertyNames actually get displayed to the users (unlike PropertyPaths)
    /// Name of a property
    /// </summary>
    public class PropertyName : Immutable
    {
        public static readonly PropertyName ROOT = new PropertyName(null, null);

        public PropertyName(string name)
            : this(ROOT, name)
        {
        }

        public PropertyName(PropertyName parent, string name)
        {
            Parent = parent;
            Name = name;
        }

        public PropertyName SubProperty(string name)
        {
            return new PropertyName(this, name);
        }

        public PropertyName SubProperty(PropertyName propertyName)
        {
            return ChangeProp(ImClone(propertyName), im => im.Parent = this);
        }

        public override string ToString()
        {
            return ToString(this);
        }

        protected virtual string Format()
        {
            return Name;
        }

        public virtual string Separator { get { return @"{2:PropertySeparator}"; } }

        private static string ToString(PropertyName name)
        {
            var text = name.Format();

            if (ReferenceEquals(name.Parent, ROOT))
                return text;
            else
                return ToString(name.Parent) + name.Separator + text;   
        }

        public PropertyName Parent { get; private set; }

        public string Name { get; private set; }
        public virtual bool IsElement { get { return false; } }
    }

    /// <summary>
    /// Name of an element in a collection
    /// </summary>
    public class PropertyElementName : PropertyName
    {
        public PropertyElementName(string name) : base(name) { }

        /*protected override string Format()
        {
            return string.Format("\"{0}\"", Name);
        }*/

        public override bool IsElement { get { return true; } }
    }

    /// <summary>
    /// Name of a property that represents a tabbed pane in the UI
    /// </summary>
    public class PropertyTabName : PropertyName
    {
        public PropertyTabName(string name) : base(name) { }

        public override string Separator { get { return @"{2:TabSeparator}"; } }
    }
}
