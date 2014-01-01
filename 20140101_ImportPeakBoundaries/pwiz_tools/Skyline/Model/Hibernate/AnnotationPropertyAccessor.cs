/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Reflection;
using NHibernate.Engine;
using NHibernate.Properties;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Hibernate
{
    /// <summary>
    /// Property accessor which returns an annotation value from a DbEntity
    /// object for Hibernate.
    /// </summary>
    public class AnnotationPropertyAccessor : IPropertyAccessor
    {
        public IGetter GetGetter(Type theClass, string propertyName)
        {
            return new Getter(AnnotationDef.GetColumnKey(propertyName));
        }

        public ISetter GetSetter(Type theClass, string propertyName)
        {
            return new Setter(AnnotationDef.GetColumnKey(propertyName));
        }

        public bool CanAccessThroughReflectionOptimizer
        {
            get { return false; }
        }

        private class Getter : IGetter
        {
            private readonly string _name;

            public Getter(String name)
            {
                _name = name;
            }

            public object Get(object target)
            {
                string value;
                ((DbEntity) target).Annotations.TryGetValue(_name, out value);
                return value;
            }

            public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
            {
                return Get(owner);
            }

            public Type ReturnType
            {
                get { return typeof (string); }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }

        private class Setter : ISetter
        {
            private readonly string _name;

            public Setter(String name)
            {
                _name = name;
            }

            public void Set(object target, object value)
            {
                var entity = (DbEntity) target;
                if (value == null)
                {
                    entity.Annotations.Remove(_name);
                }
                else
                {
                    entity.Annotations[_name] = value.ToString();
                }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }
    }

    public class BoolAnnotationPropertyAccessor : IPropertyAccessor
    {
        public IGetter GetGetter(Type theClass, string propertyName)
        {
            return new Getter(AnnotationDef.GetColumnKey(propertyName));
        }

        public ISetter GetSetter(Type theClass, string propertyName)
        {
            return new Setter(AnnotationDef.GetColumnKey(propertyName));
        }

        public bool CanAccessThroughReflectionOptimizer
        {
            get { return false; }
        }

        private class Getter : IGetter
        {
            private readonly string _name;

            public Getter(String name)
            {
                _name = name;
            }

            public object Get(object target)
            {
                string value;
                ((DbEntity)target).Annotations.TryGetValue(_name, out value);
                return value != null;
            }

            public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
            {
                return Get(owner);
            }

            public Type ReturnType
            {
                get { return typeof(bool); }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }

        private class Setter : ISetter
        {
            private readonly string _name;

            public Setter(String name)
            {
                _name = name;
            }

            public void Set(object target, object value)
            {
                var entity = (DbEntity)target;
                if (value == null || false.Equals(value))
                {
                    entity.Annotations.Remove(_name);
                }
                else
                {
                    entity.Annotations[_name] = _name;
                }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }
    }
    public class NumberAnnotationPropertyAccessor : IPropertyAccessor
    {
        public IGetter GetGetter(Type theClass, string propertyName)
        {
            return new Getter(AnnotationDef.GetColumnKey(propertyName));
        }

        public ISetter GetSetter(Type theClass, string propertyName)
        {
            return new Setter(AnnotationDef.GetColumnKey(propertyName));
        }

        public bool CanAccessThroughReflectionOptimizer
        {
            get { return false; }
        }

        private class Getter : IGetter
        {
            private readonly string _name;

            public Getter(String name)
            {
                _name = name;
            }

            public object Get(object target)
            {
                string value;
                ((DbEntity)target).Annotations.TryGetValue(_name, out value);
                return AnnotationDef.ParseNumber(value);
            }

            public object GetForInsert(object owner, IDictionary mergeMap, ISessionImplementor session)
            {
                return Get(owner);
            }

            public Type ReturnType
            {
                get { return typeof(double); }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }

        private class Setter : ISetter
        {
            private readonly string _name;

            public Setter(String name)
            {
                _name = name;
            }

            public void Set(object target, object value)
            {
                var entity = (DbEntity)target;
                if (value == null)
                {
                    entity.Annotations.Remove(_name);
                }
                else
                {
                    entity.Annotations[_name] = Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            }

            public string PropertyName
            {
                get { return null; }
            }

            public MethodInfo Method
            {
                get { return null; }
            }
        }
        
    }
}
