/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Provides compiled getters for the descriptors which <see cref="TypeDescriptor"/> creates through
    /// reflection, so that a report can read a property without going through <see cref="MethodInfo.Invoke"/>.
    /// Reflection invocation costs far more than the property getter itself: security checks,
    /// argument validation and target lookup were close to half of the CPU time when exporting a large report.
    /// Only the framework's own reflected descriptor is handled, because that is the only descriptor
    /// whose GetValue is known to be a plain call of the property getter. Any subclass of
    /// PropertyDescriptor may compute its value some other way and gets no compiled getter.
    /// </summary>
    public static class ReflectedPropertyGetter
    {
        /// <summary>
        /// The type name of the descriptor which <see cref="TypeDescriptor.GetProperties(Type)"/> returns for
        /// an ordinary property.
        /// </summary>
        private const string REFLECT_PROPERTY_DESCRIPTOR = @"System.ComponentModel.ReflectPropertyDescriptor";

        /// <summary>
        /// Compiling a getter is expensive, so each property is compiled at most once.
        /// The key includes the component type: PropertyDescriptor.Equals only compares the name and
        /// property type, so it would treat Protein.Name and Replicate.Name as the same property.
        /// </summary>
        private static readonly ConcurrentDictionary<Tuple<Type, string>, Func<object, object>> _getters =
            new ConcurrentDictionary<Tuple<Type, string>, Func<object, object>>();

        /// <summary>
        /// Returns a compiled getter equivalent to <see cref="PropertyDescriptor.GetValue"/> if the descriptor
        /// is the framework's plain reflected descriptor and its getter can be compiled. Otherwise returns null,
        /// and the caller should call <see cref="PropertyDescriptor.GetValue"/> as usual.
        /// </summary>
        public static Func<object, object> TryGetGetter(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.GetType().FullName != REFLECT_PROPERTY_DESCRIPTOR)
            {
                return null;
            }
            return _getters.GetOrAdd(Tuple.Create(propertyDescriptor.ComponentType, propertyDescriptor.Name),
                key => CompileGetter(propertyDescriptor));
        }

        /// <summary>
        /// Builds the equivalent of <c>component => (object) ((TComponent) component).Property</c>.
        /// Returns null if the property cannot be found unambiguously as a public, non-indexed instance
        /// property with a public getter.
        /// </summary>
        private static Func<object, object> CompileGetter(PropertyDescriptor propertyDescriptor)
        {
            PropertyInfo propertyInfo;
            try
            {
                propertyInfo = propertyDescriptor.ComponentType.GetProperty(propertyDescriptor.Name,
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (AmbiguousMatchException)
            {
                return null;
            }
            if (propertyInfo == null || propertyInfo.PropertyType != propertyDescriptor.PropertyType ||
                propertyInfo.GetGetMethod() == null || propertyInfo.GetIndexParameters().Length != 0)
            {
                return null;
            }
            var component = Expression.Parameter(typeof(object), @"component");
            var body = Expression.Convert(
                Expression.Property(Expression.Convert(component, propertyDescriptor.ComponentType), propertyInfo),
                typeof(object));
            return Expression.Lambda<Func<object, object>>(body, component).Compile();
        }
    }
}
