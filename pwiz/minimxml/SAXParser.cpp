//
// SAXParser.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SAXParser.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <stdexcept>
#include <stack>


//#define PWIZ_USE_BOOST_REGEX 
#ifdef PWIZ_USE_BOOST_REGEX 
#define BOOST_REGEX_MATCH_EXTRA // use boost::regex with multiple matches for tag parsing
#include "boost/regex.hpp"
//
// must link with boost::regex with BOOST_REGEX_MATCH_EXTRA
// defined in boost/regex/user.hpp
//
// boost::regex quick ref:
//   \w (alphanumeric)
//   \s (whitespace)
//   (...) marks a capture
//   (?:...) marks a group with no capture
//   what[0] stores the full matched expression
//   what[n] stores capture n
//
#endif // PWIZ_USE_BOOST_REGEX


namespace pwiz {
namespace minimxml {
namespace SAXParser {


using namespace std;
using boost::lexical_cast;


namespace {


size_t stripws(string& text)
{
    static const char* ws = "\n\r\t ";

    string::size_type first = text.find_first_not_of(ws);
    text.erase(0, first);

    string::size_type second = text.find_last_not_of(ws);
    if (second+1 < text.size()) text.erase(second+1);

    return first; // return the whitespace stripped from beginning
}


#ifdef PWIZ_USE_BOOST_REGEX

struct ProcessingInstruction 
{
    string name;
    string value; 

    ProcessingInstruction(const string& buffer)
    {
        // ?name value?
        static const boost::regex e("\\?(\\w+)\\s+([^?]+)\\?");

        boost::smatch what; 
        if (!regex_match(buffer,what,e,boost::match_extra) || what.size()!=3)
            throw runtime_error(("[SAXParser::ProcessingInstruction()] Error parsing tag: " + buffer).c_str());

        name = what[1];
        value = what[2];
    }
};

struct StartTag
{
    string name;
    Handler::Attributes attributes;
    bool end;

    StartTag(const string& buffer)
    {
        // tagName (name="value")* '/'?
        static const boost::regex e("([\\w:]+)(?:\\s*([\\w:]+)=\"([^\"]*)\")*\\s*(/)?");

        boost::smatch what; 
        if (!regex_match(buffer,what,e,boost::match_extra) || what.size()!=5)
            throw runtime_error(("[SAXParser::StartTag()] Error parsing tag: " + buffer).c_str());

        if (what.captures(2).size() != what.captures(3).size())
            throw runtime_error("[SAXParser::StartTag()] Error parsing attributes.");

        name = what[1];

        for (unsigned int i=0; i<what.captures(2).size(); i++)
            attributes[what.captures(2)[i]] = what.captures(3)[i];

        end = (what[4]=='/'); 
    }
};

#else // parsing with std library

struct ProcessingInstruction 
{
    string name;
    string value; 

    ProcessingInstruction(const string& buffer)
    {
        istringstream iss(buffer);
        char questionMark = '\0';
        iss >> questionMark >> name;
        if (questionMark != '?') throw runtime_error("[SAXParser::ProcessingInstruction] Error.");
        getline(iss, value, '?');
        stripws(value);
    }
};


const string whitespace_ = " \t\n\r";


void parseAttribute(const string& tag, string::size_type& index, Handler::Attributes& attributes)
{
    string::size_type indexNameBegin = tag.find_first_not_of(whitespace_, index);
    string::size_type indexNameEnd = tag.find_first_of(whitespace_ + '=', indexNameBegin+1);
    string::size_type indexEquals = tag.find_first_of('=', indexNameEnd);
    string::size_type indexQuoteOpen = tag.find_first_of('"', indexEquals+1);
    string::size_type indexQuoteClose = tag.find_first_of('"', indexQuoteOpen+1);

    if (indexNameBegin == string::npos ||
        indexNameEnd == string::npos ||
        indexEquals == string::npos ||
        indexQuoteOpen == string::npos ||
        indexQuoteClose == string::npos)
        throw runtime_error("[SAXParser::parseAttribute()] Error at index " 
                            + lexical_cast<string>(index));

    string name = tag.substr(indexNameBegin, indexNameEnd-indexNameBegin);
    string value = tag.substr(indexQuoteOpen+1, indexQuoteClose-indexQuoteOpen-1);

    attributes[name] = value;
    index = tag.find_first_not_of(whitespace_, indexQuoteClose+1);
}


struct StartTag
{
    string name;
    Handler::Attributes attributes;
    bool end;

    StartTag(const string& buffer)
    :   end(false)
    {
        if (buffer[buffer.size()-1] == '/') end = true;

        string::size_type indexNameBegin = buffer.find_first_not_of(whitespace_);
        if (indexNameBegin == string::npos)
            throw runtime_error("[SAXParser::StartTag] Empty buffer.");

        string::size_type indexNameEnd = buffer.find_first_of(whitespace_ + "/", indexNameBegin+1);
        if (indexNameEnd == string::npos)
        {
            name = buffer.substr(indexNameBegin);
            return;
        }

        name = buffer.substr(indexNameBegin, indexNameEnd-indexNameBegin);

        string::size_type index = indexNameEnd + 1;
        string::size_type indexEnd = end ? buffer.size()-1 : buffer.size();
        while (index < indexEnd)
            parseAttribute(buffer, index, attributes);
    }
};

#endif // PWIZ_USE_BOOST_REGEX


struct HandlerInfo
{
    Handler& handler;
    stack<string> names;

    HandlerInfo(Handler& _handler)
    :   handler(_handler)
    {}
};


// HandlerWrangler responsibilities:
// - maintain a Handler stack
// - validate return Status from Handler calls
// - validate element start/end tag matching
class HandlerWrangler : public SAXParser::Handler
{
    public:

    HandlerWrangler(Handler& root)
    {
        handlers_.push(root);
    }

    void verifyNoDelegate(const Status& status)
    {
        if (status.flag==Status::Delegate || status.delegate)
            throw runtime_error("[SAXParser] Illegal return of Status::Delegate.");
    }

    virtual Status processingInstruction(const string& name,
                                         const string& data,
                                         stream_offset position)
    {
        Status status = handlers_.top().handler.processingInstruction(name, data, position);
        verifyNoDelegate(status); 
        return status;
    }

    virtual Status startElement(const string& name,
                                const Attributes& attributes,
                                stream_offset position)
    {
        HandlerInfo& top = handlers_.top();
         
        // element start/end validation

        top.names.push(name);

        // call handler

        Handler::Status status = top.handler.startElement(name, attributes, position);
        if (status.flag != Handler::Status::Delegate)
            return status; 

        // Status::Delegate: let status.delegate handle this message 

        if (!status.delegate) throw runtime_error("[SAXParser] Null delegate.");
        top.names.pop();            
        handlers_.push(*status.delegate);
        return startElement(name, attributes, position);
    }

    virtual Status endElement(const string& name, stream_offset position)
    {
        HandlerInfo& top = handlers_.top();

        // element start/end validation

        if (top.names.empty() || top.names.top()!=name) 
            throw runtime_error(("[SAXParser::ParserWrangler::elementEnd()] Illegal end tag: " + name).c_str()); 

        top.names.pop();

        // call handler

        Status status = top.handler.endElement(name, position);
        verifyNoDelegate(status); 

        // delete handler if we're done with it 

        if (top.names.empty())
        {
            handlers_.pop();
            if (handlers_.empty()) return Status::Done;
        }

        return status;
    }

    virtual Status characters(const string& text, stream_offset position)
    {
        Status status = handlers_.top().handler.characters(text, position);
        verifyNoDelegate(status);
        return status;
    }

    private:
    stack<HandlerInfo> handlers_;
};


} // namespace


//
// parse() responsibilities: 
// - stream parsing
// - initiation of events
//   - events are routed through a HandlerWrangler
//   - HandlerWrangler handles any XML/Handler validation
// - return on Handler::Status::Done
//
void parse(istream& is, Handler& handler)
{
    using boost::iostreams::offset_to_position;

    HandlerWrangler wrangler(handler);
    Handler::stream_offset position = offset_to_position(is.tellg());

    while (is)
    {
        string buffer;

        // read text up to next tag (may be empty)

        if (!getline(is, buffer, '<')) break;

        // position == beginning of characters

        position += stripws(buffer); 

        if (!buffer.empty())
        {
            Handler::Status status = wrangler.characters(buffer, position);
            if (status.flag == Handler::Status::Done) return;
        }

        // position == beginning of tag

        position = offset_to_position(is.tellg());
        if (position > 0) position--;

        // read tag

        if (!getline(is, buffer, '>')) break;
        stripws(buffer);
        if (buffer.empty())
            throw runtime_error("[SAXParser::parse()] Empty tag."); 

        // switch on tag type

        switch (buffer[0])
        {
            case '?':
            {
                ProcessingInstruction pi(buffer);
                Handler::Status status = wrangler.processingInstruction(pi.name, pi.value, position);
                if (status.flag == Handler::Status::Done) return;
                break; 
            }
            case '/':
            {
                Handler::Status status = wrangler.endElement(buffer.substr(1), position);
                if (status.flag == Handler::Status::Done) return;
                break;
            }
            case '!':
            {
                if (buffer.size()<5 || 
                    buffer.substr(0,3) != "!--" || 
                    buffer.substr(buffer.size()-2) != "--")
                    throw runtime_error(("[SAXParser::parse()] Illegal comment: " + buffer).c_str());
                break;
            }
            default: 
            {
                StartTag tag(buffer);

                Handler::Status status = wrangler.startElement(tag.name, tag.attributes, position);
                if (status.flag == Handler::Status::Done) return;
                
                if (tag.end) 
                {
                    status = wrangler.endElement(tag.name, position);
                    if (status.flag == Handler::Status::Done) return;
                }
            }
        }

        // position == after tag end
        position = offset_to_position(is.tellg());
    }
}


} // namespace SAXParser 
} // namespace minimxml 
} // namespace pwiz


