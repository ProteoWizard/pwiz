//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
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



/*

experiment model: (full lcms run, raw/mzxml file)
  sample:      
    <protein, abundance>
  protocol:  
    digestion, LC gradient, RT-dependence

chromatographic fraction model: (single scan, single transient data file)
  species collection:
    <elemental composition EC, charge distribution <z,A> >
  instrument configuration:
    calibration parameters, observation duration T, 
    tau-dependence, phi-dependence

peak model: (ion packet)
    <A, f, phi, tau>

*/


#include "pwiz/utility/proteome/Chemistry.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include <vector>
#include <iostream>
#include <complex> // TODO: remove
#include <iterator>// TODO: remove


namespace pwiz {
namespace id {


/// scope for data structures used to model an LCMS experiment
namespace model {


/// charge-abundance pair
struct ChargeAbundance
{
    int charge;
    double abundance;

    ChargeAbundance(int z = 0, double a = 0)
    :   charge(z), abundance(a)
    {}
};


inline std::ostream& operator<<(std::ostream& os, const ChargeAbundance& ca)
{
    os << "(" << ca.charge << "," << ca.abundance << ")"; 
    return os;
}


inline std::istream& operator>>(std::istream& is, ChargeAbundance& ca)
{
    std::complex<double> buffer;
    is >> buffer;
    ca.charge = (int)(buffer.real());
    ca.abundance = buffer.imag();
    return is;
}


/// charge-abundance distribution
typedef std::vector<ChargeAbundance> ChargeDistribution;


/// chemical species, described by elemental composition and 
/// charge state distribution 
struct Species 
{
    chemistry::Formula formula;
    ChargeDistribution chargeDistribution;

    Species(){}

    Species(const chemistry::Formula& f, 
            const ChargeDistribution& cd)
    :   formula(f), chargeDistribution(cd)
    {}
};


inline std::ostream& operator<<(std::ostream& os, const Species& species)
{
    os << species.formula << " " << species.formula.monoisotopicMass() << " ";
    copy(species.chargeDistribution.begin(), species.chargeDistribution.end(), 
         std::ostream_iterator<ChargeAbundance>(os, " "));
    return os;
}


/// state of the FT instrument 
struct InstrumentConfiguration
{
    class PhaseFunction
    {
        public:
        virtual double operator()(double frequency) const = 0;
        virtual ~PhaseFunction(){}
    };

    class DecayFunction
    {
        public:
        virtual double operator()(double frequency) const = 0;
        virtual ~DecayFunction(){}
    };

    data::CalibrationParameters calibrationParameters;
    double observationDuration;
    int sampleCount;
    const PhaseFunction* phaseFunction;
    const DecayFunction* decayFunction;

    InstrumentConfiguration()
    :   observationDuration(0), sampleCount(0),
        phaseFunction(0), decayFunction(0)
    {}
};


class ConstantPhaseFunction : public InstrumentConfiguration::PhaseFunction
{
    public:
    ConstantPhaseFunction(double phase = 0) : phase_(phase) {}
    virtual double operator()(double frequency) const {return phase_;} 
    private:
    double phase_;
};


class LinearPhaseFunction : public InstrumentConfiguration::PhaseFunction
{
    public:
    LinearPhaseFunction(double timeDelay = 0) : slope_(timeDelay*2*M_PI) {}
    virtual double operator()(double frequency) const {return frequency*slope_;} 
    private:
    double slope_;
};


class ConstantDecayFunction : public InstrumentConfiguration::DecayFunction
{
    public:
    ConstantDecayFunction(double decay = 0) : decay_(decay) {}
    virtual double operator()(double frequency) const {return decay_;} 
    private:
    double decay_;
};


/// FT scan, described by the set of species and the instrument state 
struct ChromatographicFraction
{
    std::vector<Species> species;
    InstrumentConfiguration instrumentConfiguration;
};


/// single packet of ions at a given frequency (m/z)
struct IonPacket
{
    double frequency;
    double abundance;
    double phase;
    double decay;

    IonPacket(double f, double a, double p, double d)
    :   frequency(f), abundance(a), phase(p), decay(d)
    {}
};


inline std::ostream& operator<<(std::ostream& os, const IonPacket& packet)
{
    os << "<" << packet.frequency << ", " << packet.abundance << ", " 
       << packet.phase << ", " << packet.decay << ">";
    return os;
}


} // namespace model
} // namespace id 
} // namespace pwiz

