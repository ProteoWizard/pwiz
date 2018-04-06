//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _FREQUENCYDATA_HPP_
#define _FREQUENCYDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "SampleDatum.hpp"
#include "CalibrationParameters.hpp"
#include <vector>
#include <complex>
#include <memory>


namespace pwiz {
namespace data {


typedef SampleDatum< double, std::complex<double> > FrequencyDatum;


/// Class for binary storage of complex frequency data.

/// Stores raw frequency-domain data, as well as meta-data.  Also includes
/// basic access and analysis functions.

class PWIZ_API_DECL FrequencyData
{
    public:

    /// \name types
    //@{
        enum PWIZ_API_DECL IOMode {Binary, Text, Automatic};
        typedef std::vector<FrequencyDatum> container;
        typedef container::iterator iterator;
        typedef container::const_iterator const_iterator;
    //@}

    /// \name instantiation
    //@{
        FrequencyData();
        FrequencyData(const std::string& filename, IOMode mode=Automatic);
        FrequencyData(const FrequencyData& that, const_iterator begin, const_iterator end);
        FrequencyData(const FrequencyData& that, const_iterator center, int radius);
        ~FrequencyData();
    //@}

    /// \name I/O
    //@{
        void read(const std::string& filename, IOMode mode=Automatic);
        void read(std::istream& is, IOMode mode=Binary);
        void write(const std::string& filename, IOMode mode=Binary) const;
        void write(std::ostream& os, IOMode mode=Binary) const;
    //@}

    /// \name data access
    //@{
        /// const access to underlying data
        const container& data() const;

        /// non-const access to underlying data -- must call analyze() to recache after any changes
        container& data();
    //@}

    /// \name metadata
    //@{
        int scanNumber() const;
        void scanNumber(int value);

        double retentionTime() const;
        void retentionTime(double value);

        const CalibrationParameters& calibrationParameters() const;
        void calibrationParameters(const CalibrationParameters& cp);

        double observationDuration() const;
        void observationDuration(double value);

        double noiseFloor() const;
        void noiseFloor(double value);
    //@}

    /// \name data transformation 
    //@{
        /// transform all underlying data: (x,y) -> (x+shift,y*scale)
        void transform(double shift, std::complex<double> scale); 

        /// return current shift of data (compared to original)
        double shift() const;

        /// return current scale of data (compared to original)
        std::complex<double> scale() const;

        /// normalize by transform( -max.x, 1/abs(max.y) ) 
        void normalize();

        /// addition
        void operator+=(const FrequencyData& that);
    //@}

    /// \name analysis
    //@{
        /// recache statistics calculations after any direct data changes via non-const data() 
        void analyze();

        /// returns an iterator to FrequencyDatum with highest magnitude
        const_iterator max() const;

        double mean() const;
        double meanSquare() const;
        double sumSquares() const;
        double variance() const;

        /// special calculation of noise floor for data with zero holes,
        /// e.g. data obtained from RAW file m/z-intensity pairs
        double cutoffNoiseFloor() const;

        /// calculation of the observation duration from the data
        double observationDurationEstimatedFromData() const;
    //@}

    /// \name auxilliary
    //@{
        /// Finds the FrequencyDatum nearest the desired frequency.
        const_iterator findNearest(double frequency) const;
    //@}

    /// \name auxilliary functions
    //@{
        /// Returns a <frequency,magnitude> pair.
        static std::pair<double,double> magnitudeSample(const FrequencyDatum& datum);
    //@}

    private:
    struct Impl;
    std::auto_ptr<Impl> impl_;

    /// Hidden to prevent unintended copying of large amounts of data.
    FrequencyData(FrequencyData& that);

    /// Hidden to prevent unintended copying of large amounts of data.
    FrequencyData& operator=(FrequencyData& that);
};


} // namespace data 
} // namespace pwiz


#endif // _FREQUENCYDATA_HPP_

