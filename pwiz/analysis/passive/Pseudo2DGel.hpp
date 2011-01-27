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


#ifndef _PSEUDO2DGEL_HPP_
#define _PSEUDO2DGEL_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <boost/shared_ptr.hpp>

#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"
#include "pwiz/analysis/peptideid/PeptideID.hpp"


namespace pwiz {
namespace analysis {


/// Creates pseudo-2D-gel images from survey scan data.
///
/// The Pseudo2DGel class is a data analyzer that constructs an image
/// from data in to the update method by a MSDataAnalyzerDriver
/// object. FT and IT scans are separated and, if present, used to
/// construct two separate images. Output is controlled by the
/// Pseudo2DGel::Config object passed to the constructor.
class PWIZ_API_DECL Pseudo2DGel : public MSDataAnalyzer
{
    public:

    enum MarkupShape
    {
        circle = 0,
        square = 1
    };
    
    /// Holds the configuration for an instance of Pseudo2DGel.
    ///
    /// Controls both the image generation as well as markups on the
    /// image from ms2 scans or peptide locations.
    struct PWIZ_API_DECL Config
    {
        /// The filename label.
        std::string label;

        /// lower m/z cutoff. 
        float mzLow;

        /// upper m/z cutoff.
        float mzHigh;

        /// scale of the time axis.
        float timeScale;

        /// histogram bin count.
        int binCount;

        /// intensity z-score function radius.
        float zRadius;

        /// flag for blue-red-yellow coloration.
        bool bry;

        /// flag for grey scale coloration.
        bool grey;

        /// flag to sum intensity in bins. This flag causes bins to be
        /// the sum of the underlying values. If it's false, then the
        /// maximum value for the region will be used.
        bool binSum;

        /// flag to set marks for ms2 locations.
        bool ms2;

        /// flag to only draw ms2 locations that exist in the
        /// peptide_id object and have a score >= 0.
        bool positiveMs2Only;

        /// flag to render linearly with scan. This flag will switch
        /// between having a y-axis that's linear with scan or linear
        /// with time.
        bool binScan;

        /// PeptideID object to retrieve peptide id's from. If the
        /// object exists, it will be queried for peptide
        /// locations. 
        boost::shared_ptr<pwiz::peptideid::PeptideID> peptide_id;

        /// The shape used for pseudo2d gel markups.
        MarkupShape markupShape;
        
        Config();
        Config(const std::string& args);
        Config(const std::string& args,
               boost::shared_ptr<pwiz::peptideid::PeptideID> peptide_id);

        /// parses a keyword/value string to set instance variables.
        void process(const std::string& args);
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

    friend struct prob_comp;
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
        result.push_back("timeScale=N (set scaling factor for time axis)");
        result.push_back("binCount=N (set histogram bin count)");
        result.push_back("zRadius=N (set intensity function z-score radius [=2])");
        result.push_back("scan (render y-axis linear with scans)");
        result.push_back("time (render y-axis linear with time)");
        result.push_back("bry (use blue-red-yellow gradient)");
        result.push_back("grey (use grey-scale gradient)");
        result.push_back("binSum (sum intensity in bins [default = max intensity])");
        result.push_back("ms2locs (indicate masses selected for ms2)");
        result.push_back("pepxml=xxx (set ms2 id's from pepxml file xxx)");
        result.push_back("msi=xxx (set ms2 id's from msinspect output file xxx)");
        result.push_back("flat=xxx (set ms2 id's from tab delim file xxx)");
        return result; 
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _PSEUDO2DGEL_HPP_

