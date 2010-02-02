//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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
#include <stdexcept>


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::cv;
using namespace pwiz::CLI::msdata;
using namespace pwiz::CLI::analysis;
using namespace System;
using namespace System::Collections::Generic;


// TODO: test .NET filter predicate


void testPeakFilter()
{
    SpectrumListSimple^ sl = gcnew SpectrumListSimple();

    List<double>^ mzList = gcnew List<double>(gcnew array<double> {1,2,3,4,5,6,7,8,9});
    List<double>^ intensityList = gcnew List<double>(gcnew array<double> {1,5,2,4,2,6,7,4,1});

    Spectrum^ s = gcnew Spectrum();
    s->setMZIntensityArrays(mzList, intensityList);

    sl->spectra->Add(s);

    ThresholdFilter^ tf = gcnew ThresholdFilter(ThresholdFilter::ThresholdingBy_Type::ThresholdingBy_AbsoluteIntensity,
                                                3,
                                                ThresholdFilter::ThresholdingOrientation::Orientation_MostIntense);
    SpectrumList_PeakFilter^ slpf = gcnew SpectrumList_PeakFilter(sl, tf);
    Spectrum^ sf = slpf->spectrum(0, true);
    unit_assert(sf->defaultArrayLength == 5);

    BinaryData^ intensityListFiltered = sf->getIntensityArray()->data;
    unit_assert(intensityListFiltered->Count == 5);
    unit_assert(intensityListFiltered[0] == 5);
    unit_assert(intensityListFiltered[4] == 4);
}

// TODO: test more filters

int main()
{
    try
    {
        testPeakFilter();
        return 0;
    }
    catch (std::exception& e)
    {
        System::Console::Error->WriteLine("Caught std::exception not converted to System::Exception: " + gcnew System::String(e.what()));
        return 1;
    }
    catch (System::Exception^ e)
    {
        System::Console::Error->WriteLine(e->Message);
        return 1;
    }
    catch (...)
    {
        System::Console::Error->WriteLine("Caught unknown exception.");
        return 1;
    }
}
