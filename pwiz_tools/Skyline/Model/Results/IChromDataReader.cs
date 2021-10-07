/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results
{
    public abstract class ChromDataReader
    {
        public abstract TimeIntensitiesGroup ReadTimeIntensities(ChromGroupHeaderInfo chromGroupHeaderInfo);
        public abstract IList<ChromPeak> ReadPeaks(ChromGroupHeaderInfo chromGroupHeaderInfo);
        public abstract IList<float> ReadScores(ChromGroupHeaderInfo chromGroupHeaderInfo);

        public virtual IList<Tuple<IList<ChromPeak>, IList<float>>> ReadAllPeaks(
            IList<ChromGroupHeaderInfo> chromGroupHeaderInfos, bool readScoresToo)
        {
            return chromGroupHeaderInfos.Select(chromGroupHeaderInfo =>
                Tuple.Create(ReadPeaks(chromGroupHeaderInfo), readScoresToo ? ReadScores(chromGroupHeaderInfo) : null)).ToList();
        }
    }

    public class StaticChromDataReader : ChromDataReader
    {
        private TimeIntensitiesGroup _timeIntensities;
        private IList<ChromPeak> _peaks;
        private IList<float> _scores;
        public StaticChromDataReader(TimeIntensitiesGroup timeIntensities, IList<ChromPeak> peaks,
            IList<float> scores)
        {
            _timeIntensities = timeIntensities;
            _peaks = peaks;
            _scores = scores;
        }

        public override TimeIntensitiesGroup ReadTimeIntensities(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _timeIntensities;
        }

        public override IList<ChromPeak> ReadPeaks(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _peaks;
        }

        public override IList<float> ReadScores(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return _scores;
        }
    }
}
