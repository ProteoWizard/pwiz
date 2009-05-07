///
/// AMTContainer.cpp
///

#include "AMTContainer.hpp"
#include "Peptide2FeatureMatcher.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/data/misc/MinimumPepXML.cpp" // TODO: DON"T

using namespace pwiz;
using namespace eharmony;

void AMTContainer::merge(const AMTContainer& that)
{
    _fdf.merge(that._fdf);
    _pidf.merge(that._pidf);
    _mdf.merge(that._mdf);

}

void AMTContainer::write(XMLWriter& writer) const
{
    vector<SpectrumQuery> peptides = _pidf.getAllContents();
    vector<SpectrumQuery>::iterator it = peptides.begin();
    
    for(; it != peptides.end(); ++it)
        {
	    it->write(writer);

	}

    vector<Match> matches = _mdf.getAllContents();
    vector<Match>::iterator m_it = matches.begin();
    for(; m_it != matches.end(); ++m_it)
        {
	    m_it->spectrumQuery.write(writer);

	}

}

struct HandlerAMTContainer : public SAXParser::Handler
{
    AMTContainer* amtContainer;
    HandlerAMTContainer(AMTContainer* _amtContainer = 0) : amtContainer(_amtContainer){}

    virtual Status startElement(const string& name,
			        const Attributes& attributes,
			        stream_offset position)
    {
      if (name == "spectrum_query")
	  {
	      amtContainer->_sqs.push_back(SpectrumQuery());
	      _handlerSpectrumQuery.spectrumQuery = &amtContainer->_sqs.back();
	      
	      return Handler::Status(Status::Delegate, &_handlerSpectrumQuery);
	  }

      else 
	{
	  throw runtime_error(("[HandlerAMTContainer] Unexpected element name: " + name).c_str());
	  return Handler::Status::Done;
      
	}
    }  

private:

    HandlerSpectrumQuery _handlerSpectrumQuery;

  
};

void AMTContainer::read(istream& is)
{
    HandlerAMTContainer handlerAMTContainer(this);
    parse(is, handlerAMTContainer);
    
}
