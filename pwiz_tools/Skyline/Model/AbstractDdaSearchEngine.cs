/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Drawing;
using System.IO;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractDdaSearchEngine : IDisposable
    {
        public abstract string[] FragmentIons { get; }
        public abstract string EngineName { get; }
        public abstract Bitmap SearchEngineLogo { get; }
        public MsDataFileUri[] SpectrumFileNames { get; protected set; }
        protected string[] FastaFileNames { get; set; }

        /// <summary>
        /// A string or numeric variable with a default value and, for numeric types, min and max values.
        /// </summary>
        public class Setting : IAuditLogObject
        {
            public Setting(string name, int defaultValue, int minValue, int maxValue)
            {
                Name = name;
                _value = defaultValue;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public Setting(string name, double defaultValue, double minValue, double maxValue)
            {
                Name = name;
                _value = defaultValue;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public Setting(string name, bool defaultValue)
            {
                Name = name;
                _value = defaultValue;
                MinValue = false;
                MaxValue = true;
            }

            public Setting(string name, string defaultValue = null)
            {
                Name = name;
                MinValue = string.Empty;
                _value = defaultValue ?? string.Empty;
            }

            public string Name { get; }
            public object MinValue { get; }
            public object MaxValue { get; }

            private object _value;
            public object Value
            {
                get { return _value; }
                set { _value = Validate(value); }
            }

            public object Validate(object value)
            {
                // incoming value must either be a string or value type must stay the same
                Assume.IsTrue(value is string || value?.GetType() == _value?.GetType());

                if (value == null)
                    return null;

                // CONSIDER: worth an extra case to handle incoming values that are already int/double?
                switch (MinValue)
                {
                    case string s:
                        return value;

                    case bool b:
                        if (!bool.TryParse(value.ToString(), out bool tmpb))
                            throw new ArgumentException(string.Format(
                                Resources.Setting_Validate_The_value___0___is_not_valid_for_the_argument__1__which_must_be_either__True__or__False__,
                                value, Name));
                        return tmpb;

                    case int minValue:
                        if (!int.TryParse(value.ToString(), out int tmpi32))
                            throw new ArgumentException(string.Format(
                                Resources.ValueInvalidIntException_ValueInvalidIntException_The_value___0___is_not_valid_for_the_argument__1__which_requires_an_integer_,
                                value, Name));
                        if (tmpi32 < minValue || tmpi32 > (int) MaxValue)
                            throw new ArgumentOutOfRangeException(string.Format(
                                Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__,
                                value, Name, minValue, MaxValue));
                        return tmpi32;

                    case double minValue:
                        if (!double.TryParse(value.ToString(), out double tmpd))
                            throw new ArgumentException(string.Format(
                                Resources.ValueInvalidDoubleException_ValueInvalidDoubleException_The_value___0___is_not_valid_for_the_argument__1__which_requires_a_decimal_number_,
                                value, Name));
                        if (tmpd < minValue || tmpd > (double) MaxValue)
                            throw new ArgumentOutOfRangeException(string.Format(
                                Resources.ValueOutOfRangeDoubleException_ValueOutOfRangeException_The_value___0___for_the_argument__1__must_be_between__2__and__3__,
                                value, Name, minValue, MaxValue));
                        return tmpd;

                    default:
                        throw new InvalidOperationException(@"unsupported setting type");
                }
            }

            public override string ToString()
            {
                if (Value is double d)
                    return $@"{Name} = {d.ToString(@"F")}";
                return $@"{Name} = {Value}";
            }

            public string AuditLogText => ToString();
            public bool IsName => false;
        }

        /// <summary>
        /// Dictionary of available additional settings. May be null if SearchEngine implementation has no additional settings.
        /// </summary>
        public IDictionary<string, Setting> AdditionalSettings { get; set; }

        public abstract void SetPrecursorMassTolerance(MzTolerance mzTolerance);
        public abstract void SetFragmentIonMassTolerance(MzTolerance mzTolerance);
        public abstract void SetFragmentIons(string ions);
        public abstract void SetEnzyme(Enzyme enzyme, int maxMissedCleavages);

        public delegate void NotificationEventHandler(object sender, IProgressStatus status);
        public abstract event NotificationEventHandler SearchProgressChanged;

        public abstract bool Run(CancellationTokenSource cancelToken, IProgressStatus status);

        public void SetSpectrumFiles(MsDataFileUri[] searchFilenames)
        {
            SpectrumFileNames = searchFilenames;
        }

        /// <summary>
        /// Returns the search result (e.g. pepXML, mzIdentML) filenpath corresponding with the given searchFilepath.
        /// </summary>
        /// <param name="searchFilepath">The raw data filepath (e.g. RAW, WIFF, mzML)</param>
        public virtual string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return Path.ChangeExtension(searchFilepath.GetFilePath(), @".mzid");
        }

        public void SetFastaFiles(string fastFile)
        {
            //todo check multi-fasta support
            FastaFileNames = new[] {fastFile};
        }

        public abstract void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods);

        public abstract void Dispose();
    }
}
