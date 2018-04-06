//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TRADATA_TEXTWRITER_HPP_
#define _TRADATA_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace tradata {


using boost::lexical_cast;
using std::string;


class PWIZ_API_DECL TextWriter
{
    public:

    TextWriter(std::ostream& os, int depth = 0)
    :   os_(os), depth_(depth), indent_(depth*2, ' ')
    {}

    TextWriter child() {return TextWriter(os_, depth_+1);}

    TextWriter& operator()(const std::string& text)
    {
        os_ << indent_ << text << std::endl;
        return *this;
    }

    TextWriter& operator()(const CVParam& cvParam)
    {
        os_ << indent_ << "cvParam: " << cvTermInfo(cvParam.cvid).name;
        if (!cvParam.value.empty())
            os_ << ", " << cvParam.value;
        if (cvParam.units != CVID_Unknown)
            os_ << ", " << cvParam.unitsName();
        os_ << std::endl; 
        return *this;    
    }

    TextWriter& operator()(const UserParam& userParam)
    {
        os_ << indent_ << "userParam: " << userParam.name;
        if (!userParam.value.empty()) os_ << ", " << userParam.value; 
        if (!userParam.type.empty()) os_ << ", " << userParam.type; 
        if (userParam.units != CVID_Unknown) os_ << ", " << cvTermInfo(userParam.units).name;
        os_ << std::endl; 
        return *this;    
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const std::vector<object_type>& v)
    {
        (*this)(label);
        for_each(v.begin(), v.end(), child());
        return *this;
    }

    template<typename object_type>
    TextWriter& operator()(const std::string& label, const object_type& v)
    {
        (*this)(label)(boost::lexical_cast<std::string>(v));
        return *this;
    }


    TextWriter& operator()(const TraData& msd)
    {
        (*this)("tradata:");
        child()("version: " + msd.version());
        if (!msd.cvs.empty())
            child()("cvList: ", msd.cvs);
        if (!msd.contactPtrs.empty())
            child()("contactList: ", msd.contactPtrs);
        if (!msd.publications.empty())
            child()("publicationList: ", msd.publications);
        if (!msd.instrumentPtrs.empty())
            child()("instrumentList: ", msd.instrumentPtrs);
        if (!msd.softwarePtrs.empty())
            child()("softwareList: ", msd.softwarePtrs);
        if (!msd.proteinPtrs.empty())
            child()("proteinList: ", msd.proteinPtrs);
        if (!msd.peptidePtrs.empty())
            child()("peptideList: ", msd.peptidePtrs);
        if (!msd.compoundPtrs.empty())
            child()("compoundList: ", msd.compoundPtrs);
        if (!msd.transitions.empty())
            child()("transitionList: ", msd.transitions);
        if (!msd.targets.empty())
            child()(msd.targets);

        return *this;
    }
    
    TextWriter& operator()(const CV& cv)
    {
        (*this)("cv:");
        child()
            ("id: " + cv.id)
            ("fullName: " + cv.fullName)
            ("version: " + cv.version)
            ("URI: " + cv.URI);
        return *this;
    }

    TextWriter& operator()(const ParamContainer& paramContainer)
    {
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }

    TextWriter& operator()(const Publication& publication)
    {
        (*this)("publication:");
        child()
            ("id: " + publication.id)
            (static_cast<const ParamContainer&>(publication));
        return *this;
    }

    TextWriter& operator()(const Software& software)
    {
        (*this)("software:");
        child()
            ("id: " + software.id)
            ("version: " + software.version)
            (static_cast<const ParamContainer&>(software));
        return *this;
    }

    TextWriter& operator()(const Contact& contact)
    {
        (*this)("contact:");
        child()(static_cast<const ParamContainer&>(contact));
        return *this;
    }

    TextWriter& operator()(const RetentionTime& retentionTime)
    {
        (*this)("retentionTime:");
        child()(static_cast<const ParamContainer&>(retentionTime));
        if (retentionTime.softwarePtr.get() &&
            !retentionTime.softwarePtr->empty())
            child()("softwareRef: " + retentionTime.softwarePtr->id);
        return *this;    
    }

    TextWriter& operator()(const Prediction& prediction)
    {
        (*this)("prediction:");
        child()(static_cast<const ParamContainer&>(prediction));
        return *this;   
    }

    TextWriter& operator()(const Evidence& evidence)
    {
        (*this)("evidence:");
        child()(static_cast<const ParamContainer&>(evidence));
        return *this;
    }

    TextWriter& operator()(const Validation& validation)
    {
        (*this)("validation:");
        child()(static_cast<const ParamContainer&>(validation));
        return *this;
    }

    TextWriter& operator()(const Protein& protein)
    {
        (*this)("protein:");
        child()("id: " + protein.id)
               ("sequence: " + protein.sequence);
        child()(static_cast<const ParamContainer&>(protein));
        return *this;
    }

    TextWriter& operator()(const Modification& modification)
    {
        (*this)("modification:");
        child()("location: ", lexical_cast<string>(modification.location))
               ("monoisotopicMassDelta: " + lexical_cast<string>(modification.monoisotopicMassDelta))
               ("averageMassDelta: " + lexical_cast<string>(modification.averageMassDelta));
        child()(static_cast<const ParamContainer&>(modification));
        return *this;
    }

    TextWriter& operator()(const Peptide& peptide)
    {
        (*this)("peptide:");
        child()("id: " + peptide.id)
               ("sequence: " + peptide.sequence)
               (peptide.evidence);

        if (!peptide.proteinPtrs.empty())
            child()("proteinRefs:", peptide.proteinPtrs);
        if (!peptide.modifications.empty())
            child()("modifications:", peptide.modifications);
        if (!peptide.retentionTimes.empty())
            child()("retentionTimes:", peptide.retentionTimes);

        child()(static_cast<const ParamContainer&>(peptide));
        return *this;
    }

    TextWriter& operator()(const Compound& compound)
    {
        (*this)("compound:");
        child()("id: " + compound.id)
               ("retentionTimes:", compound.retentionTimes);
        child()(static_cast<const ParamContainer&>(compound));
        return *this;
    }

    TextWriter& operator()(const Precursor& precursor)
    {
        (*this)("precursor:");
        child()(static_cast<const ParamContainer&>(precursor));
        return *this;
    }

    TextWriter& operator()(const Product& product)
    {
        (*this)("product:");
        child()(static_cast<const ParamContainer&>(product));
        return *this;
    }

    TextWriter& operator()(const Transition& transition)
    {
        (*this)("transition:");
        child()("id: ", transition.id);
            if (!transition.precursor.empty())
                child()(transition.precursor);
            if (!transition.product.empty())
                child()(transition.product);
            if (!transition.prediction.empty())
                child()(transition.prediction);
            if (!transition.interpretationList.empty())
                child()("interpretationList: ", transition.interpretationList);
            if (!transition.configurationList.empty())
                child()("configurationList: ", transition.configurationList);
            if (transition.peptidePtr.get() && !transition.peptidePtr->empty())
                child()("peptideRef: " + transition.peptidePtr->id);
            if (transition.compoundPtr.get() && !transition.compoundPtr->empty())
                child()("compoundRef: " + transition.compoundPtr->id);
        return *this;
    }

    TextWriter& operator()(const Target& target)
    {
        (*this)("target:");
        child()("id: ", target.id);
            if (!target.precursor.empty())
                child()(target.precursor);
            if (!target.configurationList.empty())
                child()("configurationList: ", target.configurationList);
            if (target.peptidePtr.get() && !target.peptidePtr->empty())
                child()("peptideRef: " + target.peptidePtr->id);
            if (target.compoundPtr.get() && !target.compoundPtr->empty())
                child()("compoundRef: " + target.compoundPtr->id);
        return *this;
    }

    TextWriter& operator()(const TargetList& targetList)
    {
        (*this)("targetList:");
        child()(static_cast<const ParamContainer&>(targetList));
            if (!targetList.targetExcludeList.empty())
                child()("targetExcludeList: ", targetList.targetExcludeList);
            if (!targetList.targetIncludeList.empty())
                child()("targetIncludeList: ", targetList.targetIncludeList);
        return *this;
    }

    // if no other overload matches, assume the object is a shared_ptr of a valid overloaded type
    template<typename object_type>
    TextWriter& operator()(const boost::shared_ptr<object_type>& p)
    {
        return p.get() ? (*this)(*p) : *this;
    }

    private:
    std::ostream& os_;
    int depth_;
    std::string indent_;
};

	
} // namespace tradata
} // namespace pwiz


#endif // _TRADATA_TEXTWRITER_HPP_

