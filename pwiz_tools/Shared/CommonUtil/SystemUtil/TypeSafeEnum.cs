/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Ensures that the enum value is one of the explicitly defined values in the Enum struct.
    /// All Enum structs can take on any integer value.  Usually, it is desirable to only allow
    /// values that were explicitly listed in the enum definition.  This wrapper class only allows
    /// values that were defined in the Enum struct.
    /// 
    /// Note that this class always allows the default (i.e. zero) enum value, since there is
    /// no practical way to prevent that.
    /// </summary>
    public struct TypeSafeEnum<T> where T : struct
    {
        private readonly T _value;

        public TypeSafeEnum(T value)
        {
            TypeSafeEnum.Validate(value);
            _value = value;
        }

        public T Value { get { return _value; } }

        public static implicit operator TypeSafeEnum<T>(T value)
        {
            return new TypeSafeEnum<T>(value);
        }
        public static implicit operator T(TypeSafeEnum<T> value)
        {
            return value.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    /// <summary>
    /// Helper methods for constructing and working with <see cref="TypeSafeEnum{T}"/> values.
    /// </summary>
    public static class TypeSafeEnum
    {
        public static T Validate<T>(T value) where T : struct
        {
            if (!IsValid(value))
            {
                throw new ArgumentException();
            }
            return value;
        }

        public static void ValidateList<T>(IEnumerable<T> items) where T : struct
        {
            foreach (var item in items)
            {
                Validate(item);
            }
        }

        public static T Parse<T>(string text) where T : struct
        {
            return Validate((T)Enum.Parse(typeof(T), text));
        }

        public static T ValidateOrDefault<T>(T value, T defaultValue) where T : struct
        {
            if (IsValid(value))
            {
                return value;
            }
            return defaultValue;
        }

        public static bool IsValid<T>(T value) where T : struct
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return Equals(value, default(T)) || typeof(T).IsEnumDefined(value);
        }
    }
}
