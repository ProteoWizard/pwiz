//
// $Id$
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


#include "Reader_Bruker_Detail.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/filesystem/convenience.hpp"
#include "pwiz/data/msdata/Reader.hpp"


namespace pwiz {
namespace msdata {
namespace detail {

using namespace pwiz::util;

Reader_Bruker_Format format(const string& path)
{
    bfs::path sourcePath(path);

    // Make sure target "path" is actually a directory since
    // all Bruker formats are directory-based
    if (!bfs::is_directory(sourcePath))
    {
        // Special cases for identifying direct paths to fid/Analysis.yep/Analysis.baf/.U2
        // Note that direct paths to baf or u2 will fail to find a baf/u2 hybrid source
        std::string leaf = sourcePath.leaf();
        bal::to_lower(leaf);
        if (leaf == "fid" && !bfs::exists(sourcePath.branch_path() / "analysis.baf"))
            return Reader_Bruker_Format_FID;
        else if(extension(sourcePath) == ".u2")
            return Reader_Bruker_Format_U2;
        else if(leaf == "analysis.yep")
            return Reader_Bruker_Format_YEP;
        else if(leaf == "analysis.baf")
            return Reader_Bruker_Format_BAF;
        else
            return Reader_Bruker_Format_Unknown;
    }

    // TODO: 1SRef is not the only possible substring below, get more examples!

    // Check for fid-based data;
    // Every directory within the queried directory should have a "1/1SRef"
    // subdirectory with a fid file in it, but we check only the first non-dotted
    // directory for efficiency. This can fail, but those failures are acceptable.
    // Alternatively, a directory closer to the fid file can be identified.
    // Caveat: BAF files may be accompanied by a fid, skip these cases! (?)
    const static bfs::directory_iterator endItr;
    bfs::directory_iterator itr(sourcePath);
    for (; itr != endItr; ++itr)
        if (bfs::is_directory(itr->status()))
        {
            if (itr->path().leaf()[0] == '.') // HACK: skip ".svn"
                continue;
            else if (bfs::exists(itr->path() / "1/1SRef/fid") ||
                     bfs::exists(itr->path() / "1SRef/fid") ||
                     (bfs::exists(itr->path() / "fid") && !bfs::exists(itr->path() / "Analysis.baf") && !bfs::exists(itr->path() / "analysis.baf")) ||
                     (bfs::exists(sourcePath / "fid") && !bfs::exists(sourcePath / "Analysis.baf") && !bfs::exists(sourcePath / "analysis.baf")))
                    return Reader_Bruker_Format_FID;
            else
                break;
        }

    // Check for yep-based data;
    // The directory should have a file named "Analysis.yep"
    if (bfs::exists(sourcePath / "Analysis.yep") || bfs::exists(sourcePath / "analysis.yep"))
        return Reader_Bruker_Format_YEP;

    // Check for baf-based data;
    // The directory should have a file named "Analysis.baf"
    if (bfs::exists(sourcePath / "Analysis.baf") || bfs::exists(sourcePath / "analysis.baf"))
    {
        // Check for baf/u2 hybrid data
        string sourceDirectory = *(--sourcePath.end());
        if (bfs::exists(sourcePath / (sourceDirectory.substr(0, sourceDirectory.length()-2) + ".u2")))
            return Reader_Bruker_Format_BAF_and_U2;
        else
            return Reader_Bruker_Format_BAF;
    }

    // Check for u2-based data;
    // The directory should have a file named "<directory-name - ".d">.u2"
    string sourceDirectory = *(--sourcePath.end());
    if (bfs::exists(sourcePath / (sourceDirectory.substr(0, sourceDirectory.length()-2) + ".u2")))
        return Reader_Bruker_Format_U2;

    return Reader_Bruker_Format_Unknown;
}

} // namespace detail
} // namespace msdata
} // namespace pwiz
