/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

#pragma once

#include "CrawPeak.h"
#include "CrawPeakFinder.h"
#include "CrawPeakMethod.h"
#include <boost/shared_ptr.hpp>

namespace pwiz {
namespace SimpleCrawdad {

using std::vector;
using std::max;
using namespace crawpeaks;

struct CrawdadPeak
{
    CrawdadPeak(const SlimCrawPeak& crawPeak)
    {
        _timeIndex = crawPeak.peak_rt_idx;
        _startIndex = crawPeak.start_rt_idx;
        _endIndex = crawPeak.stop_rt_idx;
        _height = crawPeak.peak_height;
        // BUG: Crawdad can return negative areas.  Using Math::Max() below
        //      protects against that but really should be fixed in Crawdad.
        _area = max(0.0f, crawPeak.peak_area);
        _backgroundArea = max(0.0f, crawPeak.bg_area);
        _fwhm = crawPeak.fwhm;
        _fwhmDegenerate = !crawPeak.fwhm_calculated_ok;
    }

    int getTimeIndex() { return _timeIndex; } 
    int getStartIndex() { return _startIndex; }
    void setStartIndex(int value) { _startIndex = value; }
    int getEndIndex() { return _endIndex; }
    void setEndIndex(int value) { _endIndex = value; }
    int getLength() { return _endIndex - _startIndex + 1; } 
    int getCenter() { return (int)(((_startIndex + _endIndex)/2.0) + 0.5); } 
    float getArea() { return _area; } 
    float getBackgroundArea() { return _backgroundArea; } 
    float getHeight() { return _height; } 
    float getFwhm() { return _fwhm; } 
    bool getFwhmDegenerate() { return _fwhmDegenerate; } 

    private:
    int _timeIndex;
    int _startIndex;
    int _endIndex;
    float _area;
    float _backgroundArea;
    float _height;
    float _fwhm;
    bool _fwhmDegenerate;
};

typedef boost::shared_ptr<CrawdadPeak> CrawdadPeakPtr;


class CrawdadPeakFinder
{
    public:
    CrawdadPeakFinder()
    {
        _peakFinder.slim = true;
        _peakFinder.method.peak_location_meth = MAXIMUM_PEAK;
        _peakFinder.method.background_estimation_method = LOWER_BOUNDARY;
    }

    void SetChromatogram(const vector<double>& times, const vector<double>& intensities);
    void SetChromatogram(const vector<float>& times, const vector<float>& intensities);

    float getFullWidthHalfMax() { return _peakFinder.method.get_fwhm(); }
    void setFullWidthHalfMax(float fwhm)
    {
        _peakFinder.method.set_fwhm(fwhm*3);
        _peakFinder.method.min_len = (int)(fwhm/4.0 + 0.5);
    }

    float getStdDev() { return _peakFinder.method.get_sd(); }
    void setStdDev(float sd) { _peakFinder.method.set_sd(sd); }

    const vector<float>& getSmoothed2ndDerivativeIntensities();
    const vector<float>& getSmoothed1stDerivativeIntensities();
    const vector<float>& getSmoothedIntensities();
    const vector<float>& getWingData() const {return _wingData;}
    float getBaselineIntensity() const {return _baselineIntensity;}

    vector<CrawdadPeakPtr> CalcPeaks();

    vector<CrawdadPeakPtr> CalcPeaks(int max);

    private:
    void SetChromatogram(vector<float>& intensities, int maxIntensityIndex);

    static void FindIntensityCutoff(const vector<CrawdadPeakPtr>& listPeaks, float left, float right,
                                    int minPeaks, int calls, float& cutoff, int& len);
    static int FilterPeaks(const vector<CrawdadPeakPtr>& listPeaks, float intensityCutoff);

	// Padding data added before and after real data to ensure peaks
	// near the edges get detected.
	int _widthDataWings;
    vector<float> _wingData;
    float _baselineIntensity;

    StackCrawPeakFinder _peakFinder;
};

}
}
