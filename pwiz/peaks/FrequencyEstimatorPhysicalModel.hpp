//
// FrequencyEstimatorPhysicalModel.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 
#define _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 


#include "FrequencyEstimator.hpp" 
#include <string>
#include <memory>


namespace pwiz {
namespace peaks {


/// Physical model implementation of the FrequencyEstimator interface. 
class FrequencyEstimatorPhysicalModel : public FrequencyEstimator
{
    public:

    struct Config
    {
        unsigned int windowRadius;
        unsigned int iterationCount;
        std::string outputDirectory; // ("" == no logging output)

        Config() : windowRadius(10), iterationCount(20) {}
    };

    /// create an instance
    static std::auto_ptr<FrequencyEstimatorPhysicalModel> create(const Config& config);

    /// \name FrequencyEstimator interface
    //@{
    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const = 0; 

    virtual ~FrequencyEstimatorPhysicalModel(){}
    //@}
};


} // namespace peaks
} // namespace pwiz


#endif // _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 


