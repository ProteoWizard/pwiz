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


#ifndef _CHEMISTRY_HPP_
#define _CHEMISTRY_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <iosfwd>
#include <string>
#include <vector>
#include "pwiz/utility/misc/virtual_map.hpp"
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace chemistry {


/// the mass of a proton in unified atomic mass units
const double Proton   = 1.00727646688;

/// the mass of a neutron in unified atomic mass units
const double Neutron  = 1.00866491560;

/// the mass of an electron in unified atomic mass units
const double Electron = 0.00054857991;


/// struct for holding isotope information
struct PWIZ_API_DECL MassAbundance
{
    double mass;
    double abundance;

    MassAbundance(double m = 0, double a = 0)
    :   mass(m), abundance(a)
    {}

    bool operator==(const MassAbundance& that) const;
    bool operator!=(const MassAbundance& that) const;
};


/// struct for holding isotope distribution
typedef std::vector<MassAbundance> MassDistribution;


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MassAbundance& ma);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MassDistribution& md);

 
/// scope for declarations related to elements
namespace Element {


/// enumeration of the elements
enum PWIZ_API_DECL Type
{
    C, H, O, N, S, P, _13C, _2H, _18O, _15N, // Order matters: _15N is the end of the CHONSP entries
    He, Li, Be, B, F, Ne, 
    Na, Mg, Al, Si, Cl, Ar, K, Ca, 
    Sc, Ti, V, Cr, Mn, Fe, Co, Ni, Cu, Zn, 
    Ga, Ge, As, Se, Br, Kr, Rb, Sr, Y, Zr, 
    Nb, Mo, Tc, Ru, Rh, Pd, Ag, Cd, In, Sn, 
    Sb, Te, I, Xe, Cs, Ba, La, Ce, Pr, Nd, 
    Pm, Sm, Eu, Gd, Tb, Dy, Ho, Er, Tm, Yb, 
    Lu, Hf, Ta, W, Re, Os, Ir, Pt, Au, Hg, 
    Tl, Pb, Bi, Po, At, Rn, Fr, Ra, Ac, Th, 
    Pa, U, Np, Pu, Am, Cm, Bk, Cf, Es, Fm, 
    Md, No, Lr, Rf, Db, Sg, Bh, Hs, Mt, Uun, 
    Uuu, Uub, Uuq, Uuh, _3H
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, Type type);


/// class for obtaining information about elements
namespace Info
{


struct PWIZ_API_DECL Record
{
    Type type;
    std::string symbol;
    int atomicNumber;
    double atomicWeight;
    MassAbundance monoisotope; /// the most abundant isotope
    MassDistribution isotopes;
};

/// retrieve the record for an element
PWIZ_API_DECL const Record& record(Type type);

/// retrieve the record for an element
PWIZ_API_DECL const Record& record(const std::string& symbol);


} // namespace Info


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Info::Record& record);


} // namespace Element


class CompositionMap;

/// class to represent a chemical formula
class PWIZ_API_DECL Formula
{
    public:

    /// formula string given by symbol/count pairs, e.g. water: "H2 O1" (whitespace optional)
    Formula(const std::string& formula = "");
    Formula(const char* formula);
    Formula(const Formula& formula);
    const Formula& operator=(const Formula& formula);
    ~Formula();

    double monoisotopicMass() const;
    double molecularWeight() const;
    std::string formula() const;

    /// access to the Element's count in the formula
    int operator[](Element::Type e) const;
    int& operator[](Element::Type e);

    // direct access to the map, for iteration
    typedef std::map<Element::Type, int> Map;
    Map data() const;

    // operations
    Formula& operator+=(const Formula& that);    
    Formula& operator-=(const Formula& that);    
    Formula& operator*=(int scalar);

    /// formulas are equal iff their elemental compositions are equal
    bool operator==(const Formula& that) const;
    bool operator!=(const Formula& that) const;
    
    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
};


PWIZ_API_DECL Formula operator+(const Formula& a, const Formula& b);
PWIZ_API_DECL Formula operator-(const Formula& a, const Formula& b);
PWIZ_API_DECL Formula operator*(const Formula& a, int scalar);
PWIZ_API_DECL Formula operator*(int scalar, const Formula& a);


/// output a Formula
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Formula& formula);


} // namespace chemistry 
} // namespace pwiz


#endif // _CHEMISTRY_HPP_
