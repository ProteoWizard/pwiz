//
// CVTranslator.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//
//


#ifndef _CVTRANSLATOR_HPP_
#define _CVTRANSLATOR_HPP_


#include "cv.hpp"
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace msdata {


/// translates text to CV terms
class CVTranslator
{
    public:

    /// constructor -- dictionary includes all 
    /// CV term names and exact_synonyms 
    CVTranslator();

    /// insert a text-cvid pair into the dictionary
    void insert(const std::string& text, CVID cvid);

    /// translate text -> CVID
    CVID translate(const std::string& text) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    CVTranslator(CVTranslator&);
    CVTranslator& operator=(CVTranslator&);
};


} // namespace msdata
} // namespace pwiz


#endif // _CVTRANSLATOR_HPP_

