using System;
using System.Collections;
using System.Collections.Generic;
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
