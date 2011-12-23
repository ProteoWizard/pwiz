/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public class MsDataFileData : EntityModel<DbMsDataFile>
    {
        private double[] _times;
        private double[] _totalIonCurrent;
        private byte[] _msLevels;
        private String _path;
        public MsDataFileData(MsDataFile msDataFile, DbMsDataFile dbMsDataFile)
            : base(msDataFile.Workspace, dbMsDataFile)
        {
            MsDataFile = msDataFile;
        }
        protected override void Load(DbMsDataFile entity)
        {
            base.Load(entity);
            _times = entity.Times;
            _msLevels = entity.MsLevels;
            _totalIonCurrent = entity.TotalIonCurrent;
        }
        public MsDataFile MsDataFile { get; private set; }
        public void Init(MsDataFileImpl msDataFileImpl)
        {
            if (_times == null)
            {
                _times = msDataFileImpl.GetScanTimes();
                _totalIonCurrent = msDataFileImpl.GetTotalIonCurrent();
                if (_times == null)
                {
                    msDataFileImpl.GetScanTimesAndMsLevels(out _times, out _msLevels);
                }
                else
                {
                    _msLevels = new byte[_times.Length];
                }
            }
            OnChange();
        }
        protected override DbMsDataFile UpdateDbEntity(NHibernate.ISession session)
        {
            var msDataFile = base.UpdateDbEntity(session);
            if (_times != null)
            {
                msDataFile.Times = _times;
                if (msDataFile.MsLevels != null && msDataFile.MsLevels.Length == _msLevels.Length)
                {
                    for (int i = 0; i < _msLevels.Length; i++)
                    {
                        if (_msLevels[i] == 0)
                        {
                            _msLevels[i] = msDataFile.MsLevels[i];
                        }
                    }
                }
                msDataFile.MsLevels = _msLevels;
                if (_totalIonCurrent != null)
                {
                    msDataFile.TotalIonCurrent = _totalIonCurrent;
                }
            }
            session.Save(new DbChangeLog(MsDataFile));
            return msDataFile;
        }
        public int GetMsLevel(int scanIndex, MsDataFileImpl msDataFileImpl)
        {
            if (_msLevels[scanIndex] == 0)
            {
                _msLevels[scanIndex] = (byte)msDataFileImpl.GetMsLevel(scanIndex);
            }
            return _msLevels[scanIndex];
        }
        public int GetSpectrumCount()
        {
            if (_times == null)
            {
                return 0;
            }
            return _times.Length;
        }
        public double GetTime(int scanIndex)
        {
            scanIndex = Math.Min(scanIndex, GetSpectrumCount() - 1);
            if (scanIndex < 0)
            {
                return 0;
            }
            return _times[scanIndex];
        }
        public int FindScanIndex(double time)
        {
            int index = Array.BinarySearch(_times, time);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, GetSpectrumCount() - 1);
            return index;
        }
        public bool HasTimes()
        {
            return _times != null;
        }
        public double? GetTotalIonCurrent(double startTime, double endTime)
        {
            if (_totalIonCurrent == null || _msLevels == null)
            {
                return null;
            }
            double total = 0;
            int startIndex = FindScanIndex(startTime);
            int endIndex = FindScanIndex(endTime);
            for (int i = startIndex; i<=endIndex; i++)
            {
                if (_msLevels[i] != 1)
                {
                    continue;
                }
                int? prevIndex = GetPrevMs1Index(i);
                int? nextIndex = GetNextMs1Index(i);
                double width;
                if (prevIndex.HasValue && nextIndex.HasValue)
                {
                    width = (_times[nextIndex.Value] - _times[prevIndex.Value])/2;
                }
                else if (prevIndex.HasValue)
                {
                    width = _times[i] - _times[prevIndex.Value];
                }
                else if (nextIndex.HasValue)
                {
                    width = _times[nextIndex.Value] - _times[i];
                }
                else
                {
                    width = 0;
                }
                total += _totalIonCurrent[i]*width;
            }
            return total;
        }
        private int? GetPrevMs1Index(int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (_msLevels[i] == 1)
                {
                    return i;
                }
            }
            return null;
        }
        private int? GetNextMs1Index(int index)
        {
            for (int i = index + 1; i < _msLevels.Length; i++)
            {
                if (_msLevels[i] == 1)
                {
                    return i;
                }
            }
            return null;
        }
    }
}
