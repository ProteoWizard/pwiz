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

#define PWIZ_SOURCE

#include "XMLWriter.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "boost/iostreams/filtering_stream.hpp" 
#include "boost/iostreams/filter/counter.hpp"
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace minimxml {


template <typename T>
struct double12_policy : boost::spirit::karma::real_policies<T>   
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }
};

PWIZ_API_DECL void XMLWriter::Attributes::add(const string& name, const double& valueRef)
{
    double value = valueRef;

    // HACK: karma has a stack overflow on subnormal values, so we clamp to normalized values
    if (value > 0)
        value = max(numeric_limits<double>::min(), value);
    else if (value < 0)
        value = min(-numeric_limits<double>::min(), value);

    using namespace boost::spirit::karma;
    typedef real_generator<double, double12_policy<double> > double12_type;
    static const double12_type double12 = double12_type();
    char buffer[256];
    char* p = buffer;
    generate(p, double12, value);
    *p = '\0';
    push_back(make_pair(name, std::string(&buffer[0], p)));
}

PWIZ_API_DECL void XMLWriter::Attributes::add(const string& name, const int& value)
{
    using namespace boost::spirit::karma;
    static const int_generator<int> intgen = int_generator<int>();
    char buffer[256];
    char* p = buffer;
    generate(p, intgen, value);
    *p = '\0';
    push_back(make_pair(name, std::string(&buffer[0], p)));
}


class XMLWriter::Impl
{
    public:

    Impl(ostream& os, const Config& config);
    void pushStyle(unsigned int flags);
    void popStyle(); 
    void processingInstruction(const string& name, const string& data);
    void startElement(const string& name, 
                      const Attributes& attributes,
                      EmptyElementTag emptyElementTag);
    void endElement();
    void characters(const string& text, bool autoEscape);
    bio::stream_offset position() const;
    bio::stream_offset positionNext() const;

    private:
    ostream& os_;
    Config config_;
    stack<string> elementStack_;
    stack<unsigned int> styleStack_;

    string indentation() const {return string(elementStack_.size()*config_.indentationStep, ' ');}
    string indentation(size_t depth) const {return string(depth*config_.indentationStep, ' ');}
    bool style(StyleFlag styleFlag) const {return styleStack_.top() & styleFlag ? true : false;}
};


XMLWriter::Impl::Impl(ostream& os, const Config& config)
:   os_(os), config_(config)
{
    styleStack_.push(config.initialStyle);
}


void XMLWriter::Impl::pushStyle(unsigned int flags)
{
    styleStack_.push(flags);
}


void XMLWriter::Impl::popStyle() 
{
    styleStack_.pop();
    if (styleStack_.empty())
        throw runtime_error("[XMLWriter] Style stack underflow.");
}


void XMLWriter::Impl::processingInstruction(const string& name, const string& data) 
{
    ostream* os = &os_;
    if (config_.outputObserver) os = new ostringstream;

    *os << indentation() << "<?" << name << " " << data << "?>\n"; 

    if (config_.outputObserver)
    {
        config_.outputObserver->update(static_cast<ostringstream*>(os)->str());
        os_ << static_cast<ostringstream*>(os)->str();
        delete os;
    }
}


void writeEscapedAttributeXML(ostream& os, const string& str)
{
    for (size_t i=0, end=str.size(); i < end; ++i)
    {
        const char& c = str[i];
        switch (c)
        {
            case '&': os << "&amp;"; break;
            case '"': os << "&quot;"; break;
            case '\'': os << "&apos;"; break;
            case '<': os << "&lt;"; break;
            case '>': os << "&gt;"; break;
            default: os << c; break;
        }
    }
}


void writeEscapedTextXML(ostream& os, const string& str)
{
    for (size_t i=0, end=str.size(); i < end; ++i)
    {
        const char& c = str[i];
        switch (c)
        {
            case '&': os << "&amp;"; break;
            case '<': os << "&lt;"; break;
            case '>': os << "&gt;"; break;
            default: os << c; break;
        }
    }
}


void XMLWriter::Impl::startElement(const string& name, 
                  const Attributes& attributes,
                  EmptyElementTag emptyElementTag)
{
    ostream* os = &os_;
    if (config_.outputObserver) os = new ostringstream;

    if (!style(StyleFlag_InlineOuter))
        *os << indentation();

    *os << "<" << name;

    string attributeIndentation(name.size()+1, ' ');

    for (Attributes::const_iterator it=attributes.begin(); it!=attributes.end(); ++it)
    {
        *os << " " << it->first << "=\"";
        writeEscapedAttributeXML(*os, it->second);
        *os << "\"";
        if (style(StyleFlag_AttributesOnMultipleLines) && (it+1)!=attributes.end())
            *os << "\n" << indentation() << attributeIndentation;
    }

    *os << (emptyElementTag==EmptyElement ? "/>" : ">");

    if (!style(StyleFlag_InlineInner) || 
        (!style(StyleFlag_InlineOuter) && emptyElementTag==EmptyElement))
        *os << "\n";

    if (emptyElementTag == NotEmptyElement)
        elementStack_.push(name);

    if (config_.outputObserver)
    {
        config_.outputObserver->update(static_cast<ostringstream*>(os)->str());
        os_ << static_cast<ostringstream*>(os)->str();
        delete os;
    }
}


void XMLWriter::Impl::endElement()
{
    ostream* os = &os_;
    if (config_.outputObserver) os = new ostringstream;

    if (elementStack_.empty())
        throw runtime_error("[XMLWriter] Element stack underflow.");

    if (!style(StyleFlag_InlineInner))
        *os << indentation(elementStack_.size()-1);

    *os << "</" << elementStack_.top() << ">";
    elementStack_.pop();

    if (!style(StyleFlag_InlineOuter))
        *os << "\n";
        
    if (config_.outputObserver)
    {
        config_.outputObserver->update(static_cast<ostringstream*>(os)->str());
        os_ << static_cast<ostringstream*>(os)->str();
        delete os;
    }
}


void XMLWriter::Impl::characters(const string& text, bool autoEscape)
{
    ostream* os = &os_;
    if (config_.outputObserver) os = new ostringstream;

    if (!style(StyleFlag_InlineInner))
        *os << indentation();

    if (autoEscape)
        writeEscapedTextXML(*os, text);
    else
        *os << text;

    if (!style(StyleFlag_InlineInner))
        *os << "\n";

    if (config_.outputObserver)
    {
        config_.outputObserver->update(static_cast<ostringstream*>(os)->str());
        os_ << static_cast<ostringstream*>(os)->str();
        delete os;
    }
}


XMLWriter::stream_offset XMLWriter::Impl::position() const
{
    os_ << flush;
	// check to see if we're actually writing to a gzip file 
	boost::iostreams::filtering_ostream *zipper = dynamic_cast<boost::iostreams::filtering_ostream *>(&os_);
	if (zipper) 
	{  // os_ is actually a boost::iostreams::filtering_ostream with gzip and a counter
		return zipper->component<0, pwiz::minimxml::charcounter>()->characters();
	}
	else
	{  // OK to do a simple ftellp because seek is implemented, unlike with gzip
	    return boost::iostreams::position_to_offset(os_.tellp()); 
	}
}


XMLWriter::stream_offset XMLWriter::Impl::positionNext() const
{
    stream_offset offset = position(); 
    if (!style(StyleFlag_InlineOuter))
        offset += indentation().size();
    return offset;
}


//
// XMLWriter forwarding functions 
//


PWIZ_API_DECL XMLWriter::XMLWriter(ostream& os, const Config& config)
:   impl_(new Impl(os, config))
{}

PWIZ_API_DECL void XMLWriter::pushStyle(unsigned int flags) {impl_->pushStyle(flags);}

PWIZ_API_DECL void XMLWriter::popStyle() {impl_->popStyle();}

PWIZ_API_DECL void XMLWriter::processingInstruction(const string& name, const string& data) 
{
    impl_->processingInstruction(name, data);
}

PWIZ_API_DECL void XMLWriter::startElement(const string& name, 
                             const Attributes& attributes,
                             EmptyElementTag emptyElementTag)
{
    impl_->startElement(name, attributes, emptyElementTag);
}

PWIZ_API_DECL void XMLWriter::endElement() {impl_->endElement();}

PWIZ_API_DECL void XMLWriter::characters(const string& text, bool autoEscape) {impl_->characters(text, autoEscape);}

PWIZ_API_DECL XMLWriter::stream_offset XMLWriter::position() const {return impl_->position();}

PWIZ_API_DECL XMLWriter::stream_offset XMLWriter::positionNext() const {return impl_->positionNext();}


namespace {

// NCName          ::= NCNameStartChar (NCNameChar)*
// NCNameStartChar ::= [A-Z] | '_' | [a-z]
// NCNameChar      ::= NCNameStartChar | [0-9] | '.' | '-'
//
// Note: If we were working in Unicode, there's a lot of other valid characters,
//       but here we'll just encode any non-ASCII value.
bool isNCNameStartChar(char& c)
{
    return std::isalpha(c, std::locale::classic()) || c == '_';
}

bool isNCNameChar(char& c)
{
    return isNCNameStartChar(c) ||
           std::isdigit(c, std::locale::classic()) ||
           c == '.' ||
           c == '-';
}

const char hex[] = "0123456789abcdef";
void insertEncodedChar(string& str, string::iterator& itr)
{
    char c = *itr;
    *itr = '_';
    str.insert(size_t(itr-str.begin()), "_x0000");
    itr += 4;
    *itr = hex[(c & 0xF0) >> 4];
    *(++itr) = hex[c & 0x0F];
    ++itr;
}

} // namespace


PWIZ_API_DECL string& encode_xml_id(string& str)
{
    if (str.empty())
        throw std::invalid_argument("[encode_xml_id] xml:IDs and xml:IDREFs cannot be empty strings");

    // reserve size for the worst case scenario (all characters need replacing),
    // this should be a reasonable guarantee that the iterator won't be invalidated
    str.reserve(str.length()*7);
    string::iterator itr = str.begin();

    if (!isNCNameStartChar(*itr))
        insertEncodedChar(str, itr);

    for (; itr != str.end(); ++itr)
        if (!isNCNameChar(*itr))
            insertEncodedChar(str, itr);

    return str;
}


PWIZ_API_DECL string encode_xml_id_copy(const string& str)
{
    string copy(str);
    return encode_xml_id(copy);
}


} // namespace minimxml
} // namespace pwiz


