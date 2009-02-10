//
// Reader_Bruker_Detail.hpp
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


#ifndef _READER_BRUKER_DETAIL_HPP_ 
#define _READER_BRUKER_DETAIL_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>

#import "CompassXtractMS.dll"
#import "BDal.CXt.Lc.Interfaces.tlb"
#import "BDal.CXt.Lc.tlb"


namespace pwiz {
namespace msdata {
namespace detail {


enum Reader_Bruker_Format
{
    Reader_Bruker_Format_Unknown,
    Reader_Bruker_Format_FID,
    Reader_Bruker_Format_YEP,
    Reader_Bruker_Format_BAF,
    Reader_Bruker_Format_U2
};

/// returns Bruker format of 'path' if it is a Bruker directory;
/// otherwise returns empty string
Reader_Bruker_Format format(const std::string& path);


/// a transparent wrapper for sharing the CompassXtract interface
class CompassXtractWrapper
{
    public:
        
    CompassXtractWrapper(const bfs::path& sourcePath, Reader_Bruker_Format sourceFormat);
    ~CompassXtractWrapper();

    typedef EDAL::IMSAnalysisPtr MS_AnalysisPtr;
    typedef EDAL::IMSSpectrumCollectionPtr MS_SpectrumCollectionPtr;

    typedef BDal_CXt_Lc_Interfaces::IAnalysisPtr LC_AnalysisPtr;
    typedef BDal_CXt_Lc_Interfaces::ISpectrumSourceDeclarationPtr LC_SpectrumSourceDeclarationPtr;
    typedef BDal_CXt_Lc_Interfaces::ITraceDeclarationPtr LC_TraceDeclarationPtr;

    typedef std::vector<LC_SpectrumSourceDeclarationPtr> LC_SpectrumSourceDeclarationList;
    typedef std::vector<LC_TraceDeclarationPtr> LC_TraceDeclarationList;

    Reader_Bruker_Format format_;

    MS_AnalysisPtr msAnalysis_;
    MS_SpectrumCollectionPtr msSpectrumCollection_;

    LC_AnalysisPtr lcAnalysis_;
    LC_SpectrumSourceDeclarationList spectrumSourceDeclarations_;
    LC_TraceDeclarationList traceDeclarations_;
};

typedef boost::shared_ptr<CompassXtractWrapper> CompassXtractWrapperPtr;


} // namespace detail
} // namespace msdata
} // namespace pwiz

#endif // _READER_BRUKER_DETAIL_HPP_
