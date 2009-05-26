//
// TextWriter.hpp
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
        if (!msd.version.empty())
            child()("version: " + msd.version);
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
        child()
            ("normalizationStandard: " + retentionTime.normalizationStandard)
            ("normalizedRetentionTime: ", retentionTime.normalizedRetentionTime)
            ("localRetentionTime: ", retentionTime.localRetentionTime)
            ("predictedRetentionTime: ", retentionTime.predictedRetentionTime)
            (static_cast<const ParamContainer&>(retentionTime));
        if (retentionTime.predictedRetentionTimeSoftwarePtr.get() &&
            !retentionTime.predictedRetentionTimeSoftwarePtr->empty())
            child()("predictedRetentionTimeSoftwareRef: " + retentionTime.predictedRetentionTimeSoftwarePtr->id);
        return *this;    
    }

    TextWriter& operator()(const Prediction& prediction)
    {
        (*this)("prediction:");
        child()
            ("recommendedTransitionRank: ", prediction.recommendedTransitionRank)
            ("transitionSource: " + prediction.transitionSource)
            ("relativeIntensity: ", prediction.relativeIntensity)
            ("intensityRank: ", prediction.intensityRank)
            (static_cast<const ParamContainer&>(prediction));
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
        child()
            ("recommendedTransitionRank: ", validation.recommendedTransitionRank)
            ("transitionSource: " + validation.transitionSource)
            ("relativeIntensity: ", validation.relativeIntensity)
            ("intensityRank: ", validation.intensityRank)
            (static_cast<const ParamContainer&>(validation));
        return *this;
    }

    TextWriter& operator()(const Precursor& precursor)
    {
        (*this)("precursor:");
        child()
            ("m/z: ", precursor.mz)
            ("charge: ", precursor.charge);
        return *this;
    }

    TextWriter& operator()(const Product& product)
    {
        (*this)("product:");
        child()
            ("m/z: ", product.mz)
            ("charge: ", product.charge);
        return *this;
    }

    TextWriter& operator()(const Transition& transition)
    {
        (*this)("transition:");
        child()
            ("name: ", transition.name)
            ("precursor: ", transition.precursor.mz)
            ("product: ", transition.product.mz);
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

