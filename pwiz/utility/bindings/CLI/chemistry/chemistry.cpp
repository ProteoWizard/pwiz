//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "chemistry.hpp"
#include "pwiz/utility/misc/String.hpp"

using namespace System::Collections::Generic;
using namespace System;

namespace b = pwiz::chemistry;


namespace pwiz {
namespace CLI {
namespace chemistry {


MassAbundance::MassAbundance() : base_(new b::MassAbundance()) {}
MassAbundance::MassAbundance(double m, double a) : base_(new b::MassAbundance(m, a)) {}

IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(double, MassAbundance, mass);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_SET(double, MassAbundance, mass);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(double, MassAbundance, abundance);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_SET(double, MassAbundance, abundance);

bool MassAbundance::operator==(MassAbundance^ lhs, MassAbundance^ rhs)
{
    return lhs->base() == rhs->base();
}

bool MassAbundance::operator!=(MassAbundance^ lhs, MassAbundance^ rhs)
{
    return lhs->base() != rhs->base();
}


IMPLEMENT_ENUM_PROPERTY_GET(Element, ElementInfo::Record, type);
IMPLEMENT_STRING_PROPERTY_GET(ElementInfo::Record, symbol);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(int, ElementInfo::Record, atomicNumber);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(double, ElementInfo::Record, atomicWeight);
IMPLEMENT_REFERENCE_PROPERTY_GET(MassAbundance, ElementInfo::Record, monoisotope);
IMPLEMENT_REFERENCE_PROPERTY_GET(MassDistribution, ElementInfo::Record, isotopes);

ElementInfo::Record^ ElementInfo::record(Element element)
{
    return gcnew ElementInfo::Record(const_cast<b::Element::Info::Record*>(&b::Element::Info::record((b::Element::Type) element)), Proton::Mass);
}


Formula::Formula() : base_(new b::Formula()) {}
Formula::Formula(System::String^ formula) : base_(new b::Formula(ToStdString(formula))) {}
Formula::Formula(Formula^ other) : base_(new b::Formula(*other->base_)) {}

double Formula::monoisotopicMass() {return base().monoisotopicMass();}
double Formula::molecularWeight() {return base().molecularWeight();}
System::String^ Formula::formula() {return ToSystemString(base().formula());}

int Formula::Item::get(Element index)
{
    return base()[(b::Element::Type) index];
}

void Formula::Item::set(Element index, int value)
{
    base()[(b::Element::Type) index] = value;
}

System::String^ Formula::ToString()
{
    ostringstream oss;
    oss << base();
    return ToSystemString(oss.str());
}

IDictionary<Element, int>^ Formula::data()
{
    b::Formula::Map data = base().data();
    Dictionary<Element, int>^ result = gcnew Dictionary<Element, int>();
    for (b::Formula::Map::const_iterator itr = data.begin(); itr != data.end(); ++itr)
        result->Add((Element) itr->first, itr->second);
    return result;
}

Formula^ Formula::operator+=(Formula^ lhs, Formula^ rhs)
{
    lhs->base() += rhs->base();
    return lhs;
}

Formula^ Formula::operator-=(Formula^ lhs, Formula^ rhs)
{
    lhs->base() -= rhs->base();
    return lhs;
}

Formula^ Formula::operator*=(Formula^ lhs, int scalar)
{
    lhs->base() *= scalar;
    return lhs;
}

Formula^ Formula::operator+(Formula^ lhs, Formula^ rhs)
{
    Formula^ f = gcnew Formula(new b::Formula(lhs->base()));
    f->base() += rhs->base();
    return f;
}

Formula^ Formula::operator-(Formula^ lhs, Formula^ rhs)
{
    Formula^ f = gcnew Formula(new b::Formula(lhs->base()));
    f->base() -= rhs->base();
    return f;
}

Formula^ Formula::operator*(Formula^ lhs, int scalar)
{
    Formula^ f = gcnew Formula(new b::Formula(lhs->base()));
    f->base() *= scalar;
    return f;
}

Formula^ Formula::operator*(int scalar, Formula^ rhs)
{
    return rhs * scalar;
}

bool Formula::operator==(Formula^ lhs, Formula^ rhs)
{
    return lhs->base() == rhs->base();
}

bool Formula::operator!=(Formula^ lhs, Formula^ rhs)
{
    return lhs->base() != rhs->base();
}




MZTolerance::MZTolerance() : value(0), units(Units::MZ) {}
MZTolerance::MZTolerance(double value) : value(value), units(Units::MZ) {}
MZTolerance::MZTolerance(double value, Units units) : value(value), units(units) {}

MZTolerance::MZTolerance(System::String^ tolerance)
{
    b::MZTolerance tmp;
    istringstream iss(ToStdString(tolerance));
    iss >> tmp;
    value = tmp.value;
    units = (Units) tmp.units;
}

System::String^ MZTolerance::ToString()
{
    ostringstream oss;
    oss << b::MZTolerance(value, (b::MZTolerance::Units) units);
    return ToSystemString(oss.str());
}

double MZTolerance::operator+(double d, MZTolerance^ tolerance)
{   
    if (tolerance->units == MZTolerance::Units::MZ)
        return d + tolerance->value;
    else
        return d + Math::Abs(d) * tolerance->value * 1e-6;
}

double MZTolerance::operator-(double d, MZTolerance^ tolerance)
{   
    if (tolerance->units == MZTolerance::Units::MZ)
        return d - tolerance->value;
    else
        return d - Math::Abs(d) * tolerance->value * 1e-6;
}

bool MZTolerance::operator==(MZTolerance^ lhs, MZTolerance^ rhs)
{
    if (ReferenceEquals(lhs, nullptr) && ReferenceEquals(rhs, nullptr))
        return true;
    if (ReferenceEquals(lhs, nullptr) || ReferenceEquals(rhs, nullptr))
        return false;
    return lhs->value == rhs->value && lhs->units == rhs->units;
}

bool MZTolerance::operator!=(MZTolerance^ lhs, MZTolerance^ rhs)
{
    if (ReferenceEquals(lhs, nullptr) && ReferenceEquals(rhs, nullptr))
        return false;
    if (ReferenceEquals(lhs, nullptr) || ReferenceEquals(rhs, nullptr))
        return true;
    return lhs->value != rhs->value || lhs->units != rhs->units;
}


} // namespace chemistry
} // namespace CLI
} // namespace pwiz

