//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;

namespace IDPicker
{
    public static class SystemDataExtensionMethods
    {
        public static int ExecuteNonQuery (this IDbConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
                return cmd.ExecuteNonQuery(sql);
        }

        public static int ExecuteNonQuery (this IDbCommand cmd, string sql)
        {
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        public static IEnumerable<IDataRecord> ExecuteQuery (this IDbConnection conn, string sql)
        {
            using (var cmd = conn.CreateCommand())
                return cmd.ExecuteQuery(sql);
        }

        public static IEnumerable<IDataRecord> ExecuteQuery (this IDbCommand cmd, string sql)
        {
            int retryCount = 0;
            while (true)
                try
                {
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        return (reader as System.Data.Common.DbDataReader).OfType<IDataRecord>().ToArray();
                    }
                }
                catch (Exception e)
                {
                    if (retryCount == 3)
                        throw e;
                    System.Threading.Thread.Sleep(100);
                    ++retryCount;
                }
        }
    }

    public static class BitfieldExtensionMethods
    {
        public static bool HasFlag (this Enum target, Enum flags)
        {
            if (target.GetType() != flags.GetType())
                throw new InvalidOperationException("enum type mismatch");
            long a = Convert.ToInt64(target);
            long b = Convert.ToInt64(flags);
            return (a & b) == b;
        }
    }

    public static class ColorExtensionMethods
    {
        public static Color FromAhsb (int a, float h, float s, float b)
        {
            if (0 > a || 255 < a) throw new ArgumentOutOfRangeException("a", a, "Invalid value");
            if (0f > h || 360f < h) throw new ArgumentOutOfRangeException("h", h, "Invalid value");
            if (0f > s || 1f < s) throw new ArgumentOutOfRangeException("s", s, "Invalid value");
            if (0f > b || 1f < b) throw new ArgumentOutOfRangeException("b", b, "Invalid value");

            if (0 == s)
                return Color.FromArgb(a, Convert.ToInt32(b * 255), Convert.ToInt32(b * 255), Convert.ToInt32(b * 255));

            float fMax, fMid, fMin;
            int iSextant, iMax, iMid, iMin;

            if (0.5 < b)
            {
                fMax = b - (b * s) + s;
                fMin = b + (b * s) - s;
            }
            else
            {
                fMax = b + (b * s);
                fMin = b - (b * s);
            }

            iSextant = (int) Math.Floor(h / 60f);
            if (300f <= h)
                h -= 360f;
            h /= 60f;
            h -= 2f * (float) Math.Floor(((iSextant + 1f) % 6f) / 2f);
            if (0 == iSextant % 2)
                fMid = h * (fMax - fMin) + fMin;
            else
                fMid = fMin - h * (fMax - fMin);

            iMax = Convert.ToInt32(fMax * 255);
            iMid = Convert.ToInt32(fMid * 255);
            iMin = Convert.ToInt32(fMin * 255);

            switch (iSextant)
            {
                case 1: return Color.FromArgb(a, iMid, iMax, iMin);
                case 2: return Color.FromArgb(a, iMin, iMax, iMid);
                case 3: return Color.FromArgb(a, iMin, iMid, iMax);
                case 4: return Color.FromArgb(a, iMid, iMin, iMax);
                case 5: return Color.FromArgb(a, iMax, iMin, iMid);
                default: return Color.FromArgb(a, iMax, iMid, iMin);
            }
        }

        public static Color Interpolate (this Color start, Color end, float p)
        {
            float[] startHSB = new float[] { start.GetHue(), start.GetSaturation(), start.GetBrightness() };
            float[] endHSB = new float[] { end.GetHue(), end.GetSaturation(), end.GetBrightness() };

            float hue = start.GetHue() + p * (end.GetHue() - start.GetHue());
            float saturation = start.GetSaturation() + p * (end.GetSaturation() - start.GetSaturation());
            float brightness = start.GetBrightness() + p * (end.GetBrightness() - start.GetBrightness());

            return FromAhsb(255, hue, saturation, brightness);
        }
    }

    public static class SystemLinqExtensionMethods
    {
        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, SortOrder sortOrder)
        {
            switch (sortOrder)
            {
                case SortOrder.Ascending: return source.OrderBy(keySelector);
                case SortOrder.Descending: return source.OrderByDescending(keySelector);
                case SortOrder.None: default: throw new ArgumentException();
            }
        }

        public static int SequenceCompareTo<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second) where TSource : IComparable<TSource>
        {
            int result = 0;
            var itr1 = first.GetEnumerator();
            var itr2 = second.GetEnumerator();
            bool itr1Valid = true, itr2Valid = true;
            while (true)
            {
                itr1Valid = itr1.MoveNext();
                itr2Valid = itr2.MoveNext();

                if (!itr1Valid && !itr2Valid) break;
                if (itr1Valid && !itr2Valid) return 1;
                if (!itr1Valid && itr2Valid) return -1;

                result = itr1.Current.CompareTo(itr2.Current);
                if (result != 0)
                    break;
            }
            return result;
        }

        public static bool IsNullOrEmpty<TSource> (this IEnumerable<TSource> list)
        {
            if (list == null)
                return true;

            var genericCollection = list as ICollection<TSource>;
            if (genericCollection != null)
                return genericCollection.Count == 0;

            var nonGenericCollection = list as System.Collections.ICollection;
            if (nonGenericCollection != null)
                return nonGenericCollection.Count == 0;

            return !list.Any();
        }

        public static ReadOnlyCollection<TSource> AsReadOnly<TSource> (this IList<TSource> list)
        {
            return list == null ? null : new ReadOnlyCollection<TSource>(list);
        }
    }

    public static class RandomShuffleExtensionMethods
    {
        static Random defaultRng = new Random(0);

        // credit to http://stackoverflow.com/questions/1287567/c-is-using-random-and-orderby-a-good-shuffle-algorithm/1665080#1665080
        public static IEnumerable<T> Shuffle<T> (this IEnumerable<T> source, Random rng)
        {
            // shuffle the source in place if it implements IList<T> (e.g. List<T> and T[])
            IList<T> elements;
            if (source is IList<T>)
                elements = source as IList<T>;
            else
                elements = source.ToArray();

            // Note i > 0 to avoid final pointless iteration
            for (int i = elements.Count - 1; i > 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                int swapIndex = rng.Next(i + 1);
                yield return elements[swapIndex];
                elements[swapIndex] = elements[i];
                // we don't actually perform the swap, we can forget about the
                // swapped element because we already returned it.
            }

            // there is one item remaining that was not returned - we return it now
            yield return elements[0];
        }

        public static IEnumerable<T> Shuffle<T> (this IEnumerable<T> source) { return source.Shuffle(defaultRng); }
        public static IEnumerable<T> Shuffle<T> (this IEnumerable<T> source, int seed) { return source.Shuffle(new Random(seed)); }

        public static IEnumerable<T> ShuffleCopy<T> (this IEnumerable<T> source, Random rng)
        {
            var elements = new List<T>(source);
            return elements.Shuffle(rng);
        }

        public static IEnumerable<T> ShuffleCopy<T> (this IEnumerable<T> source) { return source.ShuffleCopy(defaultRng); }
        public static IEnumerable<T> ShuffleCopy<T> (this IEnumerable<T> source, int seed) { return source.ShuffleCopy(new Random(seed)); }
    }

    public static class SystemWindowsFormsExtensionMethods
    {
        public static IEnumerable<DataGridViewColumn> GetVisibleColumns(this DataGridView source) { return source.Columns.Cast<DataGridViewColumn>().Where(o => o.Visible); }
        public static IEnumerable<DataGridViewColumn> GetVisibleColumnsInDisplayOrder(this DataGridView source) { return source.GetVisibleColumns().OrderBy(o => o.DisplayIndex); }
    }

    public static class StopwatchExtensionMethods
    {
        public static TimeSpan Restart (this System.Diagnostics.Stopwatch stopwatch)
        {
            TimeSpan timeSpan = stopwatch.Elapsed;
            stopwatch.Reset();
            stopwatch.Start();
            return timeSpan;
        }
    }

    public static class ArrayExtensionMethods
    {
        public static void Fill<T> (this T[] array, T defaultValue)
        {
            if (array == null)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = defaultValue;
        }
    }
}
