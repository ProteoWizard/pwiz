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


#include "pwiz/utility/misc/unit.hpp"
#include "SAXParser.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::minimxml::SAXParser;


ostream* os_;

// note: this tests single-quoted double quotes
const char* sampleXML = 
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<!DOCTYPE foo>\n"
    "<RootElement param=\"value\">\n"
    "    <FirstElement escaped_attribute=\"&quot;&lt;&amp;lt;&gt;&quot;\">\n"
    "        Some Text with Entity References: &lt;&amp;&gt;\n"
    "    </FirstElement>\n"
    "    <SecondElement param2=\"something\" param3=\"something.else 1234-56\">\n"
    "        Pre-Text <Inline>Inlined text with <![CDATA[<&\">]]></Inline> Post-text. <br/>\n"
    "    </SecondElement>\n"
    "    <prefix:ThirdElement goober:name=\"value\">\n"
    "    <!--this is a comment-->\n"
    "    <empty_with_space />\n"
    "    </prefix:ThirdElement>\n"
    "    <FifthElement leeloo='>Leeloo > mul-\"tipass'>\n"
    "        You're a monster, Zorg.>I know.\n"
    "    </FifthElement>\n"
    "</RootElement>\n"
    "<AnotherRoot>The quick brown fox jumps over the lazy dog.</AnotherRoot>\n";


//
// demo of event handling
//


struct PrintAttribute
{
    PrintAttribute(ostream& os) : os_(os) {}
    ostream& os_;

    void operator()(const Handler::Attributes::attribute &attr)
    {
        os_ << " (" << attr.getName() << "," << attr.getValue() << ")";
    }
};


class PrintEventHandler : public Handler
{
    public:

    PrintEventHandler(ostream& os)
    :   os_(os)
    {}

    virtual Status processingInstruction(const string& name,
                                         const string& value, 
                                         stream_offset position)
    {
        os_ << "[0x" << hex << position << "] processingInstruction: (" << name << "," << value << ")\n";
        return Status::Ok;
    };

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        os_ << "[0x" << hex << position << "] startElement: " << name;
        for_each(attributes.begin(), attributes.end(), PrintAttribute(os_));
        os_ << endl;
        return Status::Ok;
    };

    virtual Status endElement(const string& name, stream_offset position)
    {
        os_ << "[0x" << hex << position << "] endElement: " << name << endl;
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        os_ << "[0x" << hex << position << "] text: " << text << endl;
        return Status::Ok;
    }

    private:
    ostream& os_;
};


void demo()
{
    if (os_)
    {
        *os_ << "sampleXML:\n" << sampleXML << endl;

        istringstream is(sampleXML);
        PrintEventHandler handler(*os_);

        *os_ << "first parse events:\n";
        parse(is, handler); 
        *os_ << endl;
         
        *os_ << "second parse events:\n";
        parse(is, handler); 
        *os_ << endl;
    }
}


//
// C++ model of the sample XML
//


struct First
{
    string escaped_attribute;
    string text;
};


struct Second
{
    string param2;
    string param3;
    vector<string> text; 
};


struct Fifth
{
    string leeloo;
    string mr_zorg;
};


struct Root
{
    string param;
    First first;
    Second second;
    Fifth fifth;
};


//
//
// Handlers to connect XML to C++ model
//


void readAttribute(const Handler::Attributes& attributes, 
                   const string& attributeName, 
                   string& result)
{
    Handler::Attributes::attribute_list::const_iterator it = attributes.find(attributeName);
    if (it != attributes.end())
        result = it->getValue();
}


class FirstHandler : public Handler
{
    public:
    
    FirstHandler(First& first, bool autoUnescapeAttributes, bool autoUnescapeCharacters)
    :   object_(first)
    {
        parseCharacters = true;
        this->autoUnescapeAttributes = autoUnescapeAttributes;
        this->autoUnescapeCharacters = autoUnescapeCharacters;
    }

    virtual Status startElement(const string& name,
                                const Handler::Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "FirstElement")
            readAttribute(attributes, "escaped_attribute", object_.escaped_attribute);
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        unit_assert_operator_equal(158, position);
        object_.text = text.c_str();          
        return Status::Ok;
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        unit_assert_operator_equal(210, position);
        return Status::Ok;
    }

    private:
    First& object_;
};


class SecondHandler : public Handler
{
    public:

    SecondHandler(Second& object, bool autoUnescapeAttributes, bool autoUnescapeCharacters)
    :   object_(object)
    {
        parseCharacters = true;
        this->autoUnescapeAttributes = autoUnescapeAttributes;
        this->autoUnescapeCharacters = autoUnescapeCharacters;
    }

    virtual Status startElement(const string& name,
                                const Handler::Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "SecondElement")
        {
            readAttribute(attributes, "param2", object_.param2);
            readAttribute(attributes, "param3", object_.param3);
            // long as we're here, verify copyability of Handler::Attributes
            Handler::Attributes *copy1 = new Handler::Attributes(attributes);
            Handler::Attributes copy2(*copy1);
            delete copy1;
            std::string str;
            readAttribute(copy2, "param2", str);
            unit_assert(str==object_.param2);
        }
           
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        object_.text.push_back(text.c_str());          
        return Status::Ok;
    }

    private:
    Second& object_;
};


class FifthHandler : public Handler
{
    public:

    FifthHandler(Fifth& object, bool autoUnescapeAttributes, bool autoUnescapeCharacters)
    :   object_(object)
    {
        parseCharacters = true;
        this->autoUnescapeAttributes = autoUnescapeAttributes;
        this->autoUnescapeCharacters = autoUnescapeCharacters;
    }

    virtual Status startElement(const string& name,
                                const Handler::Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "FifthElement")
        {
            getAttribute(attributes, "leeloo", object_.leeloo);
        }
           
        return Status::Ok;
    }

    virtual Status characters(const SAXParser::saxstring& text, stream_offset position)
    {
        object_.mr_zorg = text.c_str();
        return Status::Ok;
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        unit_assert_operator_equal(625, position);
        return Status::Ok;
    }

    private:
    Fifth& object_;
};


class RootHandler : public Handler
{
    public:
    
    RootHandler(Root& root, bool autoUnescapeAttributes = true, bool autoUnescapeCharacters = true)
    :   object_(root), 
        firstHandler_(object_.first, autoUnescapeAttributes, autoUnescapeCharacters),
        secondHandler_(object_.second, autoUnescapeAttributes, autoUnescapeCharacters),
        fifthHandler_(object_.fifth, autoUnescapeAttributes, autoUnescapeCharacters)
    {
        parseCharacters = true;
        this->autoUnescapeAttributes = autoUnescapeAttributes;
        this->autoUnescapeCharacters = autoUnescapeCharacters;
    }

    virtual Status startElement(const string& name,
                                const Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "RootElement")
        {
            readAttribute(attributes, "param", object_.param);
            unit_assert_operator_equal(54, position);
        }
        else if (name == "FirstElement")
        {
            // delegate handling to a FirstHandler
            unit_assert_operator_equal(86, position);
            return Status(Status::Delegate, &firstHandler_); 
        }
        else if (name == "SecondElement")
        {
            // delegate handling to a SecondHandler
            return Status(Status::Delegate, &secondHandler_);
        }
        else if (name == "FifthElement")
        {
            // delegate handling to a FifthHandler
            return Status(Status::Delegate, &fifthHandler_);
        }

        return Status::Ok;
    }

    private:
    Root& object_;
    FirstHandler firstHandler_;
    SecondHandler secondHandler_;
    FifthHandler fifthHandler_;
};


void test()
{
    if (os_) *os_ << "test()\n";

    istringstream is(sampleXML);
    Root root;
    RootHandler rootHandler(root);
    parse(is, rootHandler);

    if (os_)
    {
        *os_ << "root.param: " << root.param << endl
             << "first.escaped_attribute: " << root.first.escaped_attribute << endl
             << "first.text: " << root.first.text << endl
             << "second.param2: " << root.second.param2 << endl
             << "second.param3: " << root.second.param3 << endl
             << "second.text: ";
        copy(root.second.text.begin(), root.second.text.end(), ostream_iterator<string>(*os_,"|"));
        *os_ << "\nfifth.leeloo: " << root.fifth.leeloo << endl
             << "fifth.mr_zorg: " << root.fifth.mr_zorg << endl
             << "\n"; 
    }

    unit_assert_operator_equal("value", root.param);
    unit_assert_operator_equal("\"<&lt;>\"", root.first.escaped_attribute);
    unit_assert_operator_equal("Some Text with Entity References: <&>", root.first.text);
    unit_assert_operator_equal("something", root.second.param2);
    unit_assert_operator_equal("something.else 1234-56", root.second.param3);
    unit_assert_operator_equal(4, root.second.text.size());
    unit_assert_operator_equal("Pre-Text", root.second.text[0]);
    unit_assert_operator_equal("Inlined text with", root.second.text[1]);
    unit_assert_operator_equal("<&\">", root.second.text[2]);
    unit_assert_operator_equal("Post-text.", root.second.text[3]);
    unit_assert_operator_equal(">Leeloo > mul-\"tipass", root.fifth.leeloo);
    unit_assert_operator_equal("You're a monster, Zorg.>I know.", root.fifth.mr_zorg);
}


void testNoAutoUnescape()
{
    if (os_) *os_ << "testNoAutoUnescape()\n";

    istringstream is(sampleXML);
    Root root;
    RootHandler rootHandler(root, false, false);
    parse(is, rootHandler);

    if (os_)
    {
        *os_ << "root.param: " << root.param << endl
             << "first.escaped_attribute: " << root.first.escaped_attribute << endl
             << "first.text: " << root.first.text << endl
             << "second.param2: " << root.second.param2 << endl
             << "second.param3: " << root.second.param3 << endl
             << "second.text: ";
        copy(root.second.text.begin(), root.second.text.end(), ostream_iterator<string>(*os_,"|"));
        *os_ << "\n\n"; 
    }

    unit_assert_operator_equal("value", root.param);
    unit_assert_operator_equal("&quot;&lt;&amp;lt;&gt;&quot;", root.first.escaped_attribute);
    unit_assert_operator_equal("Some Text with Entity References: &lt;&amp;&gt;", root.first.text);
    unit_assert_operator_equal("something", root.second.param2);
    unit_assert_operator_equal("something.else 1234-56", root.second.param3);
    unit_assert_operator_equal(4, root.second.text.size());
    unit_assert_operator_equal("Pre-Text", root.second.text[0]);
    unit_assert_operator_equal("Inlined text with", root.second.text[1]);
    unit_assert_operator_equal("<&\">", root.second.text[2]);
    unit_assert_operator_equal("Post-text.", root.second.text[3]);
}


class AnotherRootHandler : public Handler
{
    public:

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        if (name == "AnotherRoot")
        {
            unit_assert_operator_equal(656, position);
            return Status::Done; 
        }

        return Status::Ok;
    }
};


void testDone()
{
    if (os_) *os_ << "testDone()\n";

    istringstream is(sampleXML);
    AnotherRootHandler handler;
    parse(is, handler); // parses <RootElement> ... </RootElement>
    parse(is, handler); // parses <AnotherRootElement> and aborts
    
    string buffer;
    getline(is, buffer, '<');
    
    if (os_) *os_ << "buffer: " << buffer << "\n\n";
    unit_assert_operator_equal("The quick brown fox jumps over the lazy dog.", buffer);
}


void testBadXML()
{
    if (os_) *os_ << "testBadXML()\n";

    const char* bad = "<A><B></A></B>";
    istringstream is(bad);
    Handler handler;

    try 
    {
        parse(is, handler);
    }
    catch (exception& e)
    {
        if (os_) *os_ << e.what() << "\nOK: Parser caught bad XML.\n\n";
        return;
    }
    
    throw runtime_error("Parser failed to catch bad XML.");
}


struct NestedHandler : public SAXParser::Handler
{
    int count;
    NestedHandler() : count(0) {}

    virtual Status endElement(const string& name, stream_offset position)
    {
        count++;
        return Status::Ok;
    }
};


void testNested()
{
    if (os_) *os_ << "testNested()\n"; 
    const char* nested = "<a><a></a></a>";
    istringstream is(nested);

    NestedHandler nestedHandler;
    parse(is, nestedHandler);
    if (os_) *os_ << "count: " << nestedHandler.count << "\n\n";
    unit_assert_operator_equal(2, nestedHandler.count);
}


void testRootElement()
{
    if (os_) *os_ << "testRootElement()\n";

    string RootElement = "RootElement";
    unit_assert_operator_equal(RootElement, xml_root_element(sampleXML));

    istringstream sampleXMLStream(sampleXML);
    unit_assert_operator_equal(RootElement, xml_root_element(sampleXMLStream));

    {ofstream sampleXMLFile("testRootElement.xml"); sampleXMLFile << sampleXML;}
    unit_assert_operator_equal(RootElement, xml_root_element_from_file("testRootElement.xml"));
    bfs::remove("testRootElement.xml");

    unit_assert_operator_equal(RootElement, xml_root_element("<?xml?><RootElement>"));
    unit_assert_operator_equal(RootElement, xml_root_element("<?xml?><RootElement name='value'"));

    unit_assert_throws(xml_root_element("not-xml"), runtime_error);
}


void testDecoding()
{
    string id1("_x0031_invalid_x0020_ID");
    unit_assert_operator_equal("1invalid ID", decode_xml_id_copy(id1));
    unit_assert_operator_equal((void *)&id1, (void *)&decode_xml_id(id1)); // should return reference to id1
    unit_assert_operator_equal("1invalid ID", id1);

    string id2("_invalid-ID__x0023_2__x003c_3_x003e_");
    unit_assert_operator_equal("_invalid-ID_#2_<3>", decode_xml_id_copy(id2));
    unit_assert_operator_equal("_invalid-ID_#2_<3>", decode_xml_id(id2));

    string crazyId("_x0021__x0021__x0021_");
    unit_assert_operator_equal("!!!", decode_xml_id(crazyId));
}

void testSaxParserString() 
{
    std::string str = " \t foo \n";
    saxstring xstr = str;
    unit_assert_operator_equal(xstr,str);
    unit_assert_operator_equal(xstr,str.c_str());
    unit_assert_operator_equal(str.length(),xstr.length());
    xstr.trim_lead_ws();
    unit_assert_operator_equal(xstr.length(),str.length()-3);
    unit_assert_operator_equal(xstr,str.substr(3));
    xstr.trim_trail_ws();
    unit_assert_operator_equal(xstr.length(),str.length()-5);
    unit_assert_operator_equal(xstr,str.substr(3,3));
    unit_assert_operator_equal(xstr[1],'o');
    xstr[1] = '0';
    unit_assert_operator_equal(xstr[1],'0');
    std::string str2(xstr.data());
    unit_assert_operator_equal(str2,"f0o");
    std::string str3(xstr.c_str());
    unit_assert_operator_equal(str2,str3);
    saxstring xstr2(xstr);
    unit_assert_operator_equal(xstr2,xstr);
    saxstring xstr3;
    unit_assert_operator_equal(xstr3.c_str(),std::string());
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        demo();
        testSaxParserString();
        test();
        testNoAutoUnescape();
        testDone();
        testBadXML();
        testNested();
        testRootElement();
        testDecoding();
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

