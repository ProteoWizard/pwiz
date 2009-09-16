//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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
                                                                                                     

#define PWIZ_SOURCE
#include "FeatureModeler.hpp"
#include "MZRTField.hpp"
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/proteome/IsotopeEnvelopeEstimator.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/math/distributions/normal.hpp"
#include <stdexcept>


namespace pwiz {
namespace analysis {


using namespace pwiz::data::peakdata;
using namespace pwiz::proteome;
using namespace pwiz::math;
using namespace std;
using boost::shared_ptr;


//
// Feature Modeler
//


void FeatureModeler::fitFeatures(FeatureField& featureField) const
{
    throw runtime_error("[FeatureModeler::fitFeatures()] Not implemented.");
}


//
// FeatureModeler_Gaussian
//


namespace {


class GaussianModel
{
    public:

    GaussianModel(double mean_mz, double sd_mz, double mean_rt, double sd_rt, int charge) 
    :   normal_mz_(mean_mz, sd_mz), normal_rt_(mean_rt, sd_rt), 
        charge_(charge),
        neutralMass_(Ion::neutralMass(mean_mz, charge)),
        intensity_(1)
    {
        if (!isotopeEnvelopeEstimator_.get())
            createIsotopeEnvelopeEstimator();

        envelope_ = isotopeEnvelopeEstimator_->isotopeEnvelope(neutralMass_);
        if (envelope_.empty()) throw runtime_error("[FeatureModeler::GaussianModel()] Empty envelope.");
    }

    static void createIsotopeEnvelopeEstimator()
    {
        const double abundanceCutoff = .01;
        const double massPrecision = .1; 
        IsotopeCalculator isotopeCalculator(abundanceCutoff, massPrecision);

        IsotopeEnvelopeEstimator::Config config;
        config.isotopeCalculator = &isotopeCalculator;
        config.normalization = IsotopeCalculator::NormalizeMass;

        isotopeEnvelopeEstimator_ = shared_ptr<IsotopeEnvelopeEstimator>(new IsotopeEnvelopeEstimator(config));
    }

    double operator()(double mz, double rt) const
    {
        double minMassDifference = numeric_limits<double>::max();
        size_t bestIndex = 0;
        double mass = Ion::neutralMass(mz, charge_);

        for (size_t i=0; i<envelope_.size(); i++)
        {
            double massDifference = fabs(mass - (neutralMass_ + envelope_[i].mass));
            if (minMassDifference > massDifference) 
            {
                minMassDifference = massDifference;
                bestIndex = i;
            }
        }

        double mz_shifted = Ion::mz(mass - envelope_[bestIndex].mass, charge_);
        double isotopeAbundance = envelope_[bestIndex].abundance;

        double result = intensity_ * isotopeAbundance * pdf(normal_mz_, mz_shifted) * pdf(normal_rt_, rt);
        return result;
    }


    private:

    static shared_ptr<IsotopeEnvelopeEstimator> isotopeEnvelopeEstimator_;

    boost::math::normal normal_mz_;
    boost::math::normal normal_rt_;

    int charge_;
    double neutralMass_;

    Chemistry::MassDistribution envelope_;

    double intensity_;

    friend ostream& operator<<(ostream& os, const GaussianModel& model)
    {
        os << "[mz(" << model.normal_mz_.mean() << "," << model.normal_mz_.standard_deviation() 
            << "),rt(" << model.normal_rt_.mean() << "," << model.normal_rt_.standard_deviation() << "),+" 
            << model.charge_ << "]";
        return os;
    }
};


shared_ptr<IsotopeEnvelopeEstimator> GaussianModel::isotopeEnvelopeEstimator_; // static storage


GaussianModel estimateParameters(const Feature& feature)
{
    // for now, simple estimation based on calculation of mean/variance of m/z and retention time
    // from cached peak data

    if (feature.peakels.empty()) throw runtime_error("[FeatureModeler::estimateParameters()] Empty feature.");

    const Peakel& peakel = *feature.peakels[0];

    double peakelSumIntensity = 0;
    double peakelSumIntensityMZ = 0;
    double peakelSumIntensityMZ2 = 0;
    double peakelSumIntensityRT = 0;
    double peakelSumIntensityRT2 = 0;

    for (vector<Peak>::const_iterator peak=peakel.peaks.begin(); peak!=peakel.peaks.end(); ++peak)
    {
        double peakSumIntensity = 0;
        double peakSumIntensityMZ = 0;
        double peakSumIntensityMZ2 = 0;

        for (vector<OrderedPair>::const_iterator it=peak->data.begin(); it!=peak->data.end(); ++it)
        {
            peakSumIntensity += it->y;
            peakSumIntensityMZ += (it->x * it->y);
            peakSumIntensityMZ2 += (it->x * it->x * it->y);
        }
    
        peakelSumIntensity += peakSumIntensity;
        peakelSumIntensityMZ += peakSumIntensityMZ;
        peakelSumIntensityMZ2 += peakSumIntensityMZ2;
        peakelSumIntensityRT += peakSumIntensity * peak->retentionTime;
        peakelSumIntensityRT2 += peakSumIntensity * peak->retentionTime * peak->retentionTime;
    }

    double peakelMeanMZ = peakelSumIntensityMZ/peakelSumIntensity;
    double peakelMeanMZ2 = peakelSumIntensityMZ2/peakelSumIntensity;
    double peakelVarianceMZ = peakelMeanMZ2 - peakelMeanMZ*peakelMeanMZ;
    double peakelStandardDeviationMZ = sqrt(peakelVarianceMZ);

    double peakelMeanRT = peakelSumIntensityRT/peakelSumIntensity;
    double peakelMeanRT2 = peakelSumIntensityRT2/peakelSumIntensity;
    double peakelVarianceRT = peakelMeanRT2 - peakelMeanRT*peakelMeanRT;
    double peakelStandardDeviationRT = sqrt(peakelVarianceRT);

    return GaussianModel(peakelMeanMZ, peakelStandardDeviationMZ,
                         peakelMeanRT, peakelStandardDeviationRT,
                         feature.charge);
}


void quantifyFit(const GaussianModel& model, const Feature& feature)
{
    double sumModelData = 0;
    double sumModel2 = 0;
    double sumData2 = 0;

    for (vector<PeakelPtr>::const_iterator peakel=feature.peakels.begin(); peakel!=feature.peakels.end(); ++peakel)
    {
        for (vector<Peak>::const_iterator peak=(*peakel)->peaks.begin(); peak!=(*peakel)->peaks.end(); ++peak)
        {
            double rt = peak->retentionTime;

            for (vector<OrderedPair>::const_iterator it=peak->data.begin(); it!=peak->data.end(); ++it)
            {
                double mz = it->x;
                double value_data = it->y;
                double value_model = model(mz, rt);
                    
                // (mz,rt): data model
                //cout << "(" << mz << "," << rt << ") " << value_data << " " << value_model << endl;

                sumModelData += value_model * value_data;
                sumModel2 += value_model * value_model;
                sumData2 += value_data * value_data;
            }
        }
    }

    double intensity = sumModelData/sqrt(sumModel2);
    double cosine = intensity/sqrt(sumData2);

    cout << "intensity: " << intensity << endl;
    cout << "cosine: " << cosine << endl;
/*

    double sumSquaredDifferences = sumData2 - 2*intensity*sumModelData + intensity*intensity*sumModel2;
    cout << "sumSquaredDifferences: " << sumSquaredDifferences << endl;

    sumSquaredDifferences = sumData2 - 2*intensity*sumModelData/5 + intensity*intensity*sumModel2/25;
    cout << "sumSquaredDifferences: " << sumSquaredDifferences << endl;


    for (vector<PeakelPtr>::const_iterator peakel=feature.peakels.begin(); peakel!=feature.peakels.end(); ++peakel)
    {
        for (vector<Peak>::const_iterator peak=(*peakel)->peaks.begin(); peak!=(*peakel)->peaks.end(); ++peak)
        {
            double rt = peak->retentionTime;

            for (vector<OrderedPair>::const_iterator it=peak->data.begin(); it!=peak->data.end(); ++it)
            {
                double mz = it->x;
                double value_data = it->y;
                double value_model = intensity * model(mz, rt);
                    
                // (mz,rt): data model
                cout << "(" << mz << "," << rt << ") " << value_data << " " << value_model << endl;
            }
        }
    }
*/
}


} // namespace


void FeatureModeler_Gaussian::fitFeature(Feature& feature) const
{
    cout << "we're here!\n";

    GaussianModel model = estimateParameters(feature);

    cout.precision(10);
    cout << "model: " << model << "\n\n";

    quantifyFit(model, feature); 
}


} // namespace analysis
} // namespace pwiz


