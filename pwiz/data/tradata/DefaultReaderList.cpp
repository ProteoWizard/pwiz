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


#define PWIZ_SOURCE

#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "DefaultReaderList.hpp"
#include "Serializer_traML.hpp"
#include "References.hpp"
#include "pwiz/data/tradata/Version.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"


namespace bxp = boost::xpressive;


namespace pwiz {
namespace tradata {


namespace {

SoftwarePtr getSoftwarePwiz(vector<SoftwarePtr>& softwarePtrs)
{
    string version = pwiz::tradata::Version::str();

    for (vector<SoftwarePtr>::const_iterator it=softwarePtrs.begin(); it!=softwarePtrs.end(); ++it)
        if ((*it)->hasCVParam(MS_pwiz) && (*it)->version==version)
            return *it;

    SoftwarePtr sp(new Software);
    sp->id = "pwiz_" + version;
    sp->set(MS_pwiz);
    sp->version = version;
    softwarePtrs.push_back(sp);
    return sp;
}

void fillInCommonMetadata(const string& filename, TraData& td)
{
    td.cvs = defaultCVList();

    SoftwarePtr softwarePwiz = getSoftwarePwiz(td.softwarePtrs);
}

class Reader_traML : public Reader
{
    public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head);
		 return std::string((type(iss) != Type_Unknown)?getType():"");
    }

    virtual void read(const std::string& filename, const std::string& head, TraData& result, int documentIndex = 0) const
    {
        if (documentIndex != 0)
            throw ReaderFail("[Reader_traML::read] multiple documents not supported");

		shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[Reader_traML::read] Unable to open file " + filename).c_str());

       switch (type(*is))
        {
            case Type_traML:
            {
                Serializer_traML serializer;
                serializer.read(is, result);
                break;
            }
            case Type_Unknown:
            default:
            {
                throw runtime_error("[Reader_traML::read] This isn't happening.");
            }
        }

       fillInCommonMetadata(filename, result);
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<TraDataPtr>& results) const
    {
        results.push_back(TraDataPtr(new TraData));
        read(filename, head, *results.back());
    }

	virtual const char *getType() const {return "TraML";}

    private:

    enum Type { Type_traML, Type_Unknown };

    Type type(istream& is) const
    {
        try
        {
            string rootElement = minimxml::xml_root_element(is);
            if (rootElement == "TraML")
                return Type_traML;
        }
        catch (runtime_error&)
        {
        }
        return Type_Unknown;
    }
};


} // namespace


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList()
{
    push_back(ReaderPtr(new Reader_traML));
}


} // namespace tradata
} // namespace pwiz
