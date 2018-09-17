/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// An ElementRef is an intermediate object which can be easily constructed from and converted to an ElementLocator.
    /// All ElementRef classes have a static final field which is the prototypical ElementRef of that type.
    /// The prototypical ElementRef has parent of the appropriate type, and has a blank name and an empty set of
    /// attributes.
    /// </summary>
    public abstract class ElementRef : Immutable
    {
        private const string ATTR_INDEX = "index";

        protected ElementRef(ElementRef parent)
        {
            Parent = parent;
            Prototype = this;
        }

        public ElementRef Parent { get; private set; }
        public ElementRef Prototype { get; private set; }
        public int Index { get; private set; }
        public string Name { get; private set; }
        public abstract string ElementType { get; }

        public ElementRef ChangeIndex(int index)
        {
            return ChangeProp(ImClone(this), im => im.Index = index);
        }

        public ElementRef ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }

        public ElementRef ChangeParent(ElementRef parent)
        {
            return ChangeProp(ImClone(this), im => im.Parent = parent);
        }

        protected virtual IEnumerable<KeyValuePair<string, string>> GetAttributes()
        {
            if (Index == 0)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }
            return new[] {new KeyValuePair<string, string>(ATTR_INDEX, Index.ToString(CultureInfo.InvariantCulture))};
        }

        public virtual ElementLocator ToElementLocator()
        {
            ElementLocator parentLocator;
            if (Parent == null)
            {
                parentLocator = null;
            }
            else
            {
                parentLocator = Parent.ToElementLocator().ChangeType(null);
            }
            return new ElementLocator(Name, GetAttributes())
                .ChangeParent(parentLocator).ChangeType(ElementType);
        }

        public sealed override string ToString()
        {
            return ToElementLocator().ToString();
        }

        public ElementRef ChangeElementLocator(ElementLocator objectReference)
        {
            var result = ChangeProp(ImClone(this), im => im.SetElementLocator(objectReference));
            if (result.Parent != null)
            {
                result = result.ChangeParent(result.Parent.ChangeElementLocator(objectReference.Parent));
            }
            return result;
        }

        protected virtual void SetElementLocator(ElementLocator objectReference)
        {
            Name = objectReference.Name;
            Index = ParseInt(objectReference.FindAttribute(ATTR_INDEX).Value);
        }

        public static KeyValuePair<string, string> MakeIntAttribute(string name, int value)
        {
            if (value == 0)
            {
                return default(KeyValuePair<string, string>);
            }
            return new KeyValuePair<string, string>(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static int ParseInt(string value)
        {
            if (value == null)
            {
                return 0;
            }
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        protected bool Equals(ElementRef other)
        {
            return Equals(Parent, other.Parent) && Index == other.Index && string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ElementRef) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Index;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                return hashCode;
            }
        }

        public IEnumerable<ElementRef> ListChildrenOfParent(SrmDocument document)
        {
            var genericChild = this;
            if (genericChild.Name != null)
            {
                genericChild = Prototype.ChangeParent(Parent);
            }
            var counts = new Dictionary<ElementRef, int>();
            foreach (var elementRef in genericChild.EnumerateSiblings(document))
            {
                int count;
                if (counts.TryGetValue(elementRef, out count))
                {
                    counts[elementRef] = count + 1;
                    yield return elementRef.ChangeIndex(count);
                }
                else
                {
                    counts.Add(elementRef, 1);
                    yield return elementRef;
                }
            }
        }

        protected abstract IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document);

        public virtual AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get { return AnnotationDef.AnnotationTargetSet.EMPTY; }
        }
    }
}
