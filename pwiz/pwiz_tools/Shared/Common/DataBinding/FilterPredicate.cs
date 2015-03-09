using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public static class Filters
    {
        public static bool True(object row)
        {
            return true;
        }
        public static bool False(object row)
        {
            return false;
        }
        public static Func<object,bool> And(IEnumerable<Func<object, bool>> funcs)
        {
            return row => funcs.All(func => func(row));
        }
        public static Func<object,bool> Or(IEnumerable<Func<object,bool>> funcs)
        {
            return row => funcs.Any(func => func(row));
        }
        public static Func<object,bool> Contains(PropertyDescriptor property, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return True;
            }
            return row =>
                       {
                           var value = property.GetValue(row);
                           return value != null && value.ToString().IndexOf(text) >= 0;
                       };
        }
    }
}
