//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

#include "Filter.hpp"
#include "Qonverter.hpp"

#pragma unmanaged
#include "../Filter.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"
#pragma managed


namespace IDPicker {


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::chemistry;
using namespace System::IO;
typedef NativeIDPicker::Filter NativeFilter;


namespace {

NativeFilter::Config makeNativeFilterConfig(Filterer::Configuration^ managedFilterConfig)
{
    NativeFilter::Config config;

    config.maxFDRScore = managedFilterConfig->MaxFDRScore;
    config.minDistinctPeptides = managedFilterConfig->MinDistinctPeptides;
    config.minSpectra = managedFilterConfig->MinSpectra;
    config.minAdditionalPeptides = managedFilterConfig->MinAdditionalPeptides;
    config.geneLevelFiltering = managedFilterConfig->GeneLevelFiltering;
    if (managedFilterConfig->PrecursorMzTolerance != nullptr)
        config.precursorMzTolerance = pwiz::chemistry::MZTolerance(managedFilterConfig->PrecursorMzTolerance->value, (pwiz::chemistry::MZTolerance::Units) managedFilterConfig->PrecursorMzTolerance->units);

    config.minSpectraPerDistinctMatch = managedFilterConfig->MinSpectraPerDistinctMatch;
    config.minSpectraPerDistinctPeptide = managedFilterConfig->MinSpectraPerDistinctPeptide;
    config.maxProteinGroupsPerPeptide = managedFilterConfig->MaxProteinGroupsPerPeptide;

    config.distinctMatchFormat.isAnalysisDistinct = managedFilterConfig->DistinctMatchFormat->IsAnalysisDistinct;
    config.distinctMatchFormat.isChargeDistinct = managedFilterConfig->DistinctMatchFormat->IsChargeDistinct;
    config.distinctMatchFormat.areModificationsDistinct = managedFilterConfig->DistinctMatchFormat->AreModificationsDistinct;
    if (config.distinctMatchFormat.areModificationsDistinct)
        config.distinctMatchFormat.modificationMassRoundToNearest = managedFilterConfig->DistinctMatchFormat->ModificationMassRoundToNearest;

    return config;
}

} // namespace


Filterer::Configuration::Configuration()
{
    NativeFilter::Config config;

    MaxFDRScore = config.maxFDRScore;
    MinDistinctPeptides = config.minDistinctPeptides;
    MinSpectra = config.minSpectra;
    MinAdditionalPeptides = config.minAdditionalPeptides;
    GeneLevelFiltering = config.geneLevelFiltering;
    if (config.precursorMzTolerance)
        PrecursorMzTolerance = gcnew MZTolerance(config.precursorMzTolerance.get().value, (MZTolerance::Units) config.precursorMzTolerance.get().units);

    MinSpectraPerDistinctMatch = config.minSpectraPerDistinctMatch;
    MinSpectraPerDistinctPeptide = config.minSpectraPerDistinctPeptide;
    MaxProteinGroupsPerPeptide = config.maxProteinGroupsPerPeptide;

    DistinctMatchFormat = gcnew Filterer::DistinctMatchFormat();
    DistinctMatchFormat->IsAnalysisDistinct = config.distinctMatchFormat.isAnalysisDistinct;
    DistinctMatchFormat->IsChargeDistinct = config.distinctMatchFormat.isChargeDistinct;
    DistinctMatchFormat->AreModificationsDistinct = config.distinctMatchFormat.areModificationsDistinct;
    if (DistinctMatchFormat->AreModificationsDistinct)
        DistinctMatchFormat->ModificationMassRoundToNearest = config.distinctMatchFormat.modificationMassRoundToNearest.get();
}


Filterer::Filterer()
{
    Config = gcnew Configuration();
}

void Filterer::Filter(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        NativeFilter filter;
        filter.config = makeNativeFilterConfig(Config);

        Logger::Initialize(); // make sure the logger is initialized

        filter.filter(ToStdString(idpDbFilepath), ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}

void Filterer::Filter(System::IntPtr idpDb, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    try
    {
        NativeFilter filter;
        filter.config = makeNativeFilterConfig(Config);

        Logger::Initialize(); // make sure the logger is initialized

        sqlite3* foo = (sqlite3*)idpDb.ToPointer();
        pin_ptr<sqlite3> idpDbPtr = foo;
        filter.filter(idpDbPtr, ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


} // namespace IDPicker
