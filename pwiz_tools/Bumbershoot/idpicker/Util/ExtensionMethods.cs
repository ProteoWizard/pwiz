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
using System.Linq.Expressions;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace IDPicker
{
    public class FilterString
    {
        public struct Keyword
        {
            public string String { get; set; }
            public bool IsRegex { get; set; }
            public bool IsQuoted { get; set; }
        }

        public Keyword[] Keywords { get; set; }

        public FilterString(string text)
        {
            var keywords = text.Split(';');
            Keywords = keywords.Select(o => new Keyword()
            {
                String = o.IsQuoted() ? o.Substring(1, o.Length - 2) : o,
                IsRegex = o.IsQuoted() ? false : o.IndexOfAny("\\.*?+[]".ToCharArray()) >= 0,
                IsQuoted = o.IsQuoted()
            }).ToArray();
        }
    }

    public static class SystemExtensionMethods
    {
        public static bool ContainsOrIsContainedBy(this string str, FilterString filterString)
        {
            if (str.Length == 0 || filterString.Keywords.Length == 0)
                return false;

            foreach (var otherString in filterString.Keywords)
            {
                if (otherString.IsQuoted)
                {
                    if (str == otherString.String)
                        return true;
                    else
                        continue;
                }

                if (otherString.IsRegex && Regex.IsMatch(str, otherString.String, RegexOptions.ExplicitCapture))
                    return true;

                int compare = str.Length.CompareTo(otherString.String.Length);
                if (compare == 0 && str == otherString.String ||
                    compare < 0 && otherString.String.Contains(str) ||
                    str.Contains(otherString.String))
                    return true;
            }
            return false;
        }

        public static bool IsQuoted(this string str)
        {
            return str.StartsWith("\"") && str.EndsWith("\"") ||
                   str.StartsWith("'") && str.EndsWith("'");
        }
    }

    public static class ControlExtensions
    {
        public static T CloneAsDesigned<T>(this T controlToClone) where T : Control
        {
            PropertyInfo[] controlProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Control instance = (Control)Activator.CreateInstance(controlToClone.GetType());

            foreach (PropertyInfo propInfo in controlProperties)
            {
                var browsable = propInfo.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>();
                if (browsable != null && !browsable.Browsable)
                    continue;

                var defaultValue = propInfo.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
                var category = propInfo.GetCustomAttribute<System.ComponentModel.CategoryAttribute>();
                var localizable = propInfo.GetCustomAttribute<System.ComponentModel.LocalizableAttribute>();
                if (defaultValue == null && category == null && localizable == null)
                    continue;

                if (!propInfo.CanWrite)
                    continue;

                propInfo.SetValue(instance, propInfo.GetValue(controlToClone, null), null);
            }

            /*foreach (Control ctl in controlToClone.Controls)
            {
                instance.Controls.Add(ctl.Clone());
            }*/
            return (T)instance;
        }
    }

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

        public static string GetDataSource (this IDbConnection conn)
        {
            const string dataSourceToken = "data source=";
            int dataSourceIndex = conn.ConnectionString.ToLower().IndexOf(dataSourceToken);
            if (dataSourceIndex < 0)
                throw new ArgumentException("no data source in connection string: " + conn.ConnectionString);
            dataSourceIndex += dataSourceToken.Length;
            int delimiterIndex = conn.ConnectionString.IndexOf(";", dataSourceIndex);
            if (delimiterIndex < 0)
                return conn.ConnectionString.Substring(dataSourceIndex);
            return conn.ConnectionString.Substring(dataSourceIndex, delimiterIndex - dataSourceIndex);
        }

        public static System.Data.SQLite.SQLiteConnection GetSQLiteConnection(this NHibernate.ISession session)
        {
            return session.Connection as System.Data.SQLite.SQLiteConnection;
        }

        public static DataTable ApplySort (this DataTable table, Comparison<DataRow> comparison)
        {
            DataTable clone = table.Clone();
            var rows = new List<DataRow>(table.Rows.Cast<DataRow>());

            rows.Sort(comparison);

            foreach (DataRow row in rows)
                clone.Rows.Add(row.ItemArray);

            return clone;
        }

        public static void SaveToDisk(this System.Data.SQLite.SQLiteConnection conn, string diskFilepath)
        {
            using (var diskConnection = new System.Data.SQLite.SQLiteConnection("data source=" + diskFilepath))
            {
                conn.BackupDatabase(diskConnection, diskFilepath, conn.GetDataSource(), -1, null, 100);
            }
        }

        public static NHibernate.IQuery SetQuerySource(this NHibernate.IQuery query, System.Reflection.MemberInfo memberInfo)
        {
            var time = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            query.SetComment(String.Format("Source:{0}.{1}; Start:{2}", memberInfo.DeclaringType.Name, memberInfo.Name, time.TotalSeconds));
            return query;
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

        /// <summary>
        /// Returns SortOrder.Descending for SortOrder.Ascending, SortOrder.Ascending for SortOrder.Descending, or defaultOrder for SortOrder.None.
        /// </summary>
        public static SortOrder ToggleOrDefault(this SortOrder order, SortOrder defaultOrder = SortOrder.None)
        {
            switch(order)
            {
                case SortOrder.Ascending: return SortOrder.Descending;
                case SortOrder.Descending: return SortOrder.Ascending;

                default: case SortOrder.None: return defaultOrder;
            }
        }

        public static System.Drawing.Point GetCenterPoint(this System.Drawing.Rectangle bounds)
        {
            return new System.Drawing.Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }

        public static System.Windows.Point GetCenterPoint(this System.Windows.Rect bounds)
        {
            return new System.Windows.Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }

        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            Type dgvType = dgv.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dgv, setting, null);
        }
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

    public static class ArrayCaster
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct Union
        {
            [FieldOffset(0)]
            public byte[] bytes;
            [FieldOffset(0)]
            public double[] floats;
        }
        private static readonly int byteId;
        private static readonly int floatId;

        static ArrayCaster()
        {
            byteId = getByteId();
            floatId = getFloatId();
        }

        public static unsafe void AsByteArray(double[] floats, Action<byte[]> action)
        {
            if (floats == null)
            {
                action(null);
                return;
            }
            if (floats.Length == 0)
            {
                action(new byte[0]);
                return;
            }

            var union = new Union { floats = floats };

            fixArray(union.bytes, union.floats.Length * sizeof(double), byteId);
            try
            {
                action(union.bytes);
            }
            finally
            {
                fixArray(union.bytes, union.bytes.Length / sizeof(double), floatId);
            }
        }

        public static unsafe void AsFloatArray(byte[] bytes, Action<double[]> action)
        {
            if (bytes == null)
            {
                action(null);
                return;
            }
            if (bytes.Length == 0)
            {
                action(new double[0]);
                return;
            }

            var union = new Union { bytes = bytes };

            fixArray(union.bytes, union.bytes.Length * sizeof(double), floatId);
            try
            {
                action(union.floats);
            }
            finally
            {
                fixArray(union.bytes, union.bytes.Length / sizeof(double), byteId);
            }
        }

        private static unsafe void fixArray(byte[] bytes, int newSize, int newId)
        {
            fixed (byte* pBytes = bytes)
            {
                var pSize = (int*)(pBytes - 4);
                var pId = (int*)(pBytes - 8);

                *pSize = newSize;
                *pId = newId;
            }
        }

        private static unsafe int getByteId()
        {
            fixed (byte* pBytes = new byte[1])
            {
                return *(int*)(pBytes - 8);
            }
        }

        private static unsafe int getFloatId()
        {
            fixed (double* pFloats = new double[1])
            {
                var pBytes = (byte*)pFloats;
                return *(int*)(pBytes - 8);
            }
        }
    }

}
