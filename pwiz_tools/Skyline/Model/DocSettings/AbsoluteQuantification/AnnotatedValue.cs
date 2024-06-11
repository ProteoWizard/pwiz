/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public static class AnnotatedValue
    {
        public static AnnotatedValue<T> Of<T>(T value)
        {
            return new AnnotatedValue<T>(value, null);
        }

        public static AnnotatedValue<T> WithErrorMessage<T>(T value, string error)
        {
            return new AnnotatedValue<T>(value, error);
        }
    }
    public readonly struct AnnotatedValue<T>
    {
        public AnnotatedValue(T value, string errorMessage)
        {
            Value = value;
            ErrorMessage = errorMessage;
        }
        public T Value { get; }
        public string ErrorMessage { get; }

        public bool Equals(AnnotatedValue<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value) && ErrorMessage == other.ErrorMessage;
        }

        public override bool Equals(object obj)
        {
            return obj is AnnotatedValue<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(Value) * 397) ^ (ErrorMessage != null ? ErrorMessage.GetHashCode() : 0);
            }
        }
    }
}
