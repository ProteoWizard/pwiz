/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GHistogram.h"
#include "GError.h"
#include <math.h>
#include <fstream>
#include "GVec.h"

using namespace GClasses;

GHistogram::GHistogram(double xmin, double xmax, size_t binCount)
{
	m_min = xmin;
	m_max = xmax;
	m_binCount = binCount;
	m_bins = new double[binCount];
	GVec::setAll(m_bins, 0.0, binCount);
	m_sum = 0.0;
}

GHistogram::GHistogram(GMatrix& data, size_t col, double xmin, double xmax, size_t maxBuckets)
{
	double dataMin, dataRange;
	data.minAndRangeUnbiased(col, &dataMin, &dataRange);
	double mean = data.mean(col);
	double median = data.median(col);
	double dev = sqrt(data.variance(col, mean));
	if(xmin == UNKNOWN_REAL_VALUE)
		m_min = std::max(dataMin, median - 4 * dev);
	else
		m_min = xmin;
	if(xmax == UNKNOWN_REAL_VALUE)
		m_max = std::min(dataMin + dataRange, median + 4 * dev);
	else
		m_max = xmax;
	m_binCount = std::min(maxBuckets, (size_t)floor(sqrt((double)data.rows())));
	m_bins = new double[m_binCount];
	GVec::setAll(m_bins, 0.0, m_binCount);
	m_sum = 0.0;

	for(size_t i = 0; i < data.rows(); i++)
		addSample(data[i][col], 1.0);
}

GHistogram::~GHistogram()
{
	delete[] m_bins;
}

void GHistogram::addSample(double x, double weight)
{
	size_t bin = (size_t)floor((x - m_min) * m_binCount / (m_max - m_min));
	if(bin < m_binCount)
		m_bins[bin] += weight;
	m_sum += weight;
}

size_t GHistogram::binCount()
{
	return m_binCount;
}

double GHistogram::binToX(size_t n)
{
	return ((double)n + 0.5) * (m_max - m_min) / m_binCount + m_min;
}

size_t GHistogram::xToBin(double x)
{
	size_t bin = (size_t)floor((x - m_min) * m_binCount / (m_max - m_min));
	if(bin < m_binCount)
		return bin;
	else
		return INVALID_INDEX;
}

double GHistogram::binLikelihood(size_t n)
{
	return m_bins[n] * m_binCount / ((m_max - m_min) * m_sum);
}

double GHistogram::binProbability(size_t n)
{
	return m_bins[n] / m_sum;
}

size_t GHistogram::modeBin()
{
	size_t mode = 0;
	double modeSum = m_bins[0];
	for(size_t i = 1; i < m_binCount; i++)
	{
		if(m_bins[i] > modeSum)
		{
			modeSum = m_bins[i];
			mode = i;
		}
	}
	return mode;
}

void GHistogram::toFile(const char* filename)
{
	std::ofstream os;
	os.exceptions(std::ios::failbit|std::ios::badbit);
	try
	{
		os.open(filename, std::ios::binary);
	}
	catch(const std::exception&)
	{
		ThrowError("Error creating file: ", filename);
	}
	os.precision(5);
	for(size_t i = 0; i < m_binCount; i++)
		os << binToX(i) << " " << binLikelihood(i) << "\n";
}

double GHistogram::computeRange()
{
	double min = binLikelihood(0);
	double max = min;
	double d;
	for(size_t i = 1; i < m_binCount; i++)
	{
		d = binLikelihood(i);
		if(d < min)
			min = d;
		if(d > max)
			max = d;
	}
	return max - min;
}
