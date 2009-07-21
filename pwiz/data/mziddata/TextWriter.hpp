//
// TextWriter.hpp
//
//
// Original author: Robert Burke <robetr.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


    TextWriter& operator()(const std::string& label, const ParamContainer& paramContainer)
    {
        (*this)(label+": ");
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }
    
    TextWriter& operator()(const ParamContainer& paramContainer)
    {
        for_each(paramContainer.cvParams.begin(), paramContainer.cvParams.end(), *this);
        for_each(paramContainer.userParams.begin(), paramContainer.userParams.end(), *this);
        return *this;
    }


    TextWriter& operator()(const BibliographicReferencePtr& br)
    {
        if (!br.get())
            return *this;

        (*this)((IdentifiableType)*br);
        if (!br->authors.empty())
            child()("authors: "+br->authors);
        if (!br->publication.empty())
            child()("publication: "+br->publication);
        if (!br->publisher.empty())
            child()(br->publisher);
        if (!br->editor.empty())
            child()("editor: "+br->editor);
        std::ostringstream oss;
        oss << "year: " << br->year;
        child()(oss.str());
        if (!br->volume.empty())
            child()("volume: "+br->volume);
        if (!br->issue.empty())
            child()("issue: "+br->issue);
        if (!br->pages.empty())
            child()("pages: "+br->pages);
        if (!br->title.empty())
            child()("title: "+br->title);

        return *this;
    }
    
    TextWriter& operator()(const std::vector<BibliographicReferencePtr>& br)
    {
        (*this)("BibliographicReference ");
        if (!br.empty())
            for_each(br.begin(), br.end(), *this);
        return *this;
    }

    
    TextWriter& operator()(const SpectrumIdentificationProtocolPtr& sip)
    {
        (*this)("SpectrumIdentificationProtocol:");
        (*this)((IdentifiableType)*sip);
        if (!sip->AnalysisSoftware_ref.empty())
            child()("AnalysisSoftware_ref: "+sip->AnalysisSoftware_ref);
        if (!sip->searchType.empty())
            child()("searchType", sip->searchType);
        if (!sip->additionalSearchParams.empty())
            child()("additionalSearchParams", sip->additionalSearchParams);
        if (!sip->modificationParams.empty())
        {
            child()("modificationParams");
            for_each(sip->modificationParams.begin(),
                     sip->modificationParams.end(), child());
        }
        if (!sip->enzymes.empty())
            child()(sip->enzymes);
        if (!sip->massTable.empty())
            child()(sip->massTable);
        if (!sip->fragmentTolerance.empty())
            child()("fragmentTolerance", sip->fragmentTolerance);
        if (!sip->parentTolerance.empty())
            child()("parentTolerance", sip->parentTolerance);
        if (!sip->threshold.empty())
            child()("threshold", sip->threshold);
        if (!sip->databaseFilters.empty())
            child()("databaseFilters", sip->databaseFilters);

        return *this;
    }

    
    TextWriter& operator()(const DataCollection& dc)
    {
        (*this)("DataCollection: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const Filter& dc)
    {
        (*this)("Filter: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const SearchModification& dc)
    {
        (*this)("SearchModification: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const Enzymes& ez)
    {
        (*this)("Enzymes: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const MassTable& mt)
    {
        (*this)("MassTable: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const AnalysisProtocolCollection& apc)
    {
        (*this)("AnalysisProtocolCollection: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const AnalysisCollection& ac)
    {
        (*this)("AnalysisCollection: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const SequenceCollection& sc)
    {
        (*this)("SequenceCollection: ");
        // TODO finish
        return *this;
    }

    
    TextWriter& operator()(const Provider& provider)
    {
        (*this)("Provider: ");
        if (!provider.contactRole.empty())
            child()(provider.contactRole);
        return *this;
    }

    
    TextWriter& operator()(const Sample::Component& component)
    {
        (*this)("component: "+component.Sample_ref);

        return *this;
    }

    
    TextWriter& operator()(const SamplePtr& sample)
    {
        (*this)("samples: ");
        for_each(sample->components.begin(), sample->components.end(), *this);

        return *this;
    }

    
    TextWriter& operator()(const AnalysisSampleCollection& asc)
    {
        for_each(asc.samples.begin(), asc.samples.end(), *this);

        return *this;
    }

    
    TextWriter& operator()(const ContactRole& cr)
    {
        (*this)("ContactRole: ");
        if (!cr.Contact_ref.empty())
            child()("Contact_ref: "+cr.Contact_ref);
        if (!cr.role.empty())
            child()("role: ", cr.role);
        return (*this);
    }

    
    TextWriter& operator()(const AnalysisSoftwarePtr& asp)
    {
        (*this)("analysisSoftwareList:");
        (*this)((IdentifiableType)*asp);
        if (asp->version.empty())
            child()("version: "+asp->version);
        if (asp->contactRole.empty())
            child()(asp->contactRole);
        if (asp->softwareName.empty())
            child()("softwareName: ", asp->softwareName);
        if (asp->URI.empty())
            child()("URI: "+asp->URI);
        if (asp->customizations.empty())
            child()("customizations: "+asp->customizations);
        //for_each(window.cvParams.begin(), window.cvParams.end(), child());
        return *this;
    }
    
    TextWriter& operator()(const IdentifiableType& id)
    {
        if (!id.id.empty())
            child()("id: "+id.id);
        if (!id.name.empty())
            child()("name: "+id.name);

        return *this;
    }
    
    TextWriter& operator()(const MzIdentML& mzid)
    {
        (*this)("mzid:");
        child()((IdentifiableType)mzid);
        if (!mzid.version.empty())
            child()("version: " + mzid.version);
        if (!mzid.cvs.empty())
            child()("cvList: ", mzid.cvs);
        if (!mzid.analysisSoftwareList.empty())
            child()("analysisSoftwareList: ", mzid.analysisSoftwareList);
        if (!mzid.provider.empty())
            child()(mzid.provider);
        if (!mzid.auditCollection.empty())
            child()("auditCollection: ", mzid.auditCollection);
        if (!mzid.analysisSampleCollection.empty())
            child()(mzid.analysisSampleCollection);
        if (!mzid.sequenceCollection.empty())
            child()(mzid.sequenceCollection);
        if (!mzid.analysisCollection.empty())
            child()(mzid.analysisCollection);
        if (!mzid.analysisProtocolCollection.empty())
            child()(mzid.analysisProtocolCollection);
        if (!mzid.dataCollection.empty())
            child()(mzid.dataCollection);
        if (!mzid.bibliographicReference.empty())
            child()(mzid.bibliographicReference);
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

