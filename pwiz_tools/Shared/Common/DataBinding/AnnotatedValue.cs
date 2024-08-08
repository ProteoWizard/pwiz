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

using System;
using System.Collections.Generic;

namespace pwiz.Common.DataBinding
{
    public interface IAnnotatedValue
    {
        string GetErrorMessage();
    }

    public class AnnotatedValue<T> : IAnnotatedValue, IComparable
    {
        public AnnotatedValue(T value, string message) : this(value, message == null ? value : default, message)
        {
            Raw = value;
            Message = message;
            Strict = Message == null ? Raw : default;
        }

        public AnnotatedValue(T raw, T strict, string message)
        {
            Raw = raw;
            Strict = strict;
            Message = message;
        }

        public T Raw { get; }
        public T Strict { get; }
        public string Message { get; }

        public string GetErrorMessage()
        {
            return Message;
        }

        public bool Equals(AnnotatedValue<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Raw, other.Raw) &&
                   EqualityComparer<T>.Default.Equals(Strict, other.Strict) && Message == other.Message;
        }

        public override bool Equals(object obj)
        {
            return obj is AnnotatedValue<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = EqualityComparer<T>.Default.GetHashCode(Raw);
                hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(Strict);
                hashCode = (hashCode * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return GetPrefix() + Raw;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            var thisKey = Tuple.Create(Raw, Message, Strict);
            var that = (AnnotatedValue<T>)obj;
            var thatKey = Tuple.Create(that.Raw, that.Message, that.Strict);
            return ((IComparable)thisKey).CompareTo(thatKey);
        }

        protected string GetPrefix()
        {
            return GetPrefix(Message);
        }

        public static string GetPrefix(string message)
        {
            return message == null ? string.Empty : @"*";
        }
    }
}
