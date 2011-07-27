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
#include "SimpleCrawdad.h"
#include <boost/foreach.hpp>

namespace pwiz {
namespace SimpleCrawdad {

using namespace std;
using namespace crawpeaks;

void CrawdadPeakFinder::SetChromatogram(const vector<double>& times, const vector<double>& intensities)
{
    // TODO: Check times to make sure they are evenly spaced

    // Marshall intensities to vector for Crawdad
    int len = intensities.size();
    vector<float> intensitiesCrawdad(len);
	double baselineIntensity = intensities[0];
    double maxIntensity = 0;
    int maxIntensityIndex = -1;
    for (int i = 1; i < len; i++)
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

    SetChromatogram(intensitiesCrawdad, maxIntensityIndex, baselineIntensity);
}

void CrawdadPeakFinder::SetChromatogram(const vector<float>& times, const vector<float>& intensities)
{
    // TODO: Check times to make sure they are evenly spaced

    // Marshall intensities to vector for Crawdad
    int len = intensities.size();
    vector<float> intensitiesCrawdad(len);
	double baselineIntensity = intensities[0];
    double maxIntensity = 0;
    int maxIntensityIndex = -1;
    for (int i = 1; i < len; i++)
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
    setFullWidthHalfMax((float) fwhm);

	_widthDataWings = (int)(getFullWidthHalfMax()*2);

	if (_widthDataWings > 0)
	{
		intensities.insert(intensities.begin(), _widthDataWings, (float)baselineIntensity);
		intensities.insert(intensities.end(), _widthDataWings, (float)baselineIntensity);
	}

	_peakFinder.clear();
    _peakFinder.set_chrom(intensities, 0);
}

vector<float> CrawdadPeakFinder::getIntensities2d()
{
    // Make sure the 2d chromatogram is populated
    if (_peakFinder.chrom_2d.size() != _peakFinder.chrom.size())
    {
        _peakFinder.chrom_2d.resize(_peakFinder.chrom.size());
        _peakFinder.get_2d_chrom(_peakFinder.chrom, _peakFinder.chrom_2d);
    }

    // Marshall 2nd derivative peaks back to managed list
    vector<float> intensities2d(_peakFinder.chrom.size() - _widthDataWings*2);

    vector<float>::iterator it = _peakFinder.chrom_2d.begin() + _widthDataWings;
    vector<float>::iterator itEnd = _peakFinder.chrom_2d.end() - _widthDataWings;
    while (it < itEnd)
    {
        intensities2d.push_back(*it);
        it++;
    }
    return intensities2d;
}

vector<float> CrawdadPeakFinder::getIntensities1d()
{
    // Make sure the 2d chromatogram is populated
    if (_peakFinder.chrom_1d.size() != _peakFinder.chrom.size())
    {
        _peakFinder.chrom_1d.resize(_peakFinder.chrom.size());
        _peakFinder.get_1d_chrom(_peakFinder.chrom, _peakFinder.chrom_1d);
    }

    // Marshall 2nd derivative peaks back to managed list
    vector<float> intensities1d(_peakFinder.chrom.size() - _widthDataWings*2);

    vector<float>::iterator it = _peakFinder.chrom_1d.begin() + _widthDataWings;
    vector<float>::iterator itEnd = _peakFinder.chrom_1d.end() - _widthDataWings;
    while (it < itEnd)
    {
        intensities1d.push_back(*it);
        it++;
    }
    return intensities1d;
}

struct OrderAreaDesc
{
    bool operator() (const CrawdadPeakPtr& peak1, const CrawdadPeakPtr& peak2) const
    {
        return peak1->getArea() < peak2->getArea();
    }
};

vector<CrawdadPeakPtr> CrawdadPeakFinder::CalcPeaks()
{
    return CalcPeaks(-1);
}

vector<CrawdadPeakPtr> CrawdadPeakFinder::CalcPeaks(int maxPeaks)
{
    // Find peaks
    _peakFinder.call_peaks();

    // Marshall found peaks to managed list
    vector<CrawdadPeakPtr> result;
    vector<SlimCrawPeak>::iterator itPeak = _peakFinder.sps.begin();
    vector<SlimCrawPeak>::iterator itPeakEnd = _peakFinder.sps.end();
    double totalArea = 0;
	int stop_rt = _peakFinder.chrom.size() - _widthDataWings - 1;
	int adjust_stop_rt = stop_rt - _widthDataWings;
    while (itPeak != itPeakEnd)
    {
		if (itPeak->start_rt_idx < stop_rt && itPeak->stop_rt_idx > _widthDataWings)
		{
			double rheight = itPeak->peak_height / itPeak->raw_height;
			double rarea = itPeak->peak_area / itPeak->raw_area;

			if (rheight > 0.02 && rarea > 0.02)
			{
				itPeak->start_rt_idx = max(_widthDataWings, itPeak->start_rt_idx);
				itPeak->start_rt_idx -= _widthDataWings;
				itPeak->peak_rt_idx = max(_widthDataWings, min(stop_rt, itPeak->peak_rt_idx));
				itPeak->peak_rt_idx -= _widthDataWings;
				itPeak->stop_rt_idx = max(_widthDataWings, min(stop_rt, itPeak->stop_rt_idx));
				itPeak->stop_rt_idx -= _widthDataWings;

				result.push_back(CrawdadPeakPtr(new CrawdadPeak(*itPeak)));

				totalArea += itPeak->peak_area;
			}
		}
        itPeak++;
    }

    // If max is not -1, then return the max most intense peaks
    if (maxPeaks != -1)
    {
        // Shorten the list before performing the slow sort by intensity.
        // The sort shows up a s bottleneck in a profiler.
        int lenResult = result.size();
        float intensityCutoff = 0;
        FindIntensityCutoff(result, 0, (float)(totalArea/lenResult)*2, maxPeaks, 1, intensityCutoff, lenResult);

	    vector<CrawdadPeakPtr> resultFiltered;
        for (int i = 0, lenOrig = result.size(); i < lenOrig ; i++)
        {
            if (result[i]->getArea() >= intensityCutoff || intensityCutoff == 0)
                resultFiltered.push_back(result[i]);
        }

        sort(resultFiltered.begin(), resultFiltered.end(), OrderAreaDesc());
        if ((int)maxPeaks < (int)resultFiltered.size())
            resultFiltered.erase(resultFiltered.begin()+maxPeaks, resultFiltered.end());
        return resultFiltered;
    }
    else
        return result;
}

void CrawdadPeakFinder::FindIntensityCutoff(const vector<CrawdadPeakPtr>& listPeaks, float left, float right,
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

int CrawdadPeakFinder::FilterPeaks(const vector<CrawdadPeakPtr>& listPeaks, float intensityCutoff)
{
    int nonNoise = 0;
    BOOST_FOREACH(const CrawdadPeakPtr& peak, listPeaks)
    {
        if (peak->getArea() >= intensityCutoff)
            nonNoise++;
    }
    return nonNoise;
}

}
}