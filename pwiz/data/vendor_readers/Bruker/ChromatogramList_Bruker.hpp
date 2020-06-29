//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"
#include "pwiz/data/msdata/Reader.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Container.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "Reader_Bruker_Detail.hpp"


using boost::shared_ptr;


namespace pwiz {
namespace msdata {
namespace detail {

//
// ChromatogramList_Bruker
//
class PWIZ_API_DECL ChromatogramList_Bruker : public ChromatogramListBase
{
    public:

    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    virtual ChromatogramPtr chromatogram(size_t index, DetailLevel detailLevel) const;

#ifdef PWIZ_READER_BRUKER
    ChromatogramList_Bruker(MSData& msd,
                            const string& rootpath,
                            Bruker::Reader_Bruker_Format format,
                            CompassDataPtr compassDataPtr,
                            const Reader::Config& config);

    private:

    MSData& msd_;
    bfs::path rootpath_;
    Bruker::Reader_Bruker_Format format_;
    CompassDataPtr compassDataPtr_;
    size_t size_;
    Reader::Config config_;

    struct IndexEntry : public ChromatogramIdentity
    {
        CVID chromatogramType;
        size_t declaration;
        long trace;
    };

    vector<IndexEntry> index_;

    // idToIndexMap_["scan=<#>" or "file=<sourceFile::id>"] == index
    map<string, size_t> idToIndexMap_;

    void createIndex();
#endif // PWIZ_READER_BRUKER
};

} // detail
} // msdata
} // pwiz
