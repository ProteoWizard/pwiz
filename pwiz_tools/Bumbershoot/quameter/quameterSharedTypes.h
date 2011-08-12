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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):


#ifndef _QUAMETERSHAREDTYPES_H
#define _QUAMETERSHAREDTYPES_H

#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>
#include <vector>
#include <map>

using namespace boost::icl;
using namespace std;

namespace freicore
{
namespace quameter
{

    enum EnzymaticStatus {NON_ENZYMATIC = 0, SEMI_ENZYMATIC, FULLY_ENZYMATIC};

    template<typename Sample>
    struct quartile
    {
        // for boost::result_of
        typedef Sample result_type;

        quartile() : buffer_(0), isSorted(false)
        {
        }

        quartile(quartile const &that)
            : buffer_(that.buffer_), isSorted(that.isSorted)
        {
        }

        void operator ()(Sample &value) 
        {
            this->buffer_.push_back(value);
            this->isSorted = false;
        }

        result_type extract_quartile(size_t numQuartile)
        {
            BOOST_ASSERT(this->buffer_.size() >= 4);
            BOOST_ASSERT(numQuartile >= 1 );
            BOOST_ASSERT(numQuartile < 4 );
            if(!this->isSorted)
            {
                std::sort(this->buffer_.begin(),this->buffer_.end());
                this->isSorted = true;
            }
            size_t quartileSize = (size_t) this->buffer_.size()/4;
            if(numQuartile == 2)
                return this->buffer_.at(quartileSize*2);
            else if(numQuartile == 3)
                return this->buffer_.at(quartileSize*3);
            return this->buffer_.at(quartileSize);
        }

        result_type extract_IQR()
        {
            BOOST_ASSERT(this->buffer_.size() >= 4);
            if(!this->isSorted)
            {
                std::sort(this->buffer_.begin(),this->buffer_.end());
                this->isSorted = true;
            }
            size_t quartileSize = (size_t) this->buffer_.size()/4;
            return this->buffer_.at(quartileSize*3) - this->buffer_.at(quartileSize);
        }

    private:
        quartile &operator=(quartile const &);
        std::vector<Sample> buffer_;
        bool isSorted;
    };

    struct MS2ScanInfo
    {
        string MS2;
        double MS2Retention;
        string precursor;
        double precursorMZ;
        double precursorIntensity;
        double precursorRetention;
    };

    struct preMZandRT {
        double MS2Retention;
        double precursorMZ;
        double precursorRetention;
    };

    struct LocalChromatogram
    {
        vector<double> MS1Intensity;
        vector<double> MS1RT;

        LocalChromatogram(){}

        LocalChromatogram(vector<double> intens, vector<double> rt)
        {
            MS1Intensity = intens;
            MS1RT = rt;
        }

    };

    struct fourInts {
        int first;
        int second;
        int third;
        int fourth;
    };

    struct MassErrorStats
    {
        double medianError;
        double meanAbsError;
        double medianPPMError;
        double PPMErrorIQR;
    };

    struct XICWindows 
    {
        int peptide;
        double firstMS2RT;
        interval_set<double> preMZ;
        interval_set<double> preRT;
        vector<double> MS1Intensity;
        vector<double> MS1RT;
    };


    struct IntensityPair 
    {
        double precursorIntensity;
        double peakIntensity;

        IntensityPair(double precIntens, double peakIntens)
        {
            precursorIntensity = precIntens;
            peakIntensity = peakIntens;
        }
    };

}
}

#endif

