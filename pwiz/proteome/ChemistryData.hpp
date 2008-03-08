//
// ChemistryData.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _CHEMISTRYDATA_HPP_
#define _CHEMISTRYDATA_HPP_


#include "Chemistry.hpp"


namespace pwiz {
namespace proteome {
namespace ChemistryData {


typedef pwiz::proteome::Chemistry::Element::Type Type;


struct Isotope 
{
    double mass; 
    double abundance;
};


struct Element 
{
    Type type;
    const char* symbol;
    int atomicNumber;
    double atomicWeight;
    Isotope* isotopes;
    int isotopesSize;
};


Element* elements();
int elementsSize();


} // namespace ChemistryData
} // namespace proteome
} // namespace pwiz


#endif // _CHEMISTRYDATA_HPP_

