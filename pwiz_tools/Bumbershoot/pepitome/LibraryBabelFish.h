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
// The Original Code is the Pepitome search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef _LIBRARYBABELFISH_
#define _LIBRARYBABELFISH_

#include "pepitome.h"
#include "spectraStore.h"

namespace freicore {
namespace pepitome {
struct LibraryBabelFish
{
    SpectraStore        library;
    string              libraryName;
    string              libraryIndexName;
    sqlite::database    libraryIndex;
    bool                error;

    LibraryBabelFish(const std::string& name);
    ~LibraryBabelFish();

    void initializeDatabase();
    static std::string mergeDatabaseWithContam(const std::string& database, const std::string& contam);
    static std::string mergeLibraryWithContam(const std::string& library, const std::string& contam);
    static void refreshLibrary(const std::string& library, proteinStore database, bool hcdMode, bool iTraqMode, const std::string& decoy = "rev_");
    void indexLibrary();
    void appendContaminants();
};

}
}

#endif
