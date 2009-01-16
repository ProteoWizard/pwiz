//
// TransientGenerator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _TRANSIENTGENERATOR_HPP_
#define _TRANSIENTGENERATOR_HPP_


#include "Model.hpp"
#include "TransientData.hpp"
#include "pwiz/utility/proteome/IsotopeCalculator.hpp"
#include <memory>


namespace pwiz {
namespace id {


/// class for generating transient data from a model
class TransientGenerator
{
    public:

    TransientGenerator(const proteome::IsotopeCalculator& calculator);
    ~TransientGenerator();

    std::auto_ptr<pwiz::data::TransientData> 
        createTransientData(const model::ChromatographicFraction& cf);

    private:

    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace id 
} // namespace pwiz


#endif //  _TRANSIENTGENERATOR_HPP_ 

