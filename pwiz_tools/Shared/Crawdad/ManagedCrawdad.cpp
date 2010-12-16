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
#include "ManagedCrawdad.h"

namespace pwiz {
namespace Crawdad {

    void CrawdadPeakFinder::SetChromatogram(IList<double>^ times, IList<double>^ intensities)
    {
        // TODO: Check times to make sure they are evenly spaced

        // Marshall intensities to vector for Crawdad
        int len = intensities->Count;
        vector<float> intensitiesCrawdad(len);
		double baselineIntensity = Double::MaxValue;
        double maxIntensity = 0;
        int maxIntensityIndex = -1;
        for (int i = 0; i < len; i++)
        {
            float intensity = (float)intensities[i];
            intensitiesCrawdad[i] = intensity;

            // Keep track of where the maximum intensity is
            if (intensity > maxIntensity)
            {
                maxIntensity = intensity;
                maxIntensityIndex = i;
            }
			if (intensity < baselineIntensity)
				baselineIntensity = intensity;
        }
		if (baselineIntensity == Double::MaxValue)
			baselineIntensity = 0;

        SetChromatogram(intensitiesCrawdad, maxIntensityIndex, baselineIntensity);
    }

    void CrawdadPeakFinder::SetChromatogram(IList<float>^ times, IList<float>^ intensities)
    {
        // TODO: Check times to make sure they are evenly spaced

        // Marshall intensities to vector for Crawdad
        int len = intensities->Count;
        vector<float> intensitiesCrawdad(len);
		double baselineIntensity = Double::MaxValue;
        double maxIntensity = 0;
        int maxIntensityIndex = -1;
        for (int i = 0; i < len; i++)
        {
            float intensity = intensities[i];
            intensitiesCrawdad[i] = intensity;

            // Keep track of where the maximum intensity is
            if (intensity > maxIntensity)
            {
                maxIntensity = intensity;
                maxIntensityIndex = i;
            }
			if (intensity < baselineIntensity)
				baselineIntensity = intensity;
        }
		if (baselineIntensity == Double::MaxValue)
			baselineIntensity = 0;

        SetChromatogram(intensitiesCrawdad, maxIntensityIndex, baselineIntensity);
    }

    void CrawdadPeakFinder::SetChromatogram(vector<float>& intensities, int maxIntensityIndex, double baselineIntensity)
    {
        // Find the peak width of the maximum intensity point at
        // half its height.
        int fwhm = 6;
        if (maxIntensityIndex != -1)
        {
            double halfHeight = (intensities[maxIntensityIndex] - baselineIntensity)/2 + baselineIntensity;
            int iStart = 0;
            for (int i = maxIntensityIndex - 1; i >= 0; i--)
            {
                if (intensities[i] < halfHeight)
                {
                    iStart = i;
                    break;
                }
            }
            int len = intensities.size();
            int iEnd = len - 1;
            for (int i = maxIntensityIndex + 1; i < len; i++)
            {
                if (intensities[i] < halfHeight)
                {
                    iEnd = i;
                    break;
                }
            }
            fwhm = max(fwhm, iEnd - iStart);
        }
        FullWidthHalfMax = (float) fwhm;

		_widthDataWings = (int)(FullWidthHalfMax*2);

		if (_widthDataWings > 0)
		{
			intensities.insert(intensities.begin(), _widthDataWings, (float)baselineIntensity);
			intensities.insert(intensities.end(), _widthDataWings, (float)baselineIntensity);
		}

		_pPeakFinder->clear();
        _pPeakFinder->set_chrom(intensities, 0);
    }

    List<float>^ CrawdadPeakFinder::Intensities2d::get()
    {
        // Make sure the 2d chromatogram is populated
        if (_pPeakFinder->chrom_2d.size() != _pPeakFinder->chrom.size())
        {
            _pPeakFinder->chrom_2d.resize(_pPeakFinder->chrom.size());
            _pPeakFinder->get_2d_chrom(_pPeakFinder->chrom, _pPeakFinder->chrom_2d);
        }

        // Marshall 2nd derivative peaks back to managed list
	    List<float>^ intensities2d = gcnew List<float>(_pPeakFinder->chrom.size() - _widthDataWings*2);

        vector<float>::iterator it = _pPeakFinder->chrom_2d.begin() + _widthDataWings;
        vector<float>::iterator itEnd = _pPeakFinder->chrom_2d.end() - _widthDataWings;
        while (it < itEnd)
        {
            intensities2d->Add(*it);
            it++;
        }
        return intensities2d;
    }

    List<float>^ CrawdadPeakFinder::Intensities1d::get()
    {
        // Make sure the 2d chromatogram is populated
        if (_pPeakFinder->chrom_1d.size() != _pPeakFinder->chrom.size())
        {
            _pPeakFinder->chrom_1d.resize(_pPeakFinder->chrom.size());
            _pPeakFinder->get_1d_chrom(_pPeakFinder->chrom, _pPeakFinder->chrom_1d);
        }

        // Marshall 2nd derivative peaks back to managed list
	    List<float>^ intensities1d = gcnew List<float>(_pPeakFinder->chrom.size() - _widthDataWings*2);

        vector<float>::iterator it = _pPeakFinder->chrom_1d.begin() + _widthDataWings;
        vector<float>::iterator itEnd = _pPeakFinder->chrom_1d.end() - _widthDataWings;
        while (it < itEnd)
        {
            intensities1d->Add(*it);
            it++;
        }
        return intensities1d;
    }

    CrawdadPeak^ CrawdadPeakFinder::GetPeak(int startIndex, int endIndex)
    {
		startIndex += _widthDataWings;
		endIndex += _widthDataWings;
        
		SlimCrawPeak peak;
        _pPeakFinder->annotator.reannotate_peak(peak, startIndex, endIndex);
		_pPeakFinder->annotator.calc_fwhm(peak);

		peak.start_rt_idx -= _widthDataWings;
		peak.stop_rt_idx -= _widthDataWings;
		peak.peak_rt_idx -= _widthDataWings;

        return gcnew CrawdadPeak(peak);
    }

    List<CrawdadPeak^>^ CrawdadPeakFinder::CalcPeaks()
    {
        return CalcPeaks(-1);
    }

    List<CrawdadPeak^>^ CrawdadPeakFinder::CalcPeaks(int max)
    {
        // Find peaks
        _pPeakFinder->call_peaks();

        // Marshall found peaks to managed list
	    List<CrawdadPeak^>^ result = gcnew List<CrawdadPeak^>(_pPeakFinder->sps.size());
        vector<SlimCrawPeak>::iterator itPeak = _pPeakFinder->sps.begin();
        vector<SlimCrawPeak>::iterator itPeakEnd = _pPeakFinder->sps.end();
        double totalArea = 0;
		int stop_rt = _pPeakFinder->chrom.size() - _widthDataWings - 1;
		int adjust_stop_rt = stop_rt - _widthDataWings;
        while (itPeak != itPeakEnd)
        {
			if (itPeak->start_rt_idx < stop_rt && itPeak->stop_rt_idx > _widthDataWings)
			{
				double rheight = itPeak->peak_height / itPeak->raw_height;
				double rarea = itPeak->peak_area / itPeak->raw_area;

				if (rheight > 0.02 && rarea > 0.02)
				{
					itPeak->start_rt_idx = Math::Max(_widthDataWings, itPeak->start_rt_idx);
					itPeak->start_rt_idx -= _widthDataWings;
					itPeak->peak_rt_idx = Math::Max(_widthDataWings, Math::Min(stop_rt, itPeak->peak_rt_idx));
					itPeak->peak_rt_idx -= _widthDataWings;
					itPeak->stop_rt_idx = Math::Max(_widthDataWings, Math::Min(stop_rt, itPeak->stop_rt_idx));
					itPeak->stop_rt_idx -= _widthDataWings;

					result->Add(gcnew CrawdadPeak(*itPeak));

					totalArea += itPeak->peak_area;
				}
			}
            itPeak++;
        }

        // If max is not -1, then return the max most intense peaks
        if (max != -1)
        {
            // Shorten the list before performing the slow sort by intensity.
            // The sort shows up a s bottleneck in a profiler.
            int lenResult = result->Count;
            float intensityCutoff = 0;
            FindIntensityCutoff(result, 0, (float)(totalArea/lenResult)*2, max, 1, intensityCutoff, lenResult);

    	    List<CrawdadPeak^>^ resultFiltered = gcnew List<CrawdadPeak^>(lenResult);
            for (int i = 0, lenOrig = result->Count; i < lenOrig ; i++)
            {
                if (result[i]->Area >= intensityCutoff || intensityCutoff == 0)
                    resultFiltered->Add(result[i]);
            }

            resultFiltered->Sort(gcnew Comparison<CrawdadPeak^>(OrderAreaDesc));
            if (max < resultFiltered->Count)
                resultFiltered->RemoveRange(max, resultFiltered->Count - max);
            result = resultFiltered;
        }

        return result;
    }

    void CrawdadPeakFinder::FindIntensityCutoff(List<CrawdadPeak^>^ listPeaks, float left, float right,
        int minPeaks, int calls, float& cutoff, int& len)
    {
        if (calls < 3)
        {
            float mid = (left + right)/2;
            int count = FilterPeaks(listPeaks, mid);
            if (count < minPeaks)
                FindIntensityCutoff(listPeaks, left, mid, minPeaks, calls + 1, cutoff, len);
            else
            {
                cutoff = mid;
                len = count;
                if (count > minPeaks*1.5)
                    FindIntensityCutoff(listPeaks, mid, right, minPeaks, calls + 1, cutoff, len);
            }
        }
    }

    int CrawdadPeakFinder::FilterPeaks(List<CrawdadPeak^>^ listPeaks, float intensityCutoff)
    {
        int nonNoise = 0;
        for each (CrawdadPeak^ peak in listPeaks)
        {
            if (peak->Area >= intensityCutoff)
                nonNoise++;
        }
        return nonNoise;
    }

    int CrawdadPeakFinder::OrderAreaDesc(CrawdadPeak^ peak1, CrawdadPeak^ peak2)
    {
        float a1 = peak1->Area, a2 = peak2->Area;
        return (a1 > a2 ? -1 : (a1 < a2 ? 1 : 0));
    }
}
}