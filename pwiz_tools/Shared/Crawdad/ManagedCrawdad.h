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

#include <memory>
#include "AutoNative.h"
#include "CrawPeak.H"
#include "CrawPeakFinder.H"
#include "CrawPeakMethod.H"

using namespace System;
using namespace System::Collections::Generic;
using namespace std;
using namespace crawpeaks;

namespace pwiz {
namespace Crawdad {

    public ref class CrawdadPeak
    {
    public:
        CrawdadPeak(const SlimCrawPeak& crawPeak)
        {
            _timeIndex = crawPeak.peak_rt_idx;
            _startIndex = crawPeak.start_rt_idx;
            _endIndex = crawPeak.stop_rt_idx;
            _height = crawPeak.peak_height;
            // Note: Crawdad had a bug that caused it to return negative areas.
            //       Using Math::Max() below left in place to protect against that,
            //       even though the bug is believed to be fixed.
            _area = Math::Max(0.0f, crawPeak.peak_area);
            _backgroundArea = Math::Max(0.0f, crawPeak.bg_area);
            _fwhm = crawPeak.fwhm;
            _fwhmDegenerate = !crawPeak.fwhm_calculated_ok;
        }

        ~CrawdadPeak()
        {
        }

        property int TimeIndex { int get() { return _timeIndex; } }
        property int StartIndex { int get() { return _startIndex; } }
        property int EndIndex { int get() { return _endIndex; } }
        property int Length { int get() { return _endIndex - _startIndex + 1; } }
        property int Center { int get() { return (int)(((_startIndex + _endIndex)/2.0) + 0.5); } }
        property float Area { float get() { return _area; } }
        property float BackgroundArea { float get() { return _backgroundArea; } }
        property float Height { float get() { return _height; } }
        property float Fwhm { float get() { return _fwhm; } }
        property bool FwhmDegenerate { bool get() { return _fwhmDegenerate; } }
        property bool Identified { bool get() { return _identified; } }

        void ResetBoundaries(int startIndex, int endIndex)
        {
            if (startIndex > endIndex)
                throw gcnew ArgumentException(String::Format("Start peak boundary {0} must be less than or equal to end boundary {1}", _startIndex, _endIndex));
            _startIndex = startIndex;
            _endIndex = endIndex;
        }

        void SetIdentified(array<int>^ idIndices)
        {
            _identified = IsIdentified(idIndices);
        }

        virtual String^ ToString() override
        {
            return String::Format("a = {0}{1}, bg = {2}, s = {3}, e = {4}, r = {5}",
                _area, _identified ? "*" : "", _backgroundArea, _startIndex, _endIndex, _timeIndex);
        }

    private:
        bool IsIdentified(array<int>^ idIndices)
        {
            for each (int idIndex in idIndices)
            {
                if (_startIndex <= idIndex && idIndex <= _endIndex)
                    return true;
            }
            return false;
        }

        int _timeIndex;
        int _startIndex;
        int _endIndex;
        float _area;
        float _backgroundArea;
        float _height;
        float _fwhm;
        bool _fwhmDegenerate;
        bool _identified;
    };

    public ref class CrawdadPeakFinder
	{
    public:
        CrawdadPeakFinder()
        {
            _pPeakFinder = new StackCrawPeakFinder();
            _pPeakFinder->slim = true;
            _pPeakFinder->method.peak_location_meth = MAXIMUM_PEAK;
            _pPeakFinder->method.background_estimation_method = LOWER_BOUNDARY;
            // _pPeakFinder->method.area_calc_method = HEIGHT_AS_AREA; // For testing other options for quantitative metrics
        }

        ~CrawdadPeakFinder()
        {
        }

        void SetChromatogram(IList<double>^ times, IList<double>^ intensities);
        void SetChromatogram(IList<float>^ times, IList<float>^ intensities);

        property bool IsHeightAsArea
        {
            bool get() { return _pPeakFinder->method.area_calc_method == HEIGHT_AS_AREA; }
        }

        property float FullWidthHalfMax
        {
            float get() { return _pPeakFinder->method.get_fwhm(); }
            void set(float fwhm)
            {
                _pPeakFinder->method.set_fwhm(fwhm*3);
                _pPeakFinder->method.min_len = (int)(fwhm/4.0 + 0.5);
            }
        }

        property float StdDev
        {
            float get() { return _pPeakFinder->method.get_sd(); }
            void set(float sd) { _pPeakFinder->method.set_sd(sd); }
        }

        property List<float>^ Intensities2d { List<float>^ get(); }
        property List<float>^ Intensities1d { List<float>^ get(); }

        List<CrawdadPeak^>^ CalcPeaks() { return CalcPeaks(-1, gcnew array<int>(0)); }
        List<CrawdadPeak^>^ CalcPeaks(int max) { return CalcPeaks(max, gcnew array<int>(0)); }
        List<CrawdadPeak^>^ CalcPeaks(int max, array<int>^ idIndices);

        CrawdadPeak^ GetPeak(int startIndex, int endIndex);

    private:
        void SetChromatogram(vector<float>& intensities, int maxIntensityIndex, double baselintIntensity);

        static void FindIntensityCutoff(List<CrawdadPeak^>^ listPeaks, float left, float right,
            int minPeaks, int calls, float& cutoff, int& len);
        static int FilterPeaks(List<CrawdadPeak^>^ listPeaks, float intensityCutoff);
        static int OrderIdAreaDesc(CrawdadPeak^ peak1, CrawdadPeak^ peak2);
        
    private:
		// Padding data added before and after real data to ensure peaks
		// near the edges get detected.
		int _widthDataWings;

        CAutoNativePtr<StackCrawPeakFinder> _pPeakFinder;
	};

    // Yes, this is a strange place to have this class, but it is very useful
    // for debugging ProteoWizard .NET applications while working in Visual Studio.
    public ref class CrtDebugHeap
    {
    public:
        static void Checkpoint();
        static long DumpLeaks(bool dumpDetails);
    };
}
}