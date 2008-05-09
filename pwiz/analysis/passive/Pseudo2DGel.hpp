//
// Pseudo2DGel.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _PSEUDO2DGEL_HPP_
#define _PSEUDO2DGEL_HPP_


#include "utility/misc/Export.hpp"
#include <boost/shared_ptr.hpp>

#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"
#include "analysis/peptideid/PeptideID.hpp"


namespace pwiz {
namespace analysis {


/// creates pseudo-2D-gel images from survey scan data 
class PWIZ_API_DECL Pseudo2DGel : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config
    {
        std::string label;
        float mzLow;
        float mzHigh;
        int binCount;
        float zRadius;
        bool bry;
        bool binSum;
        bool ms2;
        boost::shared_ptr<pwiz::peptideid::PeptideID> peptide_id;

        Config(const std::string& args);
        Config(const std::string& args,
               boost::shared_ptr<pwiz::peptideid::PeptideID> peptide_id);
    };

    Pseudo2DGel(const MSDataCache& cache, const Config& config);

    /// \name MSDataAnalyzer interface
    //@{
    virtual void open(const DataInfo& dataInfo);

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);

    virtual void close(const DataInfo& dataInfo);
    //@}

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Pseudo2DGel(Pseudo2DGel&);
    Pseudo2DGel& operator=(Pseudo2DGel&);
};


template<>
struct analyzer_strings<Pseudo2DGel>
{
    static const char* id() {return "image";}
    static const char* description() {return "create pseudo-2D-gel image";}
    static const char* argsFormat() {return "[args]";}
    static std::vector<std::string> argsUsage()
    {
        std::vector<std::string> result;
        result.push_back("label=xxxx (set filename label to xxxx)");
        result.push_back("mzLow=N (set low m/z cutoff)");
        result.push_back("mzHigh=N (set high m/z cutoff)");
        result.push_back("binCount=N (set histogram bin count)");
        result.push_back("zRadius=N (set intensity function z-score radius [=2])");
        result.push_back("bry (use blue-red-yellow gradient)");
        result.push_back("binSum (sum intensity in bins [default = max intensity])");
        result.push_back("ms2locs (indicate masses selected for ms2)");
        return result; 
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _PSEUDO2DGEL_HPP_

