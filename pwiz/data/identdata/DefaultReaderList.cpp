//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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


#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "DefaultReaderList.hpp"
#include "Serializer_mzid.hpp"
//#include "Serializer_idpDB.hpp"
#include "Serializer_pepXML.hpp"
#include "Serializer_protXML.hpp"
#ifndef NO_MASCOT_READER
#include "MascotReader.hpp"
#endif
//#include "References.hpp"
#include "pwiz/data/identdata/Version.hpp"


using namespace pwiz::util;
using namespace pwiz::minimxml;


namespace pwiz {
namespace identdata {


namespace {

AnalysisSoftwarePtr getPwizSoftware(IdentData& mzid)
{
    string version = pwiz::identdata::Version::str();

    AnalysisSoftwarePtr result;

    BOOST_FOREACH(const AnalysisSoftwarePtr& softwarePtr, mzid.analysisSoftwareList)
        if (softwarePtr->softwareName.hasCVParam(MS_pwiz) && softwarePtr->version==version)
        {
            result = softwarePtr;
            if (result->contactRolePtr.get() && result->contactRolePtr->contactPtr.get())
                return result;
        }

    ContactPtr contactPwiz;
    BOOST_FOREACH(const ContactPtr& contactPtr, mzid.auditCollection)
        if (contactPtr->name == "ProteoWizard")
            contactPwiz = contactPtr;

    if (!contactPwiz.get())
    {
        contactPwiz.reset(new Organization("ORG_PWIZ", "ProteoWizard"));
        contactPwiz->set(MS_contact_email, "support@proteowizard.org");
        mzid.auditCollection.push_back(contactPwiz);
    }

    if (!result.get())
    {
        result.reset(new AnalysisSoftware("pwiz_" + version, "ProteoWizard MzIdentML"));
        result->softwareName.set(MS_pwiz);
        result->version = version;
        mzid.analysisSoftwareList.push_back(result);
    }

    if (!result->contactRolePtr.get())
    {
        result->contactRolePtr.reset(new ContactRole);
        result->contactRolePtr->cvid = MS_software_vendor;
    }
    result->contactRolePtr->contactPtr = contactPwiz;

    return result;
}

void fillInCommonMetadata(const string& filename, IdentData& mzid)
{
    mzid.cvs = defaultCVList();

    // add pwiz software and contact metadata
    AnalysisSoftwarePtr softwarePwiz = getPwizSoftware(mzid);
}

class Reader_mzid : public Reader
{
    public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        // mzIdentML 1.0 root is "mzIdentML", 1.1 root is "MzIdentML"
        string result;
        try {
            result = string(bal::iequals(xml_root_element(head), "MzIdentML") ? getType() : "");
        } catch(...) {
        }
        return result;
    }

    virtual void read(const std::string& filename, const std::string& head, IdentDataPtr& result, const Config& config) const
    {
        if (!result.get())
            throw ReaderFail("[Reader_mzid::read] NULL valued IdentDataPtr passed in.");
        return read(filename, head, *result, config);
    }
    
    virtual void read(const std::string& filename, const std::string& head, IdentData& result, const Config& config) const
    {
        shared_ptr<istream> is(new random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[Reader_mzid::read] Unable to open file " + filename).c_str());

        Serializer_mzIdentML::Config serializerConfig;
        serializerConfig.readSequenceCollection = !config.ignoreSequenceCollectionAndAnalysisData;
        serializerConfig.readAnalysisData = !config.ignoreSequenceCollectionAndAnalysisData;
        Serializer_mzIdentML serializer(serializerConfig);
        serializer.read(is, result, config.iterationListenerRegistry);

        fillInCommonMetadata(filename, result);
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config) const
    {
        results.push_back(IdentDataPtr(new IdentData));
        read(filename, head, *results.back(), config);
    }

    virtual const char *getType() const {return "mzIdentML";}
};


class Reader_pepXML : public Reader
{
public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        string result;
        try {
            result = string(xml_root_element(head) == "msms_pipeline_analysis" ? getType() : "");
        } catch(...) {
        }
        return result;
    }

    virtual void read(const std::string& filename, const std::string& head, IdentData& result, const Config& config) const
    {
        shared_ptr<istream> is(new random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error("[Reader_pepXML::read] Unable to open file " + filename);

        Serializer_pepXML serializer(Serializer_pepXML::Config(!config.ignoreSequenceCollectionAndAnalysisData));
        serializer.read(is, result, config.iterationListenerRegistry);
        fillInCommonMetadata(filename, result);
    }

    virtual void read(const std::string& filename, const std::string& head, IdentDataPtr& result, const Config& config) const
    {
        if (!result.get())
            throw ReaderFail("[Reader_pepXML::read] NULL valued IdentDataPtr passed in.");
        return read(filename, head, *result, config);
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config) const
    {
        results.push_back(IdentDataPtr(new IdentData));
        read(filename, head, *results.back(), config);
    }

    virtual const char *getType() const {return "pepXML";}
};


class Reader_protXML : public Reader
{
    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        string result;
        try {
            result = string(xml_root_element(head) == "protein_summary" ? getType() : "");
        } catch(...) {
        }
        return result;
    }

    virtual void read(const std::string& filename, const std::string& head, IdentData& result, const Config& config) const
    {
        shared_ptr<istream> is(new random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error("[Reader_protXML::read] Unable to open file " + filename);

        Serializer_protXML serializer(Serializer_protXML::Config(!config.ignoreSequenceCollectionAndAnalysisData));
        if (result.empty())
        {
            // no preexisting info - see if we can read corresponding pepXML
            // data referred to in the protXML file
            std::vector<std::string> sourceFiles;
            serializer.read(is, result, &sourceFiles, NULL);
            // now sourceFiles[0] should contain protXML file's original name
            // and sourceFiles[1] should contain source pepXML file's original name
            if (2==sourceFiles.size())
            {
                // likely that files are not in the original locations - let's
                // assume (actually require) that they moved together
                std::string srcFile(boost::replace_all_copy(filename,"\\","/"));
                std::string protFile(boost::replace_all_copy(sourceFiles[0],"\\","/"));
                std::string pepFile(boost::replace_all_copy(sourceFiles[1],"\\","/"));
                // find common trailing part of actual and declared protxml filenames
                int l,r=0;
                for (l=0;l < (int)srcFile.length();l++)
                {
                    for (r=0;r < (int)protFile.length();r++)
                    {
                        if (srcFile.substr(l)==protFile.substr(r))
                        {
                            break;
                        }
                    }
                    if (r < (int)protFile.length())
                    {
                        break;
                    }
                }
                // find common leading part of declared protxml and pepxml filenames
                int ll;
                for (ll=0;ll<(int)pepFile.length();ll++)
                {
                    if (pepFile.substr(0,ll)!=protFile.substr(0,ll))
                    {
                        // retreat to path sep
                        while (ll && (pepFile[ll] != '/'))
                            ll--;
                        break;
                    }
                }
                // now try to populate the idendata's peptides
                std::string pepx = srcFile.substr(0,l)+pepFile.substr(ll);
                try
                {
                    std::string head;
                    Reader_pepXML reader;
                    reader.read(pepx, head, result, config);
                }
                catch(...) 
                {
                    // not a crisis, much peptide info can be read from protXML itself
                }
            }
        }
        // and proceed with protXML reading
        serializer.read(is, result, NULL, config.iterationListenerRegistry);
        fillInCommonMetadata(filename, result);
    }

    virtual void read(const std::string& filename, const std::string& head, IdentDataPtr& result, const Config& config) const
    {
        if (!result.get())
            throw ReaderFail("[Reader_protXML::read] NULL valued IdentDataPtr passed in.");
        return read(filename, head, *result, config);
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config) const
    {
        results.push_back(IdentDataPtr(new IdentData));
        read(filename, head, *results.back(), config);
    }

    virtual const char *getType() const {return "protXML";}
};


/*class Reader_idpDB : public Reader
{
    public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        string result;
        try {result = string((bal::iends_with(filename, ".idpDB") && bal::starts_with(head, "SQLite")) ? getType() : "");}
        catch(...) {}
        return result;
    }

    virtual void read(const std::string& filename, const std::string& head, IdentDataPtr& result, const Config& config) const
    {
        if (!result.get())
            throw ReaderFail("[Reader_idpDB::read] NULL valued IdentDataPtr passed in.");
        return read(filename, head, *result, config);
    }
    
    virtual void read(const std::string& filename, const std::string& head, IdentData& result, const Config& config) const
    {
        Serializer_idpDB::Config serializerConfig;
        serializerConfig.readSequenceCollection = !config.ignoreSequenceCollectionAndAnalysisData;
        serializerConfig.readAnalysisData = !config.ignoreSequenceCollectionAndAnalysisData;
        Serializer_idpDB serializer(serializerConfig);
        serializer.read(filename, result, 0, config.iterationListenerRegistry);

        fillInCommonMetadata(filename, result);
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results,
                      const Config& config) const
    {
        Serializer_idpDB::Config serializerConfig;
        serializerConfig.readSequenceCollection = !config.ignoreSequenceCollectionAndAnalysisData;
        serializerConfig.readAnalysisData = !config.ignoreSequenceCollectionAndAnalysisData;
        Serializer_idpDB serializer(serializerConfig);

        vector<string> analysisNames;
        serializer.readIds(filename, analysisNames);

        for (size_t i=0; i < analysisNames.size(); ++i)
        {
            results.push_back(IdentDataPtr(new IdentData));
            results.back()->id = analysisNames[i];
            serializer.read(filename, *results.back(), i, config.iterationListenerRegistry);
        }
    }

    virtual const char *getType() const {return "idpDB";}
};*/


} // namespace


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList()
{
    push_back(ReaderPtr(new Reader_mzid));
    push_back(ReaderPtr(new Reader_pepXML));
    push_back(ReaderPtr(new Reader_protXML));
#ifndef NO_MASCOT_READER
    push_back(ReaderPtr(new MascotReader));
#endif
    //push_back(ReaderPtr(new Reader_idpDB));
}


} // namespace identdata
} // namespace pwiz
