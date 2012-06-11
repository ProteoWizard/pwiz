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

#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "DefaultReaderList.hpp"
#include "Reader_FASTA.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"

namespace pwiz {
namespace proteome {


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList(bool indexed /*= false*/)
{
    Reader_FASTA::Config fastaConfig;
    fastaConfig.indexed = indexed;
    push_back(ReaderPtr(new Reader_FASTA(fastaConfig)));
}


} // namespace proteome
} // namespace pwiz
