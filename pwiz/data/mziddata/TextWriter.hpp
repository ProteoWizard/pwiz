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


#ifndef _MZIDDATA_TEXTWRITER_HPP_
#define _MZIDDATA_TEXTWRITER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MzIdentML.hpp"
#include "pwiz/data/msdata/CVParam.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <string>
#include <vector>


namespace pwiz {
namespace mziddata {

using msdata::CVParam;
using msdata::UserParam;

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


    TextWriter& operator()(const MzIdentML& mzid)
    {
        /*
        (*this)("mzid:");
        if (!mzid.version.empty())
            child()("version: " + mzid.version);
        if (!mzid.cvs.empty())
            child()("cvList: ", mzid.cvs);
        if (!mzid.analysisSoftwareList.empty())
            child()("analysisSoftwareList: ", mzid.analysisSoftwareList);
//        if (!mzid.provider.empty())
//            child()("provider: ", mzid.provider);
        if (!mzid.auditCollection.empty())
            child()("auditCollection: ", mzid.auditCollection);
//        if (!mzid.analysisSampleCollection.empty())
//            child()("analysisSampleCollection: ", mzid.analysisSampleCollection);
        if (!mzid.referenceableCollection.empty())
            child()("referenceableCollection: ", mzid.referenceableCollection);
        if (!mzid.sequenceCollection.empty())
            child()("sequenceCollection: ", mzid.sequenceCollection);
//        if (!mzid.analysisCollection.empty())
            child()("analysisCollection: ", mzid.analysisCollection);
        if (!mzid.dataCollection.empty())
            child()("dataCollection: ", mzid.dataCollection);
//        if (!mzid.BibliographicReference_ref.empty())
//            child()("BibliographicReference_ref: ", mzid.BibliographicReference_ref);
*/
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

	
} // namespace mziddata
} // namespace pwiz


#endif // _MZIDDATA_TEXTWRITER_HPP_

