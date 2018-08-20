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
using System.Linq;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Class used by audit log to store the object and its "relatives"
    /// that are currently being processed
    /// </summary>
    /// <typeparam name="T">Type of the underlying object</typeparam>
    public class ObjectInfo<T> : Immutable where T : class
    {
        public ObjectInfo(T oldObject = null, T newObject = null, T oldParentObject = null, T newParentObject = null,
            T oldRootObject = null, T newRootObject = null)
        {
            OldObject = oldObject;
            NewObject = newObject;
            OldParentObject = oldParentObject;
            NewParentObject = newParentObject;
            OldRootObject = oldRootObject;
            NewRootObject = newRootObject;
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

        public ObjectInfo<T> ChangeRootObjectPair(ObjectPair<T> rootObjectPair)
        {
            return ChangeProp(ImClone(this), im => im.RootObjectPair = rootObjectPair);
        }

        public ObjectInfo<T> ChangeParentPair(ObjectPair<T> parentPair)
        {
            return ChangeProp(ImClone(this), im => im.ParentObjectPair = parentPair);
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

    public class ObjectPair
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

    public interface IAuditLogObject
    {
        string AuditLogText { get; }
        // Determines whether the AuditLogText is a name or a string representation
        // of the object
        bool IsName { get; }
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

    public abstract class TrackAttributeBase : Attribute
    {
        protected TrackAttributeBase(bool isTab, bool ignoreName, bool ignoreDefaultParent, Type defaultValues,
            Type customLocalizer)
        {
            IsTab = isTab;
            IgnoreName = ignoreName;
            IgnoreDefaultParent = ignoreDefaultParent;
            DefaultValues = defaultValues;
            CustomLocalizer = customLocalizer;
        }

        public bool IsTab { get; protected set; }
        public bool IgnoreName { get; protected set; }
        public bool IgnoreNull { get; protected set; }
        public virtual bool DiffProperties { get { return false; } }
        public bool IgnoreDefaultParent { get; protected set; }
        public Type DefaultValues { get; protected set; }
        public Type CustomLocalizer { get; protected set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class TrackAttribute : TrackAttributeBase
    {
        public TrackAttribute(bool isTab = false,
            bool ignoreName = false,
            bool ignoreDefaultParent = false,
            Type defaultValues = null,
            Type customLocalizer = null)
            : base(isTab, ignoreName, ignoreDefaultParent, defaultValues, customLocalizer) { }
    }

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
