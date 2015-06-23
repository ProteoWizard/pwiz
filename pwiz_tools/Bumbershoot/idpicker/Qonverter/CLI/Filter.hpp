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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "pwiz/utility/bindings/CLI/common/SharedCLI.hpp"
#include "Logger.hpp"

#pragma unmanaged
#include "../SchemaUpdater.hpp"
#pragma managed

#using <system.dll>
#pragma warning( pop )


namespace IDPicker {


using namespace System;
using namespace System::Collections::Generic;


/// encapsulates IDPicker's filtering functions
public ref struct Filterer
{
    ref struct DistinctMatchFormat
    {
        property bool IsChargeDistinct;
        property bool IsAnalysisDistinct;
        property bool AreModificationsDistinct;
        property double ModificationMassRoundToNearest;
    };

    ref struct Configuration
    {
        Configuration();

        property double MaxFDRScore;
        property int MinDistinctPeptides;
        property int MinSpectra;
        property int MinAdditionalPeptides;
        property bool GeneLevelFiltering;

        property int MinSpectraPerDistinctMatch;
        property int MinSpectraPerDistinctPeptide;
        property int MaxProteinGroupsPerPeptide;
        property DistinctMatchFormat^ DistinctMatchFormat;
    };

    property Configuration^ Config;

    Filterer();

    void Filter(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr);
    void Filter(System::IntPtr idpDbConnection, pwiz::CLI::util::IterationListenerRegistry^ ilr);
};


} // namespace IDPicker
