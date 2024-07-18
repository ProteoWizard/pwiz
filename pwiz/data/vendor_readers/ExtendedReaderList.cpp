//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#define PWIZ_SOURCE

#include "ExtendedReaderList.hpp"
#include "ABI/Reader_ABI.hpp"
#include "ABI/T2D/Reader_ABI_T2D.hpp"
#include "Agilent/Reader_Agilent.hpp"
#include "Bruker/Reader_Bruker.hpp"
#include "Mobilion/Reader_Mobilion.hpp"
#include "Shimadzu/Reader_Shimadzu.hpp"
#include "Thermo/Reader_Thermo.hpp"
#include "UIMF/Reader_UIMF.hpp"
#include "UNIFI/Reader_UNIFI.hpp"
#include "Waters/Reader_Waters.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


PWIZ_API_DECL ExtendedReaderList::ExtendedReaderList()
{
    push_back(ReaderPtr(new Reader_ABI));
    push_back(ReaderPtr(new Reader_ABI_WIFF2));
    push_back(ReaderPtr(new Reader_ABI_T2D));
    push_back(ReaderPtr(new Reader_Agilent));
    push_back(ReaderPtr(new Reader_Bruker_BAF));
    push_back(ReaderPtr(new Reader_Bruker_TDF));
    push_back(ReaderPtr(new Reader_Bruker_TSF));
#if !defined(PWIZ_READER_BRUKER) || defined(PWIZ_READER_BRUKER_WITH_COMPASSXTRACT)
    push_back(ReaderPtr(new Reader_Bruker_FID));
    push_back(ReaderPtr(new Reader_Bruker_YEP));
    push_back(ReaderPtr(new Reader_Bruker_U2));
#endif
#if defined(PWIZ_READER_MOBILION)
	push_back(ReaderPtr(new Reader_Mobilion));
#endif	
    push_back(ReaderPtr(new Reader_Shimadzu));
    push_back(ReaderPtr(new Reader_Thermo));
    push_back(ReaderPtr(new Reader_UIMF));
    push_back(ReaderPtr(new Reader_UNIFI));
    push_back(ReaderPtr(new Reader_Waters));
}


} // namespace msdata
} // namespace pwiz
