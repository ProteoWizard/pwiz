//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Reworked for zero-copy performance by Brian Pratt, Insilicos LLC
// those changes Copyright 2011 Insilicos LLC All Rights Reserved
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


#ifndef _SAXPARSER_HPP_
#define _SAXPARSER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "pwiz/utility/misc/shared_map.hpp"
#include "boost/iostreams/positioning.hpp"
#include <string.h>
#include <iosfwd>
#include <string>
#include <vector>
#include <assert.h>
#include <stdexcept>


namespace pwiz {
namespace minimxml {


///
/// An extended SAX interface for custom XML stream parsing.
///
/// Use cases:
/// - read a single element
/// - read a single element, aborting on a specified tag
/// - delegate handling of a sub-element to another handler 
///
namespace SAXParser {

PWIZ_API_DECL size_t count_trail_ws(const char *data,size_t len); // count whitespace chars at end of data
PWIZ_API_DECL void unescapeXML(char *str);
PWIZ_API_DECL void unescapeXML(std::string &str);

class saxstring 
{ 
    // simple string management for zero-copy saxparser
    //
    // not using std::string due to overhead with:
    // reference counts 
    // exception unwinding 
    // etc etc
    //
    // provides for zero-copy trimming of whitespace
    //
public:
    saxstring(size_t size = 0) {
        init(size);
    }

    saxstring(const SAXParser::saxstring &rhs) {
        *this = rhs;
    }

    saxstring(const std::string &rhs) {
        init(rhs.length());
        memcpy(data(),rhs.c_str(),rhs.length());
        (*this)[rhs.length()] = 0; // nullterm
    }

    void unescapeXML() {
        if (strchr(c_str(),'&')) {
            SAXParser::unescapeXML(data());
            resize(strlen(c_str()));
        }
    }

    ~saxstring() {
        free(_data);
    }

    saxstring & operator = (const SAXParser::saxstring &rhs) {
        init(rhs.length());
        if (length()) {
            memcpy(data(),rhs.c_str(),length()+1);
        }
        return *this;
    }

    saxstring & operator = (const char *rhs) {
        init(rhs ? strlen(rhs) : 0);
        if (length()) {
            memcpy(data(),rhs,length()+1);
        }
        return *this;
    }

    saxstring & operator += (const SAXParser::saxstring &rhs) {
        if (rhs.length()) {
            size_t oldsize = length();
            resize(rhs.length()+oldsize);
            memcpy(data()+oldsize,rhs.c_str(),rhs.length()+1);
        }
        return *this;
    }

    saxstring & operator += (const char *rhs) {
        size_t rhslen = rhs?strlen(rhs):0;
        if (rhslen) {
            size_t oldsize = length();
            resize(rhslen+oldsize);
            strcpy(data()+oldsize,rhs);
        }
        return *this;
    }

    bool operator == (const char *c) const {
        return c && !strcmp(c,c_str());
    }

    bool operator == (const std::string &s) const {
        return !strcmp(c_str(),s.c_str());
    }

    bool operator == (const saxstring &s) const {
        return !strcmp(c_str(),s.c_str());
    }

    char *resize(size_t size) {
        if (!size) {
            _lead = 0; // empty, reclaim the start of buffer
        }
        size_t new_used = size + _lead; // translate to "used" space
        if (new_used >= _capacity) {
            _data = (char *)realloc(_data, (_used = new_used)+1);
            if (_used && !_data) {
                throw std::runtime_error("SAXParser: cannot allocate memory");
            }
            _capacity = _used;
        } else {
            _used = new_used;
        }
        _data[_used] = 0;
        return _data;
    }
    void clear() {
        resize(0);
    }
    inline const char *c_str() const {
        return _data?_data+_lead:"";
    }
    inline char & operator [](size_t n) {
        return *(data()+n);
    }
    inline size_t length() const {
        return _used-_lead;
    }
    inline size_t capacity() const {
        return _capacity;
    }
    void trim_trail_ws() { // remove trailing whitespace if any
        size_t n = count_trail_ws(c_str(),length());
        resize(length()-n);
    }
    // returns number of ws chars it had to eat on front end
    int trim_lead_ws() {
        size_t n=0;
        for (const char *c=c_str(); *c && strchr(" \n\r\t",*c); c++) {
            n++;
        }
        _lead += n;
        return n;
    }
    bool starts_with(const char *txt) const {
        return !strncmp(c_str(),txt,strlen(txt));
    }
    bool ends_with(const char *txt) const {
        size_t len = strlen(txt);
        return (len <= length()) ? (!strcmp(c_str()+length()-len,txt)) : false;
    }
    char *data() { // direct access to data buffer
        if (!_data) {
            resize(0);
        }
        return _data+_lead;
    }
private:
    void init(size_t size) {
        _used = 0;
        _lead = 0;
        _capacity = 0;
        _data = NULL;
        if (size) {
            resize(size);
        }
    }
    char * _data;  // char buf
    size_t _used;  // characters used
    size_t _lead;  // for skipping whitespace
    size_t _capacity; // max characters (always >_used)
};

inline std::ostream& operator<<(std::ostream& os, const saxstring& s)
{
    os << s.c_str();
    return os;
}

// fast string-to-value conversions
// not very boost-y, or even very c++, but lexical_cast and istringstreams are
// just too slow for our parsing performance needs.
template< typename Target > inline Target textToValue(const char *txt); // template prototype

template<> inline float textToValue(const char *txt)
{
    return (float) ATOF( txt ) ;
}

template<> inline double textToValue(const char *txt)
{
    return ATOF( txt );
}

template<> inline int textToValue(const char *txt)
{
    return atoi(txt);
}

template<> inline char textToValue(const char *txt)
{
    return *(txt);
}

template<> inline long textToValue(const char *txt)
{
    return atol(txt);
}

template<> inline unsigned int textToValue(const char *txt)
{
    return (unsigned int) strtoul( txt, NULL, 10 );
}

template<> inline unsigned long textToValue(const char *txt)
{
    return strtoul( txt, NULL, 10 );
}

#if defined(BOOST_HAS_LONG_LONG)

template<> inline long long textToValue(const char *txt)
{
#if defined(BOOST_HAS_MS_INT64)
    return _atoi64(txt);
#else
    return atoll(txt);
#endif
}

template<> inline unsigned long long textToValue(const char *txt)
{
#if defined(BOOST_HAS_MS_INT64)
    return  _strtoui64(txt,NULL,10);
#else
    return strtoull( txt, NULL, 10 );
#endif
}

#endif // has long long

inline bool istrue(const char *t)
{
    return strcmp(t, "0") && strcmp(t,"false"); // as in optimized_lexical_cast.h
}

template<> inline bool textToValue(const char *txt)
{
    return istrue(txt);
}

template<> inline boost::logic::tribool textToValue(const char *txt)
{
    using namespace boost::logic;
    if (!*txt)
        return tribool(indeterminate);
    else 
    {
        bool b = istrue(txt);
        return tribool(b);
    }
}

template<> inline std::string textToValue(const char *txt)
{
    return std::string( txt );
}


/// SAX event handler interface.
class Handler
{
    public:

    /// When false, no calls to characters() will be made
    bool parseCharacters;

    /// Setting these to false will disable the auto-unescaping feature of the parser;
    /// this is useful for handlers which deal with large amounts of data
    bool autoUnescapeAttributes, autoUnescapeCharacters;

    /// contextual version available to control handler logic which support multiple versions of a schema;
    /// the default value 0 indicates handler should ignore the version;
    /// the handler determines the meaning of any non-zero value
    int version;

    /// Handler returns the Status struct as a means of changing the parser's behavior.  
    struct Status
    {
        enum Flag 
        {
            Ok,       // ok, continue parsing the stream
            Done,    // abort immediately
            Delegate  // delegate this element to the specified Handler [startElement() only] 
        };

        Flag flag;
        Handler* delegate; // valid iff (flag == Delegate)

        Status(Flag _flag = Ok, 
               Handler* _delegate = 0)
        :   flag(_flag), delegate(_delegate)
        {}
    };

    enum XMLUnescapeBehavior_t {XMLUnescapeDefault,NoXMLUnescape};
    class Attributes 
    {
        // lazy evaluation - doesn't process text until asked
        // near-zero copy - copies the source text just once,
        // instead of a bunch of little std::string operations
    public:
        Attributes(const char * _source_text, size_t _source_text_len, bool _autoUnescape) :
          index(0),index_end(0),autoUnescape(_autoUnescape),firstread(true),attrs()
        {
              size=_source_text_len;
              textbuff = (char *)malloc(size+1);
              managemem = true;
              memcpy(textbuff,_source_text,size);
              textbuff[size] = 0;
              setParserIndex(); // ready for eventual parsing
              test_invariant(); // everything correct?
        };
        Attributes() :
          index(0),index_end(0),autoUnescape(false),firstread(true),attrs()
        {
              size=0;
              textbuff = NULL;
              managemem = true;
              test_invariant(); // everything correct?
        };
        Attributes(saxstring &str, bool _autoUnescape) :
          index(0),index_end(0),autoUnescape(_autoUnescape),firstread(true),attrs() 
        {
              textbuff = str.data();
              size=str.length();
              managemem = false; // we don't have to free this
              setParserIndex(); // ready for eventual parsing
              test_invariant(); // everything correct?
        };
        ~Attributes() 
        {
             if (managemem) 
                  free(textbuff);
        }
        Attributes(const Attributes &rhs) 
        {
            textbuff = NULL;
            *this = rhs;
        }
        Attributes & operator = (const Attributes &rhs) {
            size = rhs.size;
            index = rhs.index;
            index_end = rhs.index_end; // string bounds for attribute parsing
            autoUnescape = rhs.autoUnescape; // do XML escape of attribute?
            firstread = rhs.firstread; // may change during const access
            if (managemem)
                textbuff = (char *)realloc(textbuff,size+1);
            else
                textbuff = (char *)malloc(size+1);
            managemem = true; // we need to free textbuff at dtor
            memcpy(textbuff,rhs.textbuff,size+1);
            attrs.resize(rhs.attrs.size()); 
            // now fix up the char ptrs to point to our copy of attribute list
            for (size_t n=attrs.size();n--;) 
            {
                attrs[n].name  = ((char *)textbuff)+(rhs.attrs[n].getName()-rhs.getTextBuffer());
                attrs[n].value = ((char *)textbuff)+(rhs.attrs[n].getValuePtr()-rhs.getTextBuffer());
            }
            test_invariant(); // everything correct?
            return *this;
        }

        inline void test_invariant() const
        {
#ifdef _DEBUG
            for (size_t n=attrs.size();n--;) 
            {
                assert(textbuff != NULL);
                assert(attrs[n].name>textbuff);
                assert(attrs[n].value>attrs[n].name);
                assert(attrs[n].value<textbuff+size);
                if (n) 
                    assert(attrs[n].name>attrs[n-1].value);
            }
#endif
         }

        const char *getTagName() const 
        { // work area contains tag name
            test_invariant(); // everything correct?
            return textbuff+('/'==*textbuff);
        }
        const char *getTextBuffer() const 
        { // return pointer to our work area
            test_invariant(); // everything correct?
            return textbuff;
        }
        size_t getSize() const 
        {
            return size;
        }
    protected:
        mutable char *textbuff; // we'll operate on this copy of string
        size_t size;
        mutable size_t index,index_end; // string bounds for attribute parsing
        bool autoUnescape; // do XML escape of attribute?
        bool managemem; // if true we need to free on exit
        mutable bool firstread; // may change during const access
        
        void setParserIndex() 
        {
            // on entry, buffer has form "foo bar="baz" or maybe "foo/"
            const char *c = textbuff;
            while (*c && !strchr(" \n\r\t/",*c)) c++;
            size_t indexNameEnd = c-textbuff;
            while (*c && strchr(" \n\r\t",*c)) c++;
            textbuff[indexNameEnd] = 0; // nullterm the name
            index = c-textbuff; // should point to bar
            index_end = size;
            test_invariant(); // everything correct?
        }
    public:
        class attribute 
        {
            // a set of pointers into the main text buffer - going for zero copy, for speed
        public:
            attribute() {};
            bool matchName(const char *test) const 
            {
                return !strcmp(test,name); // return true on match
            }
            const char *getName() const 
            {
                return name;
            }

            // handle XML escapes on demand
            const char *getValuePtr(XMLUnescapeBehavior_t Unescape = XMLUnescapeDefault) const 
            {
                if (Unescape == NoXMLUnescape) 
                    needsUnescape = false;
                else if (needsUnescape) {
                    unescapeXML(value);
                    needsUnescape = false;
                }
                return value;
            }
            std::string getValue(XMLUnescapeBehavior_t Unescape = XMLUnescapeDefault) const {
                return std::string(getValuePtr(Unescape));
            }

            // cast-to-type
            template< typename T >
            inline T valueAs( XMLUnescapeBehavior_t Unescape ) const
            {
                return textToValue<T>(getValuePtr(Unescape));
            }

            inline size_t valueAs( XMLUnescapeBehavior_t Unescape ) const
            {
                return (size_t)strtoul(getValuePtr(Unescape),NULL,10);
            }

            friend class Attributes;
        protected:
            const char *name; // attribute name - a pointer into main text buffer
            char *value; // also a pointer into main text buffer, content may change during read
            mutable bool needsUnescape; // may change during read
            void set(const char *_name, char *_value, bool _needsUnescape) 
            {
                name = _name;
                value = _value;
                needsUnescape = _needsUnescape;
            }
        }; // class attribute

    public:
            typedef std::vector<attribute> attribute_list;
    protected:
            mutable attribute_list attrs; // may change even in a const function due to lazy evaluation
    public:
            attribute_list::const_iterator begin() const 
            {
                    access(); // have we actually parsed the attributes text yet?
                    return attrs.begin();
            }
            attribute_list::const_iterator end() const 
            {
                    access(); // have we actually parsed the attributes text yet?
                    return attrs.end();
            }
            attribute_list::const_iterator find(const std::string &name) const 
            {
                attribute_list::const_iterator it;
                for (it = begin(); it != end() ; it++ ) 
                {
                    if (it->matchName(name.c_str())) 
                        break; // found it
                }
                return it;
            }
    protected:

            PWIZ_API_DECL void parseAttributes(std::string::size_type& index) const;

            void access() const 
            { // don't parse attributes until asked to
                test_invariant(); // everything correct?
                if (firstread) {
                    firstread = false;
                    parseAttributes(index);
                }
                test_invariant(); // everything correct?
            }

    public:
            const attribute *findAttributeByName(const char *name) const 
            {
                access(); // parse the buffer if we haven't already
                for (attribute_list::const_iterator it=attrs.begin();it!=attrs.end();it++) 
                {
                    if (it->matchName(name)) 
                        return &(*it);
                }
                return NULL;
            }

            // return value for name if any, or NULL
            const char *findValueByName(const char *name,XMLUnescapeBehavior_t Unescape = XMLUnescapeDefault) const 
            {
                const attribute *attr = findAttributeByName(name);
                if (attr) 
                    return attr->getValuePtr(Unescape);
                return NULL;
            }

    };
    typedef boost::iostreams::stream_offset stream_offset; 

    virtual Status processingInstruction(const std::string& name,
                                         const std::string& data,
                                         stream_offset position) {return Status::Ok;}

    virtual Status startElement(const std::string& name,
                                const Attributes& attributes,
                                stream_offset position) {return Status::Ok;}

    virtual Status endElement(const std::string& name,
                              stream_offset position) {return Status::Ok;}

    virtual Status characters(const SAXParser::saxstring& text,
                              stream_offset position) {return Status::Ok;}

    Handler() : parseCharacters(false), autoUnescapeAttributes(true), autoUnescapeCharacters(true), version(0) {}
    virtual ~Handler(){}

    protected:

    template <typename T>
    inline T& getAttribute(const Attributes& attributes,
                    const char * name,
                    T& result,
                    XMLUnescapeBehavior_t Unescape,
                    T defaultValue = T()) const
    {
        const Attributes::attribute *attr = attributes.findAttributeByName(name);
        if (attr) 
            result = attr->valueAs<T>(Unescape);
         else 
            result = defaultValue;
        return result;
    }

    const char *getAttribute(const Attributes& attributes,
                    const char * name,
                    XMLUnescapeBehavior_t Unescape,
                    const char * defaultValue = NULL) const
    {
        const char *val = attributes.findValueByName(name,Unescape);
        if (!val) 
            val = defaultValue;
        return val;
    }


    // general case using default unescape behavior 
    template <typename T>
    inline T& getAttribute(const Attributes& attributes,
        const char *name,
        T& result) const
    {
        const Attributes::attribute *attr = attributes.findAttributeByName(name);
        if (attr) 
            result = attr->valueAs<T>(XMLUnescapeDefault);
        else 
            result = T();
        return result;
    }

    inline std::string& getAttribute(const Attributes& attributes,
        const char *name,
        std::string& result) const
    {
        const Attributes::attribute *attr = attributes.findAttributeByName(name);
        if (attr) 
            result = attr->getValuePtr(XMLUnescapeDefault);
        else 
            result = "";
        return result;
    }

    // general case using default unescape behavior 
    template <typename T>
    inline T& getAttribute(const Attributes& attributes,
        const std::string &name,
        T& result,
        T defaultValue = T()) const
    {
        const Attributes::attribute *attr = attributes.findAttributeByName(name.c_str());
        if (attr) 
            result = attr->valueAs<T>(XMLUnescapeDefault);
        else 
            result = defaultValue;
        return result;    
    }
};


///
/// Extract a single XML element from the istream, sending SAX events to the handler.
///
/// Behavior:
///
/// - Parser returns when it completes reading of the first element it encounters.
///
/// - Parser returns immediately if the Handler returns Status::Done when handling an event.
///
/// - On startElement(), Handler may delegate handling to a sub-Handler, which will receive
///   the same startElement() event.  The sub-Handler pointer will remain on the parser's 
///   Handler stack until it handles the corresponding endElement().  Caution: The sub-Handler 
///   pointer must remain valid while it is on the Handler stack, so it cannot point to
///   a local object that goes out of scope when Handler:startElement() returns. 
///
/// Notes:
/// - Start tags with end marker '/' generate two events, e.g. <br/> will generate events
///   startElement("br", ...) and endElement("br").
///
PWIZ_API_DECL void parse(std::istream& is, Handler& handler);


} // namespace SAXParser


/// Returns the root element from an XML buffer;
/// throws runtime_error if no element is found.
PWIZ_API_DECL std::string xml_root_element(const std::string& fileheader);

/// Returns the root element from an XML stream;
/// throws runtime_error if no element is found.
PWIZ_API_DECL std::string xml_root_element(std::istream& is);

/// Returns the root element from an XML file;
/// throws runtime_error if no element is found.
PWIZ_API_DECL std::string xml_root_element_from_file(const std::string& filepath);


/// Decodes any characters encoded with their hexadecimal value,
/// e.g. "_x0020_" decodes as " "
/// This override modifies the input string in place and returns its reference.
PWIZ_API_DECL std::string& decode_xml_id(std::string& str);


/// Decodes any characters encoded with their hexadecimal value,
/// e.g. "_x0020_" decodes as " "
/// This override modifies and returns a copy of the input string.
PWIZ_API_DECL std::string decode_xml_id_copy(const std::string& str);


} // namespace minimxml
} // namespace pwiz


#endif // _SAXPARSER_HPP_


