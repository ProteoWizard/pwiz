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
using System;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Lists
{
    public class ListExceptionDetail
    {
        private Func<string> _message;
        public ListExceptionDetail(string listName, Func<string> message)
        {
            _message = message;
            ListName = listName;
        }

        public string ListName { get; private set; }

        public String Message
        {
            get
            {
                return _message();
            }
        }

        public override string ToString()
        {
            return Message;
        }

        public static ListExceptionDetail ColumnNotFound(string listName, string columnName)
        {
            return new ListExceptionDetail(listName, ()=>string.Format(Resources.ListExceptionDetail_ColumnNotFound_Column___0___does_not_exist_, columnName));
        }

        public static ListExceptionDetail DuplicateValue(string listName, string columnName, object value)
        {
            return new ListExceptionDetail(listName, ()=>string.Format(Resources.ListExceptionDetail_DuplicateValue_Duplicate_value___1___found_in_column___0___, columnName, value));
        }

        public static ListExceptionDetail NullValue(string listName, string column)
        {
            return new ListExceptionDetail(listName, ()=>String.Format(Resources.ListExceptionDetail_NullValue_Column___0___cannot_be_blank_, column));
        }

        public static ListExceptionDetail InvalidValue(string listName, string columnName, object value)
        {
            return new ListExceptionDetail(listName,
                () => String.Format(Resources.ListExceptionDetail_InvalidValue_Invalid_value___1___for_column___0___, columnName, value));
        }
    }
}
