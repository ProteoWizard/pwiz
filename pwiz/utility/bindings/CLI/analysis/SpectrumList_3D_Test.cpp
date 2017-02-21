//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "../common/unit.hpp"


#pragma unmanaged
#include <stdexcept>
#include "boost/foreach_field.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_3D.hpp"
#pragma managed

ostream* os_;

using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::analysis;
using namespace System;
using namespace System::Collections::Generic;


namespace pwiz {
namespace CLI {
namespace analysis {

typedef System::Collections::Generic::KeyValuePair<double, double> MzIntensityPair;
typedef System::Collections::Generic::List<MzIntensityPair> Spectrum3DValue;
typedef System::Collections::Generic::List<System::Collections::Generic::KeyValuePair<double, Spectrum3DValue^>> Spectrum3D;

}
}
}


template<typename MapT>
typename MapT::const_iterator find_nearest(const MapT& m, typename const MapT::key_type& query, typename const MapT::key_type& tolerance)
{
    typename MapT::const_iterator cur, min, max, best;

    min = m.lower_bound(query - tolerance);
    max = m.lower_bound(query + tolerance);

    if (min == m.end() || fabs(query - min->first) > tolerance)
        return m.end();
    else if (min == max)
        return min;
    else
        best = min;

    double minDiff = fabs(query - best->first);
    for (cur = min; cur != max; ++cur)
    {
        double curDiff = fabs(query - cur->first);
        if (curDiff < minDiff)
        {
            minDiff = curDiff;
            best = cur;
        }
    }
    return best;
}


//                drift time
// m/z    100  200  300  400  500  600
// 123.4  0    1    2    1    0    0
// 234.5  1    2    3    2    1    0
// 345.6  2    3    4    3    2    1
// 456.7  1    2    3    2    1    0
// 567.8  0    1    2    1    0    0
// translates to:
// "100,123.4,0 200,123.4,1 300,123.4,2 400,123.4,1 500,123.4,0 600,123.4,0 100,234.5,1 200,123.4,2 300,123.4,3 400,123.4,2 500,123.4,1 600,123.4,0 etc."

void test(MSDataFile^ msd, double scanStartTime, const char* driftTimeRanges, const char* expectedSpectrumTable)
{
    List<ContinuousInterval>^ driftTimeRangesSet = gcnew List<ContinuousInterval>();
    vector<string> tokens, tokens2;
    bal::split(tokens, driftTimeRanges, bal::is_any_of(" "));
    BOOST_FOREACH(const string& token, tokens)
    {
        bal::split(tokens2, token, bal::is_any_of("-"));
        driftTimeRangesSet->Add(ContinuousInterval(lexical_cast<double>(tokens2[0]), lexical_cast<double>(tokens2[1])));
    }

    pwiz::analysis::Spectrum3D expectedSpectrum3d;
    bal::split(tokens, expectedSpectrumTable, bal::is_any_of(" "));
    BOOST_FOREACH(const string& token, tokens)
    {
        bal::split(tokens2, token, bal::is_any_of(","));
        expectedSpectrum3d[lexical_cast<double>(tokens2[0])][lexical_cast<double>(tokens2[1])] = lexical_cast<double>(tokens2[2]);
    }

    SpectrumList_3D^ sl = gcnew SpectrumList_3D(msd->run->spectrumList);
    pwiz::CLI::analysis::Spectrum3D^ resultSpectrum3d_CLI = sl->spectrum3d(scanStartTime, driftTimeRangesSet);
    System::GC::KeepAlive(msd);
    System::GC::KeepAlive(sl);
    System::GC::KeepAlive(driftTimeRangesSet);
    pwiz::analysis::Spectrum3D resultSpectrum3d;
    for each (KeyValuePair<double, Spectrum3DValue^> itr1 in resultSpectrum3d_CLI)
    {
        for each (MzIntensityPair itr2 in itr1.Value)
        {
            resultSpectrum3d[itr1.Key][itr2.Key] = itr2.Value;
        }
    }

    unit_assert(!resultSpectrum3d.empty());
    unit_assert(!expectedSpectrum3d.empty());

    pwiz::analysis::Spectrum3D inResultButNotExpected, inExpectedButNotResult;
    BOOST_FOREACH_FIELD((double actualDriftTime)(const pwiz::analysis::Spectrum3D::value_type::second_type& resultSpectrum), resultSpectrum3d)
    BOOST_FOREACH_FIELD((double expectedDriftTime)(const pwiz::analysis::Spectrum3D::value_type::second_type& expectedSpectrum), expectedSpectrum3d)
    {
        if (find_nearest(resultSpectrum3d, expectedDriftTime, 1e-5) == resultSpectrum3d.end())
            inExpectedButNotResult[expectedDriftTime] = pwiz::analysis::Spectrum3D::value_type::second_type();
        if (find_nearest(expectedSpectrum3d, actualDriftTime, 1e-5) == expectedSpectrum3d.end())
            inResultButNotExpected[actualDriftTime] = pwiz::analysis::Spectrum3D::value_type::second_type();

        if (fabs(actualDriftTime - expectedDriftTime) < 1e-5)
            BOOST_FOREACH_FIELD((double actualMz)(double actualIntensity), resultSpectrum)
            BOOST_FOREACH_FIELD((double expectedMz)(double expectedIntensity), expectedSpectrum)
            {
                if (find_nearest(resultSpectrum, expectedMz, 1e-4) == resultSpectrum.end())
                    inExpectedButNotResult[expectedDriftTime][expectedMz] = expectedIntensity;
                if (find_nearest(expectedSpectrum, actualMz, 1e-4) == expectedSpectrum.end())
                    inResultButNotExpected[actualDriftTime][actualMz] = actualIntensity;
            }
    }

    if (os_ && !inResultButNotExpected.empty())
    {
        *os_ << "Extra points in the result were not expected:\n";
        BOOST_FOREACH_FIELD((double driftTime)(const pwiz::analysis::Spectrum3D::value_type::second_type& spectrum), inResultButNotExpected)
        {
            *os_ << driftTime << ":";
            BOOST_FOREACH_FIELD((double mz)(double intensity), spectrum)
                *os_ << " " << mz << "," << intensity;
            *os_ << endl;
        }
    }

    if (os_ && !inExpectedButNotResult.empty())
    {
        *os_ << "Missing points in the result that were expected:\n";
        BOOST_FOREACH_FIELD((double driftTime)(const pwiz::analysis::Spectrum3D::value_type::second_type& spectrum), inExpectedButNotResult)
        {
            *os_ << driftTime << ":";
            BOOST_FOREACH_FIELD((double mz)(double intensity), spectrum)
                *os_ << " " << mz << "," << intensity;
            *os_ << endl;
        }
    }

    unit_assert(inResultButNotExpected.empty());
    unit_assert(inExpectedButNotResult.empty());
}


void parseArgs(const vector<string>& args, vector<string>& rawpaths)
{
    for (size_t i = 1; i < args.size(); ++i)
    {
        if (args[i] == "-v") os_ = &cout;
        else if (bal::starts_with(args[i], "--")) continue;
        else rawpaths.push_back(args[i]);
    }
}


int main(int argc, char* argv[])
{
    TEST_PROLOG_EX(argc, argv, "_CLI")

    try
    {
        vector<string> args(argv, argv+argc);
        vector<string> rawpaths;
        parseArgs(args, rawpaths);

        unit_assert(!rawpaths.empty());;

        BOOST_FOREACH(const string& filepath, rawpaths)
        {
            if (bal::ends_with(filepath, "ImsSynthCCS.d"))
            {
                MSDataFile^ msd = gcnew MSDataFile(ToSystemString(filepath));

                // scans 529, 554, 557, 615, 638
                test(msd, 4.2, "29.6-29.7 33.8-33.9 34.4-34.41 34.57-34.58 44.23-44.24 48.13-48.14",
                     "33.89830,99.9954,0 34.57627,99.9954,0 29.66101,99.9954,0 34.40678,99.9954,0 44.23729,99.9954,0 48.13559,99.9954,0 " // I don't know why these isolated points show up
                     "33.89830,174.03522,0 33.89830,174.04278,1 33.89830,174.05034,1 33.89830,174.05790,1 33.89830,174.06546,1 33.89830,174.07302,0 33.89830,174.35288,0 33.89830,174.36044,1 "
                     "33.89830,174.36801,1 33.89830,174.37558,1 33.89830,174.38314,1 33.89830,174.39071,1 33.89830,174.39828,1 33.89830,174.40585,1 33.89830,174.41342,1 34.57627,177.05704,0 "
                     "34.57627,177.06466,1 34.57627,177.07229,1 34.57627,177.07991,1 34.57627,177.08754,2 34.40678,177.09517,0 34.57627,177.09517,3 34.40678,177.10279,1 34.57627,177.10279,3 "
                     "34.40678,177.11042,1 34.57627,177.11042,4 34.40678,177.11804,1 34.57627,177.11804,4 34.40678,177.12567,1 34.57627,177.12567,5 34.40678,177.13330,1 34.57627,177.13330,5 "
                     "34.40678,177.14092,1 34.57627,177.14092,5 34.40678,177.14855,1 34.57627,177.14855,4 34.40678,177.15618,1 34.57627,177.15618,4 34.40678,177.16381,0 34.57627,177.16381,3 "
                     "34.57627,177.17143,2 34.57627,177.17906,1 34.57627,177.18669,1 34.57627,177.19432,1 34.57627,177.20195,0 34.57627,177.59120,0 34.57627,177.59884,1 34.57627,177.60648,1 "
                     "34.57627,177.61411,1 34.57627,177.62175,1 34.57627,177.62939,1 34.57627,177.63703,1 34.57627,177.64466,1 34.57627,177.65230,1 34.57627,177.65994,1 34.57627,177.66758,1 "
                     "34.57627,177.67522,0 29.66101,177.71341,0 29.66101,177.72105,1 29.66101,177.72869,1 29.66101,177.73633,1 29.66101,177.74397,2 29.66101,177.75161,2 29.66101,177.75925,3 "
                     "29.66101,177.76689,4 29.66101,177.77453,4 29.66101,177.78217,4 29.66101,177.78981,4 29.66101,177.79745,4 29.66101,177.80509,4 29.66101,177.81274,3 29.66101,177.82038,3 "
                     "29.66101,177.82802,2 29.66101,177.83566,1 29.66101,177.84330,1 29.66101,177.85094,1 29.66101,177.85859,0 29.66101,178.07264,0 29.66101,178.08029,1 29.66101,178.08793,1 "
                     "29.66101,178.09558,1 29.66101,178.10323,1 29.66101,178.11088,1 34.57627,178.11088,0 29.66101,178.11852,2 34.57627,178.11852,1 29.66101,178.12617,1 34.57627,178.12617,1 "
                     "29.66101,178.13382,1 34.57627,178.13382,1 29.66101,178.14147,1 34.57627,178.14147,1 29.66101,178.14912,1 34.57627,178.14912,0 29.66101,178.15677,1 29.66101,178.16442,1 "
                     "29.66101,178.17206,0 29.66101,178.39396,0 29.66101,178.40161,1 29.66101,178.40927,1 29.66101,178.41692,1 29.66101,178.42458,1 29.66101,178.43223,2 29.66101,178.43989,2 "
                     "29.66101,178.44754,2 29.66101,178.45520,2 29.66101,178.46285,2 29.66101,178.47051,2 29.66101,178.47816,1 29.66101,178.48582,1 29.66101,178.49347,1 29.66101,178.50113,1 "
                     "29.66101,178.50879,0 29.66101,178.76154,0 29.66101,178.76920,1 29.66101,178.77686,1 29.66101,178.78452,1 29.66101,178.79219,1 29.66101,178.79985,1 29.66101,178.80751,1 "
                     "34.57627,398.99589,0 34.57627,399.00734,1 34.57627,399.01878,1 34.57627,399.03023,1 34.57627,399.04168,1 34.57627,399.05313,2 34.40678,399.06457,0 34.57627,399.06457,2 "
                     "34.40678,399.07602,1 34.57627,399.07602,3 34.40678,399.08747,1 34.57627,399.08747,4 34.40678,399.09892,1 34.57627,399.09892,5 34.40678,399.11036,1 34.57627,399.11036,5 "
                     "34.40678,399.12181,1 34.57627,399.12181,6 34.40678,399.13326,1 34.57627,399.13326,7 34.40678,399.14471,1 34.57627,399.14471,7 34.40678,399.15616,1 34.57627,399.15616,7 "
                     "34.40678,399.16761,1 34.57627,399.16761,7 34.40678,399.17906,1 34.57627,399.17906,7 34.40678,399.19051,1 34.57627,399.19051,7 34.40678,399.20196,1 34.57627,399.20196,7 "
                     "34.40678,399.21341,1 34.57627,399.21341,6 34.40678,399.22486,1 34.57627,399.22486,5 34.40678,399.23631,1 34.57627,399.23631,5 34.40678,399.24776,1 34.57627,399.24776,4 "
                     "34.40678,399.25921,1 34.57627,399.25921,3 34.57627,399.27066,2 34.57627,399.28211,2 34.57627,399.29356,1 34.57627,399.30501,1 34.57627,399.31646,1 34.57627,399.32791,1 "
                     "34.57627,399.33936,0 34.57627,400.04968,0 34.57627,400.06114,1 34.57627,400.07260,1 34.57627,400.08406,1 34.57627,400.09552,1 34.57627,400.10699,1 34.57627,400.11845,1 "
                     "34.57627,400.12991,2 34.57627,400.14138,2 34.57627,400.15284,2 34.57627,400.16430,2 34.57627,400.17577,2 34.57627,400.18723,2 34.57627,400.19869,2 34.57627,400.21016,2 "
                     "34.57627,400.22162,1 34.57627,400.23309,1 34.57627,400.24455,1 34.57627,400.25601,1 34.57627,400.26748,1 34.57627,400.27894,1 48.13559,608.09487,0 48.13559,608.10900,1 "
                     "44.23729,608.12313,0 48.13559,608.12313,1 44.23729,608.13726,1 48.13559,608.13726,1 44.23729,608.15139,1 48.13559,608.15139,1 44.23729,608.16553,1 48.13559,608.16553,1 "
                     "44.23729,608.17966,1 48.13559,608.17966,1 44.23729,608.19379,1 48.13559,608.19379,1 44.23729,608.20792,1 48.13559,608.20792,1 44.23729,608.22206,1 48.13559,608.22206,1 "
                     "44.23729,608.23619,1 48.13559,608.23619,2 44.23729,608.25032,1 48.13559,608.25032,2 44.23729,608.26445,1 48.13559,608.26445,2 44.23729,608.27859,1 48.13559,608.27859,2 "
                     "44.23729,608.29272,1 48.13559,608.29272,2 44.23729,608.30685,1 48.13559,608.30685,2 44.23729,608.32099,1 48.13559,608.32099,2 44.23729,608.33512,1 48.13559,608.33512,1 "
                     "44.23729,608.34926,1 48.13559,608.34926,1 44.23729,608.36339,1 48.13559,608.36339,1 44.23729,608.37753,1 48.13559,608.37753,1 44.23729,608.39166,1 48.13559,608.39166,1 "
                     "44.23729,608.40579,1 48.13559,608.40579,1 48.13559,608.41993,1 48.13559,608.43406,1 48.13559,608.44820,0 48.13559,609.19760,0 48.13559,609.21175,1 48.13559,609.22589,1 "
                     "48.13559,609.24004,1 48.13559,609.25418,1 48.13559,609.26832,1 48.13559,609.28247,1 48.13559,609.29661,1 48.13559,609.31076,1 48.13559,609.32491,1 48.13559,609.33905,1");
            }
        }
    }
    catch (exception& e)
    {
        TEST_FAILED("std::exception: " + string(e.what()))
    }
    catch (System::Exception^ e)
    {
        TEST_FAILED("System.Exception: " + ToStdString(e->Message))
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
