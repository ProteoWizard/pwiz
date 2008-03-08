//
// FrequencyEstimatorSimple.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _FREQUENCYESTIMATORSIMPLE_HPP_
#define _FREQUENCYESTIMATORSIMPLE_HPP_


#include "FrequencyEstimator.hpp" 
#include <memory>


namespace pwiz {
namespace peaks {


/// Simple implementation of the FrequencyEstimator interface. 
class FrequencyEstimatorSimple : public FrequencyEstimator
{
    public:

    enum Type {LocalMax, Parabola, Lorentzian};

    /// create an instance
    static std::auto_ptr<FrequencyEstimatorSimple> create(Type type = Parabola,
                                                          unsigned int windowRadius = 1);
                                                         
    /// \name FrequencyEstimator interface
    //@{
    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const = 0; 
    virtual ~FrequencyEstimatorSimple(){}
    //@}
};


} // namespace peaks
} // namespace pwiz


#endif // _FREQUENCYESTIMATORSIMPLE_HPP_


