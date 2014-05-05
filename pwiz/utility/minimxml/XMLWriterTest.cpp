//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "XMLWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::util;
using namespace pwiz::minimxml;


ostream* os_ = 0;


const char* targetXML = 
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<root name1=\"value1\" name2=\"420\" name3=\"0.666\">\n"
    "    <record name=\"nixon\"\n"
    "            color=\"red\"\n"
    "            number=\"37\">\n" 
    "        <quote>I'm not a crook.</quote>\n"
    "        <resigned/>\n"
    "    </record>\n"
    "    <record name=\"&quot;Penn &amp; Teller&quot;\">\n"
    "        <quote>'Bull&lt;shit!'</quote>\n"
    "    </record>\n"
    "    <record name=\"clinton\"\n"
    "            color=\"blue\"\n"
    "            number=\"42\">\n" 
    "        <quote>I did <em>not</em> have sexual relations with that woman.</quote>\n"
    "        <impeached/>\n"
    "    </record>\n"
    "    <record name=\"bush\"\n"
    "            color=\"red\"\n"
    "            number=\"43\">\n" 
    "        <quote>Mission accomplished.</quote>\n"
    "    </record>\n"
    "</root>\n";


struct TestOutputObserver : public XMLWriter::OutputObserver
{
    virtual void update(const string& output)
    {
        cache += output;
    }

    string cache; 
};


void test()
{
    ostringstream oss;

    TestOutputObserver outputObserver;
    XMLWriter::Config config;
    config.indentationStep = 4;
    config.outputObserver = &outputObserver;

    XMLWriter writer(oss, config);
    if (os_) *os_ << "target:\n" << targetXML << endl;

    unit_assert(writer.position() < 1); // 0 or -1 depending on platform 

    const char* piData = "version=\"1.0\" encoding=\"UTF-8\"";
    writer.processingInstruction("xml", piData);

    unit_assert(writer.position() == 39);

    XMLWriter::Attributes attributes;
    attributes.push_back(make_pair("name1", "value1"));
    attributes.push_back(make_pair("name2", "420"));
    attributes.push_back(make_pair("name3", "0.666"));
    writer.startElement("root", attributes);

    unit_assert(writer.position() == 87);
    unit_assert(writer.positionNext() == 91);

        attributes.clear();
        attributes.push_back(make_pair("name", "nixon"));
        attributes.push_back(make_pair("color", "red"));
        attributes.push_back(make_pair("number", "37"));
        writer.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
        writer.startElement("record", attributes);
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("quote");
            writer.characters("I'm not a crook.");
            writer.endElement();
            writer.startElement("resigned", XMLWriter::Attributes(), XMLWriter::EmptyElement);
            writer.popStyle();
        writer.endElement();
        writer.popStyle();

        attributes.clear();
        attributes.push_back(make_pair("name", "\"Penn & Teller\""));
        writer.startElement("record", attributes);
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("quote");
            writer.characters("'Bull<shit!'");
            writer.endElement();
            writer.popStyle();
        writer.endElement();

        attributes.clear();
        attributes.push_back(make_pair("name", "clinton"));
        attributes.push_back(make_pair("color", "blue"));
        attributes.push_back(make_pair("number", "42"));
        writer.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
        writer.startElement("record", attributes);
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("quote");
            writer.characters("I did ");
                writer.pushStyle(XMLWriter::StyleFlag_Inline);
                unit_assert(writer.position() == writer.positionNext()); // no indentation
                writer.startElement("em");
                    writer.characters("not");
                writer.endElement();
                writer.popStyle();
            writer.characters(" have sexual relations with that woman.");
            writer.endElement();
            writer.popStyle();
            writer.startElement("impeached", XMLWriter::Attributes(), XMLWriter::EmptyElement);
        writer.endElement();
        writer.popStyle();

        attributes.clear();
        attributes.push_back(make_pair("name", "bush"));
        attributes.push_back(make_pair("color", "red"));
        attributes.push_back(make_pair("number", "43"));
        writer.pushStyle(XMLWriter::StyleFlag_AttributesOnMultipleLines);
        writer.startElement("record", attributes);
            writer.pushStyle(XMLWriter::StyleFlag_InlineInner);
            writer.startElement("quote");
            writer.characters("Mission accomplished.");
            writer.endElement();
            writer.popStyle();
        writer.endElement();
        writer.popStyle();

    writer.endElement();

    if (os_) *os_ << "test: (" << oss.str().size() << ")\n" << oss.str() << endl;

    unit_assert(targetXML == oss.str());
    unit_assert(writer.position() == (int)oss.str().size());
    unit_assert(writer.position() == 671);

    if (os_) *os_ << "outputObserver cache:\n" << outputObserver.cache << endl;
    unit_assert(targetXML == outputObserver.cache);


    // test id encoding

    string id1("1invalid ID");
    unit_assert(encode_xml_id_copy(id1) == "_x0031_invalid_x0020_ID");
    unit_assert(&encode_xml_id(id1) == &id1);
    unit_assert(id1 == "_x0031_invalid_x0020_ID");

    string id2("_invalid-ID_#2_<3>");
    unit_assert(encode_xml_id_copy(id2) == "_invalid-ID__x0023_2__x003c_3_x003e_");
    unit_assert(encode_xml_id(id2) == "_invalid-ID__x0023_2__x003c_3_x003e_");

    string crazyId("!!!");
    unit_assert(encode_xml_id(crazyId) == "_x0021__x0021__x0021_");
}

void testNormalization()
{
#ifndef __APPLE__ // TODO: how to test that this works with Darwin's compiler?
    // test that subnormal values are clamped and provide 12 decimal places
    XMLWriter::Attributes attributes;
    attributes.add("1", 2.2250738585072014e-309);
    attributes.add("2", -2.2250738585072014e-309);
    unit_assert_operator_equal(2.225073858507e-308, lexical_cast<double>(attributes[0].second));
    unit_assert_operator_equal(-2.225073858507e-308, lexical_cast<double>(attributes[1].second));
#endif
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testNormalization();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}

