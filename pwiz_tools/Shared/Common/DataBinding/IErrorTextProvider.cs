/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Interface which, if implemented by a class, can provide error text to be displayed in a
    /// <see cref="Controls.BoundDataGridView"/>.
    /// </summary>
    public interface IErrorTextProvider
    {
        string GetErrorText(string columnName);
    }

    public class ImmutableErrorTextProvider : Immutable, IErrorTextProvider
    {
        private ImmutableSortedList<string, string> _errors;

        public virtual string GetErrorText(string columnName)
        {
            string error = null;
            _errors?.TryGetValue(columnName, out error);
            return error;
        }

        public ImmutableErrorTextProvider ChangeErrorText(string columnName, string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                if (true != _errors?.ContainsKey(columnName))
                {
                    return this;
                }
                return ChangeProp(ImClone(this), im => im._errors = im._errors.RemoveKey(columnName));
            }

            return ChangeProp(ImClone(this), im =>
            {
                if (im._errors == null)
                {
                    im._errors = ImmutableSortedList.FromValues(new[]
                        { new KeyValuePair<string, string>(columnName, error) });
                }
                else
                {
                    im._errors = im._errors.Replace(columnName, error);
                }
            });
        }

        protected bool Equals(ImmutableErrorTextProvider other)
        {
            return Equals(_errors, other._errors);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImmutableErrorTextProvider)obj);
        }

        public override int GetHashCode()
        {
            return _errors != null ? _errors.GetHashCode() : 0;
        }
    }
}
