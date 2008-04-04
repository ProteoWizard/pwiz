//
// SAXParserTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "SAXParser.hpp"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <vector>
#include <iterator>
#include <algorithm>


using namespace std;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::minimxml::SAXParser;


ostream* os_;


const char* sampleXML = 
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<RootElement param=\"value\">\n"
    "    <FirstElement>\n"
    "        Some Text\n"
    "    </FirstElement>\n"
    "    <SecondElement param2=\"something\" param3=\"something.else 1234-56\">\n"
    "        Pre-Text <Inline>Inlined text</Inline> Post-text. <br/>\n"
    "    </SecondElement>\n"
    "    <prefix:ThirdElement goober:name=\"value\">\n"
    "    <!--this is a comment-->\n"
    "    <empty_with_space />\n"
    "    </prefix:ThirdElement>\n"
    "</RootElement>\n"
    "<AnotherRoot>The quick brown fox jumps over the lazy dog.</AnotherRoot>\n";


//
// demo of event handling
//


struct PrintAttribute
{
    PrintAttribute(ostream& os) : os_(os) {}
    ostream& os_;

    void operator()(const pair<string,string>& attribute)
    {
        os_ << " (" << attribute.first << "," << attribute.second << ")";
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

    virtual Status characters(const string& text, stream_offset position)
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
    string text;
};


struct Second
{
    string param2;
    string param3;
    vector<string> text; 
};


struct Root
{
    string param;
    First first;
    Second second;
};


//
//
// Handlers to connect XML to C++ model
//


void readAttribute(const Handler::Attributes& attributes, 
                   const string& attributeName, 
                   string& result)
{
    Handler::Attributes::const_iterator it = attributes.find(attributeName);
    if (it != attributes.end())
        result = it->second;
}


class FirstHandler : public Handler
{
    public:
    
    FirstHandler(First& first)
    :   object_(first)
    {}

    virtual Status characters(const string& text, stream_offset position)
    {
        unit_assert(position == 0x5e);
        object_.text = text;          
        return Status::Ok;
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        unit_assert(position == 0x6c);
        return Status::Ok;
    }

    private:
    First& object_;
};


class SecondHandler : public Handler
{
    public:

    SecondHandler(Second& object)
    :   object_(object)
    {}

    virtual Status startElement(const string& name,
                                const Handler::Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "SecondElement")
        {
            readAttribute(attributes, "param2", object_.param2);
            readAttribute(attributes, "param3", object_.param3);
        }
           
        return Status::Ok;
    }

    virtual Status characters(const string& text, stream_offset position)
    {
        object_.text.push_back(text);          
        return Status::Ok;
    }

    private:
    Second& object_;
};


class RootHandler : public Handler
{
    public:
    
    RootHandler(Root& root)
    :   object_(root), 
        firstHandler_(object_.first),
        secondHandler_(object_.second)
    {}

    virtual Status startElement(const string& name,
                                const Attributes& attributes, 
                                stream_offset position)
    {
        if (name == "RootElement")
        {
            readAttribute(attributes, "param", object_.param);
            unit_assert(position == 0x27);
        }
        else if (name == "FirstElement")
        {
            // delegate handling to a FirstHandler
            unit_assert(position == 0x47);
            return Status(Status::Delegate, &firstHandler_); 
        }
        else if (name == "SecondElement")
        {
            // delegate handling to a SecondHandler
            return Status(Status::Delegate, &secondHandler_);
        }

        return Status::Ok;
    }

    private:
    Root& object_;
    FirstHandler firstHandler_;
    SecondHandler secondHandler_;
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
             << "first.text: " << root.first.text << endl
             << "second.param2: " << root.second.param2 << endl
             << "second.param3: " << root.second.param3 << endl
             << "second.text: ";
        copy(root.second.text.begin(), root.second.text.end(), ostream_iterator<string>(*os_,"|"));
        *os_ << "\n\n"; 
    }

    unit_assert(root.param == "value");
    unit_assert(root.first.text == "Some Text");
    unit_assert(root.second.param2 == "something");
    unit_assert(root.second.param3 == "something.else 1234-56");
    unit_assert(root.second.text.size() == 3);
    unit_assert(root.second.text[0] == "Pre-Text");
    unit_assert(root.second.text[1] == "Inlined text");
    unit_assert(root.second.text[2] == "Post-text.");
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
            unit_assert(position == 0x1a6);
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
    unit_assert(buffer == "The quick brown fox jumps over the lazy dog.");
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
    unit_assert(nestedHandler.count == 2);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        demo();
        test();
        testDone();
        testBadXML();
        testNested();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n"; 
    }
     
    return 1;
}

