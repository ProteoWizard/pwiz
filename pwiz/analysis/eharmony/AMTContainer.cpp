///
/// AMTContainer.cpp
///

#include "AMTContainer.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/data/misc/MinimumPepXML.cpp" // TODO: DON"T

using namespace pwiz;
using namespace eharmony;

void AMTContainer::merge(const AMTContainer& that)
{
    _fdf->merge(*that._fdf);
    _pidf->merge(*that._pidf);
    _id += that._id;
    cout << "size: " << _pidf->getAllContents().size() << endl;

}

void AMTContainer::write(XMLWriter& writer) const
{
    vector<boost::shared_ptr<SpectrumQuery> > peptides = _pidf->getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = peptides.begin();
    
    for(; it != peptides.end(); ++it)
        {
            (*it)->write(writer);

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
	      amtContainer->_sqs.push_back(boost::shared_ptr<SpectrumQuery>( new SpectrumQuery()));
	      _handlerSpectrumQuery.spectrumQuery = amtContainer->_sqs.back().get();
	      
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
    cout << "constructing pidf .. " << endl;
    PidfPtr pidf(new PeptideID_dataFetcher(_sqs));
    _pidf = pidf;

}

bool AMTContainer::operator==(const AMTContainer& that)
{
    return *_pidf == *that._pidf
      && *_fdf == *that._fdf 
      && _pm == that._pm
      && _f2pm == that._f2pm
      && _sqs == that._sqs;


}

bool AMTContainer::operator!=(const AMTContainer& that)
{
    return !(*this == that);

}
