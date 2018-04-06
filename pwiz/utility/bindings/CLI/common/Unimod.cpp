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

#include "Unimod.hpp"

using namespace System::Collections::Generic;

namespace b = pwiz::data::unimod;

namespace pwiz {
namespace data {
namespace unimod {
            
// needed for std::find (used by DEFINE_STD_VECTOR_WRAPPER)
bool operator== (const b::Modification& lhs, const b::Modification& rhs) {return lhs.cvid == rhs.cvid;}
bool operator== (const b::Modification::Specificity& lhs, const b::Modification::Specificity& rhs)
{
    return lhs.site == rhs.site &&
           lhs.position == rhs.position &&
           lhs.hidden == rhs.hidden &&
           lhs.classification == rhs.classification;
}

} // namespace unimod
} // namespace data


namespace CLI {
namespace data {


using System::String;
using System::Nullable;
using System::Collections::Generic::IList;
using namespace CLI::cv;
using namespace CLI::chemistry;
using namespace boost::logic;


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ModificationList, pwiz::data::unimod::Modification, unimod::Modification, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SpecificityList, pwiz::data::unimod::Modification::Specificity, unimod::Modification::Specificity, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


unimod::Site unimod::Modification::Specificity::site::get() {return (unimod::Site) base().site.value();}
unimod::Position unimod::Modification::Specificity::position::get() {return (unimod::Position) base().position.value();}
unimod::Classification unimod::Modification::Specificity::classification::get() {return (unimod::Classification) base().classification.value();}
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(bool, unimod::Modification::Specificity, hidden);


IMPLEMENT_ENUM_PROPERTY_GET(CVID, unimod::Modification, cvid);
IMPLEMENT_STRING_PROPERTY_GET(unimod::Modification, name);
IMPLEMENT_REFERENCE_PROPERTY_GET(Formula, unimod::Modification, deltaComposition);
IMPLEMENT_SIMPLE_PRIMITIVE_PROPERTY_GET(bool, unimod::Modification, approved);
double unimod::Modification::deltaMonoisotopicMass::get() {return base().deltaMonoisotopicMass();}
double unimod::Modification::deltaAverageMass::get() {return base().deltaAverageMass();}
IList<unimod::Modification::Specificity^>^ unimod::Modification::specificities::get() {return gcnew SpecificityList(&base().specificities, this);}


unimod::Site unimod::site(System::Char symbol) {try {return (Site) b::site((char) symbol).value();} CATCH_AND_FORWARD}
unimod::Position unimod::position(CVID cvid) {try {return (Position) b::position((pwiz::cv::CVID) cvid).value();} CATCH_AND_FORWARD}

IList<unimod::Modification^>^ unimod::modifications()
{
    try {return gcnew ModificationList(const_cast<std::vector<b::Modification>*>(&b::modifications()), gcnew System::Object());} CATCH_AND_FORWARD
}

unimod::Filter::Filter(double mass, double tolerance)
{
    this->mass = mass;
    this->tolerance = tolerance;
    monoisotopic = true;
    approved = true;
    site = unimod::Site::Any;
    position = unimod::Position::Anywhere;
    classification = unimod::Classification::Any;
}

IList<unimod::Modification^>^ unimod::modifications(Filter^ filter)
{
    try
    {
        if (filter == nullptr)
            return unimod::modifications();

        tribool nativeMonoisotopic(indeterminate), nativeApproved(indeterminate), nativeHidden(indeterminate);
        if (filter->monoisotopic.HasValue) nativeMonoisotopic = filter->monoisotopic.Value;
        if (filter->approved.HasValue) nativeApproved = filter->approved.Value;
        if (filter->hidden.HasValue) nativeHidden = filter->hidden.Value;

        b::Site nativeSite = b::Site::get_by_value((int) filter->site).get();
        b::Position nativePosition = b::Position::get_by_value((int) filter->position).get();
        b::Classification nativeClassification = b::Classification::get_by_value((int) filter->classification).get();

        return gcnew ModificationList(new std::vector<b::Modification>(
            b::modifications(filter->mass, filter->tolerance,
                             nativeMonoisotopic, nativeApproved,
                             nativeSite, nativePosition,
                             nativeClassification, nativeHidden)));
    }
    CATCH_AND_FORWARD
}

unimod::Modification^ unimod::modification(CVID cvid) {return gcnew Modification(new b::Modification(b::modification((pwiz::cv::CVID) cvid)));}
unimod::Modification^ unimod::modification(String^ title) {return gcnew Modification(new b::Modification(b::modification(ToStdString(title))));}


} // namespace data
} // namespace CLI
} // namespace pwiz
