//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ExtensionMethods
{
    public static class Extensions
    {
        public static object Front(this IList list) { return list[0]; }
        public static object Back( this IList list ) { return list[list.Count - 1]; }
        public static object PopFront( this IList list ) { object o = list[0]; list.RemoveAt( 0 ); return o; }
        public static object PopBack( this IList list ) { object o = list[list.Count - 1]; list.RemoveAt( list.Count - 1 ); return o; }
        public static void PushFront( this IList list, object o ) { list.Insert( 0, o ); }
        public static void PushBack( this IList list, object o ) { list.Add( o ); }
    }
}
