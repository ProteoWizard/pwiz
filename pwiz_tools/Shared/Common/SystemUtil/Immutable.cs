/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Implement on an <see cref="Immutable"/> subclass to cause a <see cref="Validate"/>
    /// function to be called during <see cref="Immutable.ChangeProp{T,TProp}"/> calls.
    /// </summary>
    public interface IValidating
    {
        /// <summary>
        /// Called after the <see cref="Immutable.SetProperty{T,TProp}"/> function is
        /// executed in a call to <see cref="Immutable.ChangeProp{T,TProp}"/>.
        /// </summary>
        void Validate();
    }

    /// <summary>
    /// Provides utility functions for working with immutable objects.
    /// </summary>
    public class Immutable
    {
        /// <summary>
        /// Wraps a <see cref="IList{T}"/> in a <see cref="ReadOnlyCollection{T}"/>
        /// if it is not already one.  All <see cref="Immutable"/> objects returning
        /// <see cref="IList{T}"/> properties, should use this to ensure the lists
        /// are also immutable.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements</typeparam>
        /// <param name="list">The original list</param>
        /// <returns>A read-only list</returns>
        protected static ImmutableList<TItem> MakeReadOnly<TItem>(IEnumerable<TItem> list)
        {
            // If not already read-only, make readonly, and if not already an array
            // convert to an array for minimum allocation overhead.
            return ImmutableList.ValueOf(list);
        }

        /// <summary>
        /// Wraps a <see cref="IDictionary{TKey,TValue}"/> in a <see cref="ImmutableDictionary{TKey,TValue}"/>
        /// if it is not already one.  All <see cref="Immutable"/> objects returning
        /// <see cref="IDictionary{TKey,TValue}"/> properties, should use this to ensure the dictionaries
        /// are also immutable.
        /// </summary>
        /// <typeparam name="TKey">Type of the dictionary keys</typeparam>
        /// <typeparam name="TValue">Type of the dictionary values</typeparam>
        /// <param name="dict">The original dictionary</param>
        /// <returns>A read-only dictionary</returns>
        protected static ImmutableDictionary<TKey,TValue> MakeReadOnly<TKey,TValue>(IDictionary<TKey, TValue> dict)
        {
            return dict as ImmutableDictionary<TKey, TValue> ?? new ImmutableDictionary<TKey, TValue>(dict);
        }

        /// <summary>
        /// Creates a <see cref="object.MemberwiseClone"/> of a supplied
        /// <see cref="Immutable"/> and casts it to its original type.  For use in
        /// creating brief readable property setters for <see cref="Immutable"/> objects.
        /// </summary>
        /// <typeparam name="TIm">Type of the original object</typeparam>
        /// <param name="immutable">Instance object to clone</param>
        /// <returns>A shallow copy of the <see cref="Immutable"/> correctly typed</returns>
        protected static TIm ImClone<TIm>(TIm immutable)
            where TIm : Immutable
        {
            return (TIm)immutable.MemberwiseClone();
        }

        /// <summary>
        /// Delegate usually provided through a lambda expression in conjunction with
        /// <see cref="Immutable.ChangeProp{T,TValue}"/> to create public single-line,
        /// single-property change methods for <see cref="Immutable"/> objects,
        /// where all property setters must be private, and changing a property requires
        /// cloning the original object.
        /// 
        /// The exressions take the following form:
        /// 
        /// (im, v) => im.Name = v
        /// </summary>
        /// <typeparam name="TIm">Type of the node being changed</typeparam>
        /// <typeparam name="TProp">Type of the property to set</typeparam>
        /// <param name="immutable">The node instance to change</param>
        /// <param name="value">The value to assign to the property in the clone</param>
        protected delegate void SetProperty<in TIm, in TProp>(TIm immutable, TProp value);

        /// <summary>
        /// Use to create concise single-property change methods for
        /// <see cref="Immutable"/> objects, where all property setters must be private,
        /// and changing a property requires cloning the original object.
        /// 
        /// These expressions take the form:
        /// 
        /// public PeptideGroupDocNode ChangeName(string name)
        /// {
        ///     return ChangeProp(ImClone(this), (im, v) => im.Name = v, name);
        /// }
        /// 
        /// This took some playing to get right, since the value must be set on
        /// the clone, and not the this object on which the function is called.
        /// And getting rid of all type casting was tricky.
        /// </summary>
        /// <typeparam name="TIm">Type of the node being changed</typeparam>
        /// <typeparam name="TProp">Type of the property to set</typeparam>
        /// <param name="immutable">A cloned node on which to set the property</param>
        /// <param name="set">The delegate used to set the property (usually a lambda experssion)</param>
        /// <param name="value">The value to set the property to</param>
        /// <returns>The modified clone instance</returns>
        protected static TIm ChangeProp<TIm, TProp>(TIm immutable, SetProperty<TIm, TProp> set, TProp value)
            where TIm : Immutable
        {
// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ExpressionIsAlwaysNull
// ReSharper disable HeuristicUnreachableCode
            set(immutable, value);
            var validating = immutable as IValidating;
            if (validating != null)
                validating.Validate();
            return immutable;
// ReSharper restore HeuristicUnreachableCode
// ReSharper restore ExpressionIsAlwaysNull
// ReSharper restore ConditionIsAlwaysTrueOrFalse
// ReSharper restore SuspiciousTypeConversion.Global
        }

        /// <summary>
        /// Delegate provided through a lambda expression in conjunction with
        /// <see cref="Immutable.ChangeProp{T}"/> to create public single-line,
        /// single-property change methods for <see cref="Immutable"/> objects,
        /// where all property setters must be private, and changing a property requires
        /// cloning the original object.
        /// 
        /// The exressions take the following form:
        /// 
        /// im => im.Name = prop
        /// 
        /// Where prop is a captured local variable in the surrounding function.
        /// 
        /// This simpler version suggested by Nick Shulman.
        /// </summary>
        /// <typeparam name="TIm">Type of the node being changed</typeparam>
        /// <param name="immutable">The node instance to change</param>
        protected delegate void SetLambda<in TIm>(TIm immutable);

        /// <summary>
        /// Use to create more concise single-property change methods for
        /// <see cref="Immutable"/> objects, where all property setters must be private,
        /// and changing a property requires cloning the original object.
        /// 
        /// These expressions take the form:
        /// 
        /// public PeptideGroupDocNode ChangeName(string name)
        /// {
        ///     return ChangeProp(ImClone(this), im => im.Name = name);
        /// }
        /// 
        /// Where prop is a captured local variable in the surrounding function.
        /// 
        /// This simpler version suggested by Nick Shulman.
        /// </summary>
        /// <typeparam name="TIm">Type of the node being changed</typeparam>
        /// <param name="immutable">A cloned node on which to set the property</param>
        /// <param name="set">The delegate used to set the property (usually a lambda experssion)</param>
        /// <returns>The modified clone instance</returns>
        protected static TIm ChangeProp<TIm>(TIm immutable, SetLambda<TIm> set)
            where TIm : Immutable
        {
            // ReSharper disable SuspiciousTypeConversion.Global
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            // ReSharper disable ExpressionIsAlwaysNull
            // ReSharper disable HeuristicUnreachableCode
            set(immutable);
            var validating = immutable as IValidating;
            if (validating != null)
                (validating).Validate();
            return immutable;
            // ReSharper restore HeuristicUnreachableCode
            // ReSharper restore ExpressionIsAlwaysNull
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
            // ReSharper restore SuspiciousTypeConversion.Global
        }
    }
}
