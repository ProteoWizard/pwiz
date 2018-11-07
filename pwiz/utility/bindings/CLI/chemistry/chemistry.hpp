//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _CHEMISTRY_HPP_CLI_
#define _CHEMISTRY_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )

//#pragma unmanaged
#include "pwiz/utility/chemistry/Chemistry.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include <sstream>
//#pragma managed

#include "../common/SharedCLI.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace chemistry {


/// <summary>the mass of a proton in unified atomic mass units</summary>
public ref struct Proton abstract sealed { const static double Mass = pwiz::chemistry::Proton; };

/// <summary>the mass of a neutron in unified atomic mass units</summary>
public ref struct Neutron abstract sealed { const static double Mass = pwiz::chemistry::Neutron; };

/// <summary>the mass of an electron in unified atomic mass units</summary>
public ref struct Electron abstract sealed { const static double Mass = pwiz::chemistry::Electron; };


/// <summary>struct for holding isotope information</summary>
public ref class MassAbundance
{
    DEFINE_INTERNAL_BASE_CODE(MassAbundance, pwiz::chemistry::MassAbundance);

    public:

    property double mass { double get(); void set(double value); }
    property double abundance { double get(); void set(double value); }

    MassAbundance();
    MassAbundance(double m, double a);

    static bool operator==(MassAbundance^ lhs, MassAbundance^ rhs);
    static bool operator!=(MassAbundance^ lhs, MassAbundance^ rhs);
};

/// <summary>struct for holding isotope distribution</summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MassDistribution, pwiz::chemistry::MassAbundance, MassAbundance, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>enumeration of the elements</summary>
public enum class Element
{
    C, H, O, N, S, P, _13C, _2H, _18O, _15N,  // Order matters: _15N is the end of the CHONSP entries
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


/// <summary>scope for obtaining information about elements</summary>
public ref struct ElementInfo abstract sealed {


/// <summary>struct for holding data for a single chemical element</summary>
ref class Record
{
    DEFINE_INTERNAL_BASE_CODE(Record, pwiz::chemistry::Element::Info::Record);

    public:

    property Element type { Element get(); }
    property System::String^ symbol { System::String^ get(); }
    property int atomicNumber { int get(); }
    property double atomicWeight { double get(); }
    property MassAbundance^ monoisotope { MassAbundance^ get(); } /// <summary>the most abundant isotope</summary>
    property MassDistribution^ isotopes { MassDistribution^ get(); }
};

/// <summary>retrieve the record for an element</summary>
static Record^ record(Element element);


}; // namespace ElementInfo


/// <summary>class to represent a chemical formula</summary>
public ref class Formula
{
    DEFINE_INTERNAL_BASE_CODE(Formula, pwiz::chemistry::Formula);

    public:

    Formula();

    /// <summary>formula string given by symbol/count pairs, e.g. water: "H2 O1" (whitespace optional)</summary>
    Formula(System::String^ formula);

    Formula(Formula^ other);

    double monoisotopicMass();
    double molecularWeight();
    System::String^ formula();

    /// <summary>access to the Element's count in the formula</summary>
    property int Item[Element]
    {
        int get(Element index);
        void set(Element index, int value);
    }

    virtual System::String^ ToString() override;

    /// <summary>direct access to the map, for iteration</summary>
    System::Collections::Generic::IDictionary<Element, int>^ data();

    // operators
    static Formula^ operator+=(Formula^ lhs, Formula^ rhs);
    static Formula^ operator-=(Formula^ lhs, Formula^ rhs);
    static Formula^ operator*=(Formula^ lhs, int scalar);
    static Formula^ operator+(Formula^ lhs, Formula^ rhs);
    static Formula^ operator-(Formula^ lhs, Formula^ rhs);
    static Formula^ operator*(Formula^ lhs, int scalar);
    static Formula^ operator*(int scalar, Formula^ rhs);

    /// <summary>formulas are equal iff their elemental compositions are equal</summary>
    static bool operator==(Formula^ lhs, Formula^ rhs);

    /// <summary>formulas are equal iff their elemental compositions are equal</summary>
    static bool operator!=(Formula^ lhs, Formula^ rhs);
};


/// <summary>struct for expressing m/z tolerance in either amu or ppm</summary>
public ref struct MZTolerance
{
    enum class Units {MZ, PPM};
    double value;
    Units units;

    MZTolerance();
    MZTolerance(double value);
    MZTolerance(double value, Units units);

    MZTolerance(System::String^ tolerance);
    virtual System::String^ ToString() override;

    // operators
    static double operator+(double d, MZTolerance^ tolerance);
    static double operator-(double d, MZTolerance^ tolerance);

    /// <summary>tolerances are equal iff their value and units are equal</summary>
    static bool operator==(MZTolerance^ lhs, MZTolerance^ rhs);

    /// <summary>tolerances are equal iff their value and units are equal</summary>
    static bool operator!=(MZTolerance^ lhs, MZTolerance^ rhs);
};


} // namespace chemistry
} // namespace CLI
} // namespace pwiz

#endif // _CHEMISTRY_HPP_CLI

