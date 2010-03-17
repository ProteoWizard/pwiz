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


#include "TransientGenerator.hpp"


namespace pwiz {
namespace id {


using namespace proteome;
using namespace model;
using namespace data;
using namespace std;


class TransientGenerator::Impl
{
    public:

    Impl(const IsotopeCalculator& ic)
    :   isotopeCalculator_(ic)
    {}

    vector<IonPacket> calculateIonPackets(const ChromatographicFraction& cf);

    private:

    const IsotopeCalculator& isotopeCalculator_;
};


TransientGenerator::TransientGenerator(const IsotopeCalculator& ic)
:   impl_(new Impl(ic))
{}


TransientGenerator::~TransientGenerator()
{} // auto-destruction of impl_


vector<IonPacket> TransientGenerator::Impl::calculateIonPackets(const ChromatographicFraction& cf)
{
    vector<IonPacket> result;

    for (vector<Species>::const_iterator itSpecies=cf.species.begin();
         itSpecies!=cf.species.end(); ++itSpecies)
    {
        for (ChargeDistribution::const_iterator itChargeDistribution=itSpecies->chargeDistribution.begin();
             itChargeDistribution!=itSpecies->chargeDistribution.end(); ++itChargeDistribution)
        {
            const int& z = itChargeDistribution->charge;
            const double& a = itChargeDistribution->abundance;

            Chemistry::MassDistribution md = 
                isotopeCalculator_.distribution(itSpecies->formula, z); 

            for (Chemistry::MassDistribution::const_iterator it=md.begin(); it!=md.end(); ++it)
            {
                const CalibrationParameters& cp = cf.instrumentConfiguration.calibrationParameters;
                double frequency = cp.frequency(it->mass);
                double abundance = it->abundance * a;
                double phase = (*cf.instrumentConfiguration.phaseFunction)(frequency); 
                double decay = (*cf.instrumentConfiguration.decayFunction)(frequency); 

                result.push_back(IonPacket(frequency, abundance, phase, decay));
            }
        }
    }

    return result;
}


namespace {


class DecayingSinusoid
{
    public:

    DecayingSinusoid(double f, double A, double phi, double tau)
    :   f_(f), A_(A), phi_(phi), tau_(tau) 
    {}

    double operator()(double t) const
    {
        return A_ * exp(-t/tau_) * cos(2.*M_PI*f_*t + phi_);
    }

    private:

    double f_;      // frequency
    double A_;      // amplitude
    double phi_;    // phase
    double tau_;    // decay constant
};


class CombinedSignal : public TransientData::Signal
{
    public:

    void push_back(DecayingSinusoid signal) {signals_.push_back(signal);}

    virtual double operator()(double t) const
    {
        double value = 0;
        for (vector<DecayingSinusoid>::const_iterator it=signals_.begin(); 
             it!=signals_.end(); ++it) 
            value += (*it)(t);
        return value;
    }

    private:

    vector<DecayingSinusoid> signals_;
};


} // namespace 


auto_ptr<TransientData> 
    TransientGenerator::createTransientData(const model::ChromatographicFraction& cf)
{
    vector<IonPacket> ionPackets = impl_->calculateIonPackets(cf);

    CombinedSignal signal;
    for (vector<IonPacket>::const_iterator it=ionPackets.begin(); it!=ionPackets.end(); ++it)
        signal.push_back(DecayingSinusoid(it->frequency, it->abundance, it->phase, it->decay)); 

    const InstrumentConfiguration& ic = cf.instrumentConfiguration;
    auto_ptr<TransientData> td(new TransientData);
    td->A(ic.calibrationParameters.A);
    td->B(ic.calibrationParameters.B);
    td->observationDuration(ic.observationDuration);
    td->data().resize(ic.sampleCount);
    td->add(signal);
    return td;
}


} // namespace id 
} // namespace pwiz


