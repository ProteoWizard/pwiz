//
// SpectrumList_Bruker.hpp
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
#include "pwiz/utility/misc/IntegerSet.hpp"
#import "CompassXtractMS.dll"
#include <map>

using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;

namespace pwiz {
namespace msdata {
namespace detail {


enum PWIZ_API_DECL SpectrumList_Bruker_Format
{
    FID,
    YEP,
    BAF
};


//
// SpectrumList_Bruker
//
class PWIZ_API_DECL SpectrumList_Bruker : public SpectrumList
{
    public:

    SpectrumList_Bruker(const MSData& msd,
                        const string& rootpath,
                        SpectrumList_Bruker_Format format,
                        EDAL::IMSAnalysisPtr& pAnalysis);
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    SpectrumPtr spectrum(size_t index, bool getBinaryData, const pwiz::util::IntegerSet& msLevelsToCentroid) const;


    private:

    const MSData& msd_;
    string rootpath_;
    SpectrumList_Bruker_Format format_;
    size_t size_;
    vector<SpectrumIdentity> index_;

    // nativeIdToIndexMap_[<function #>][<scan #>] == index
    map<string, size_t> nativeIdToIndexMap_;

    void createIndex();
    //string findPrecursorID(int precursorMsLevel, size_t index) const;

    // EDAL MSAnalysisPtr shared with Reader
    EDAL::IMSAnalysisPtr& pAnalysis_;
    EDAL::IMSSpectrumCollectionPtr pSpectra_;
};

} // detail
} // msdata
} // pwiz
