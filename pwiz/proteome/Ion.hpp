//
// Ion.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _ION_HPP_
#define _ION_HPP_


namespace pwiz {
namespace proteome {


namespace Ion
{
    const double protonMass_ = 1.00727647;

    inline double mz(double neutralMass, int charge)
    {
        return neutralMass/charge + protonMass_;
    } 

    inline double neutralMass(double mz, int charge)
    {
        return (mz - protonMass_)*charge; 
    }

    inline double ionMass(double neutralMass, int charge)
    {
        return neutralMass + protonMass_*charge;
    }
}


} // namespace proteome 
} // namespace pwiz


#endif // _ION_HPP_

