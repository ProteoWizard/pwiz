//
// ChromatogramList_Bruker.hpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "Reader_Bruker_Detail.hpp"
#include <map>

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;

namespace pwiz {
namespace msdata {
namespace detail {


//
// SpectrumList_Bruker
//
class PWIZ_API_DECL ChromatogramList_Bruker : public ChromatogramList
{
    public:

    ChromatogramList_Bruker(MSData& msd,
                            const string& rootpath,
                            Reader_Bruker_Format format,
                            CompassXtractWrapperPtr compassXtractWrapperPtr);

    virtual size_t size() const;
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;


    private:

    MSData& msd_;
    bfs::path rootpath_;
    Reader_Bruker_Format format_;
    size_t size_;

    struct IndexEntry : public ChromatogramIdentity
    {
        size_t declaration;
        long trace;
    };

    vector<IndexEntry> index_;

    // idToIndexMap_["scan=<#>" or "file=<sourceFile::id>"] == index
    map<string, size_t> idToIndexMap_;

    void createIndex();

    CompassXtractWrapperPtr compassXtractWrapperPtr_;
};

} // detail
} // msdata
} // pwiz
