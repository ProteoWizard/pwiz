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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Class used by audit log to store the object and its "relatives"
    /// that are currently being processed
    /// </summary>
    /// <typeparam name="T">Type of the underlying object</typeparam>
    public class ObjectInfo<T> : Immutable where T : class
    {
        public ObjectInfo(T oldObject, T newObject, T oldParentObject, T newParentObject,
            T oldRootObject, T newRootObject)
        {
            OldObject = oldObject;
            NewObject = newObject;
            OldParentObject = oldParentObject;
            NewParentObject = newParentObject;
            OldRootObject = oldRootObject;
            NewRootObject = newRootObject;
        }

        public ObjectInfo() : this(null, null, null, null, null, null)
        {
        }

        public ObjectInfo<object> ToObjectType()
        {
            return new ObjectInfo<object>(OldObject, NewObject, OldParentObject, NewParentObject, OldRootObject,
                NewRootObject);
        }

        public T OldObject { get; private set; }
        public T NewObject { get; private set; }
        public T OldParentObject { get; private set; }
        public T NewParentObject { get; private set; }
        public T OldRootObject { get; private set; }
        public T NewRootObject { get; private set; }

        public ObjectInfo<T> ChangeOldObject(T oldObject)
        {
            return ChangeProp(ImClone(this), im => im.OldObject = oldObject);
        }

        public ObjectInfo<T> ChangeNewObject(T newObject)
        {
            return ChangeProp(ImClone(this), im => im.NewObject = newObject);
        }

        public ObjectInfo<T> ChangeObjectPair(ObjectPair<T> objectPair)
        {
            return ChangeProp(ImClone(this), im => im.ObjectPair = objectPair);
        }

        public ObjectInfo<T> ChangeParentPair(ObjectPair<T> parentPair)
        {
            return ChangeProp(ImClone(this), im => im.ParentObjectPair = parentPair);
        }

        public ObjectInfo<T> ChangeRootObjectPair(ObjectPair<T> rootObjectPair)
        {
            return ChangeProp(ImClone(this), im => im.RootObjectPair = rootObjectPair);
        }

        public ObjectPair<T> ObjectPair
        {
            get { return ObjectPair<T>.Create(OldObject, NewObject); }
            private set { OldObject = value.OldObject; NewObject = value.NewObject; }
        }

        public ObjectPair<T> ParentObjectPair
        {
            get { return ObjectPair<T>.Create(OldParentObject, NewParentObject); }
            private set { OldParentObject = value.OldObject; NewParentObject = value.NewObject; }
        }

        public ObjectPair<T> RootObjectPair
        {
            get { return ObjectPair<T>.Create(OldRootObject, NewRootObject); }
            private set { OldRootObject = value.OldObject; NewRootObject = value.NewObject; }
        }

        public ObjectGroup<T> OldObjectGroup
        {
            get { return ObjectGroup<T>.Create(OldObject, OldParentObject, OldRootObject); }
        }

        public ObjectGroup<T> NewObjectGroup
        {
            get { return ObjectGroup<T>.Create(NewObject, NewParentObject, NewRootObject); }
            private set { NewObject = value.Object; NewParentObject = value.ParentObject; NewRootObject = value.RootObject; }
        }
    }

    public class ObjectPair<T> : Immutable
    {
        public ObjectPair(T oldObject, T newObject)
        {
            OldObject = oldObject;
            NewObject = newObject;
        }

        public static ObjectPair<T> Create(T oldObj, T newObj)
        {
            return new ObjectPair<T>(oldObj, newObj);
        }

        public ObjectPair<T> ChangeOldObject(T oldObject)
        {
            return ChangeProp(ImClone(this), im => im.OldObject = oldObject);
        }

        public ObjectPair<T> ChangeNewObject(T newObject)
        {
            return ChangeProp(ImClone(this), im => im.NewObject = newObject);
        }

        public ObjectPair<S> Transform<S>(Func<T, S> func)
        {
            return Transform(func, func);
        }

        public ObjectPair<S> Transform<S>(Func<T, S> oldFunc, Func<T, S> newFunc)
        {
            return ObjectPair<S>.Create(oldFunc(OldObject), newFunc(NewObject));
        }

        public bool Equals()
        {
            return Equals(OldObject, NewObject);
        }

        public bool ReferenceEquals()
        {
            return ReferenceEquals(OldObject, NewObject);
        }

        public T OldObject { get; private set; }
        public T NewObject { get; private set; }
    }

    public static class ObjectPair
    {
        public static ObjectPair<T> Create<T>(T oldObj, T newObj)
        {
            return ObjectPair<T>.Create(oldObj, newObj);
        }
    }

    public class ObjectGroup<T>
    {
        public ObjectGroup(T obj, T parentObject, T rootObject)
        {
            Object = obj;
            ParentObject = parentObject;
            RootObject = rootObject;
        }

        public static ObjectGroup<T> Create(T obj, T parentObject, T rootObject)
        {
            return new ObjectGroup<T>(obj, parentObject, rootObject);
        }

        public T Object { get; private set; }
        public T ParentObject { get; private set; }
        public T RootObject { get; private set; }
    }

    /// <summary>
    /// Provides custom string representation of things that appear in the audit log.
    /// </summary>
    public interface IAuditLogObject
    {
        /// <summary>
        /// String representation or Name of the object.
        /// </summary>
        string AuditLogText { get; }
        /// <summary>
        /// Returns true if the audit log text is the name of this object.
        /// That is, the audit log text provides no information about the current state and properties of this object.
        /// Returns false if the audit log text is a description of this object.
        /// </summary>
        bool IsName { get; }
    }

    internal static class CharToResourceStringMap
    {
        private static Dictionary<char, string> _charToResourceStringMap = new Dictionary<char, string>
        {
            {'-', @"_minus_"},
            {'+', @"_plus_"},
            {'<', @"_lt_" },
            {'>', @"_gt_" },
        };

        public static void AppendResourceChar(this StringBuilder sb, char c)
        {
            string s;
            if (_charToResourceStringMap.TryGetValue(c, out s))
                sb.Append(s);
            else
                sb.Append('_');
        }
    }
    public abstract class LabeledValues<T> : IAuditLogObject
    {
        protected readonly Func<string> _getLabel;
        protected readonly Func<string> _getInvariantName;

        protected LabeledValues(T name, Func<string> getLabel, Func<string> getInvariantName = null)
        {
            Name = name;
            _getLabel = getLabel;
            _getInvariantName = getInvariantName;
        }

        public T Name { get; private set; }

        protected virtual string InvariantName
        {
            get { return (_getInvariantName ?? GetValidResourceName(Name.ToString()))(); }
        }

        public virtual string Label
        {
            get { return _getLabel(); }
        }

        public virtual string AuditLogText
        {
            get
            {
                if (!RequiresAuditLogLocalization)
                    return Label;

                return AuditLogParseHelper.GetParseString(ParseStringType.enum_fn,
                    GetType().Name + '_' + InvariantName);
            }
        }

        public static Func<string> GetValidResourceName(string str)
        {
            var sb = new StringBuilder(str.Length);

            if (string.IsNullOrEmpty(str))
                return () => str;

            foreach (var c in str)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.AppendResourceChar(c);
            }

            var s = sb.ToString();
            return () => s;
        }

        public virtual bool RequiresAuditLogLocalization
        {
            get { return true; }
        }

        public bool IsName
        {
            get { return true; }
        }
    }

    // These values get written into audit logs by index and can therefore
    // not be changed.
    public enum ParseStringType
    {
        property_names,
        property_element_names,
        audit_log_strings,
        primitive,
        path,
        column_caption,
        enum_fn
    }

    public class AuditLogParseHelper
    {
        // Construct a string that can will be stored in the audit log, and when
        // read, the corresponding function from PARSE_FUNCTIONS is called with the given
        // invariant string as parameter
        public static string GetParseString(ParseStringType stringType, string invariantString)
        {
            var index = (int)stringType;
            return string.Format(CultureInfo.InvariantCulture, @"{{{0}:{1}}}", index, invariantString);
        }
    }

    public interface IAuditLogComparable
    {
        object GetDefaultObject(ObjectInfo<object> info);
    }

    public abstract class DefaultValues
    {
        public IEnumerable<object> Values
        {
            get { return _values; }
        }

        protected virtual IEnumerable<object> _values
        {
            get { return Enumerable.Empty<object>(); }
        }

        public virtual bool IsDefault(object obj, object parentObject)
        {
            return _values.Any(v => ReferenceEquals(v, obj));
        }

        public virtual bool IgnoreIfDefault
        {
            get { return false; }
        }

        public static DefaultValues CreateInstance(Type defaultValuesType)
        {
            if (defaultValuesType == null)
                return null;

            return (DefaultValues) Activator.CreateInstance(defaultValuesType);
        }
    }

    public class DefaultValuesStringNullOrEmpty : DefaultValues
    {
        public override bool IsDefault(object obj, object parentObject)
        {
            var str = obj as string;
            if (obj != null && str == null)
                return false;
            return string.IsNullOrEmpty(str);
        }
    }

    public class DefaultValuesFalse : DefaultValues
    {
        protected override IEnumerable<object> _values
        {
            get { yield return false; }
        }
    }

    public class DefaultValuesTrue : DefaultValues
    {
        protected override IEnumerable<object> _values
        {
            get { yield return true; }
        }
    }

    public class DefaultValuesNull : DefaultValues
    {
        protected override IEnumerable<object> _values
        {
            get { yield return null; }
        }
    }

    public class DefaultValuesNullOrEmpty : DefaultValues
    {
        public static bool IsEmpty(object obj)
        {
            var enumerable = obj as IEnumerable;
            if (enumerable == null)
                return false;

            // If the collection is empty the first call to MoveNext will return false
            return !enumerable.GetEnumerator().MoveNext();
        }

        public override bool IsDefault(object obj, object parentObject)
        {
            return obj == null || IsEmpty(obj);
        }
    }

    public class DefaultValuesZero : DefaultValues
    {
        protected override IEnumerable<object> _values
        {
            get { yield return 0; }
        }
    }

    public abstract class TrackAttributeBase : Attribute
    {
        protected TrackAttributeBase(bool isTab, bool ignoreName, bool ignoreDefaultParent, Type defaultValues,
            Type customLocalizer, int decimalPlaces = -1)
        {
            IsTab = isTab;
            IgnoreName = ignoreName;
            IgnoreDefaultParent = ignoreDefaultParent;
            DefaultValues = defaultValues;
            CustomLocalizer = customLocalizer;
            DecimalPlaces = decimalPlaces;
        }

        /// <summary>
        /// Returns true if a "--" should be used to separate this property from its parent. Returns false if it should be ">" or ":".
        /// </summary>
        public bool IsTab { get; protected set; }
        /// <summary>
        /// Returns true if the sub-properties of this property value should be merged with the parent.
        /// That is, the name of this property gets ignored, and it is as if the properties of this sub-object are properties on the parent.
        /// </summary>
        public bool IgnoreName { get; protected set; }
        /// <summary>
        /// Returns true if the audit log engine should recurse into the sub-properties of this property value.
        /// </summary>
        public virtual bool DiffProperties { get { return false; } }
        /// <summary>
        /// Whether to display this property value, even if this property value is the default value.
        /// </summary>
        public bool IgnoreDefaultParent { get; protected set; }
        /// <summary>
        /// Class which extends <see cref="pwiz.Common.SystemUtil.DefaultValues" /> which provides the default values of this property.
        /// </summary>
        public Type DefaultValues { get; protected set; }
        /// <summary>
        /// Class which extends CustomPropertyLocalizer 
        /// </summary>
        public Type CustomLocalizer { get; protected set; }
        /// <summary>
        /// Number of decimal places to round the value to. Defaults to -1 for no rounding.
        /// </summary>
        public int DecimalPlaces { get; protected set; }
    }

    /// <summary>
    /// Can be used on enums to indicate that certain values don't have to be localized. Used for testing only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public class IgnoreEnumValuesAttribute : Attribute
    {
        public static readonly IgnoreEnumValuesAttribute NONE = new IgnoreEnumValuesAttribute(new object[0]);

        protected ImmutableList<object> _ignoreValues;
        public IgnoreEnumValuesAttribute(object[] values)
        {
            _ignoreValues = ImmutableList.ValueOf(values);
        }

        public virtual bool ShouldIgnore(object obj)
        {
            return _ignoreValues.Contains(obj);
        }
    }

    /// <summary>
    /// Use this attribute on properties where you do not want to recurse into the sub-properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TrackAttribute : TrackAttributeBase
    {
        public TrackAttribute(bool isTab = false,
            bool ignoreName = false,
            bool ignoreDefaultParent = false,
            Type defaultValues = null,
            Type customLocalizer = null,
            int decimalPlaces = -1)
            : base(isTab, ignoreName, ignoreDefaultParent, defaultValues, customLocalizer, decimalPlaces) { }
    }

    /// <summary>
    /// Use this attribute to tell the Audit Log engine to recurse into the sub-properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TrackChildrenAttribute : TrackAttributeBase
    {
        public TrackChildrenAttribute(bool isTab = false,
            bool ignoreName = false,
            bool ignoreDefaultParent = false,
            Type defaultValues = null,
            Type customLocalizer = null)
            : base(isTab, ignoreName, ignoreDefaultParent, defaultValues, customLocalizer) { }

        public override bool DiffProperties { get { return true; } }
    }
}
