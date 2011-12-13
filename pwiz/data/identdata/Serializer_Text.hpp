//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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


#ifndef _SERIALIZER_TEXT_HPP_
#define _SERIALIZER_TEXT_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"

namespace pwiz {
namespace identdata {

/// \class Serializer_Text
///
/// Serializer_Text reads in and writes out an id file in tab format.
///
class  PWIZ_API_DECL Serializer_Text
{
public:
    /// List of fields available.
    enum IdField
    {
        None=0,
        Scan=1,
        Rt=2,
        Mz=3,
        Charge=4,
        Score=5,
        ScoreType=6,
        Peptide=7,
        Protein=8,
        ProteinDescription=9,
        Last=ProteinDescription
    };

    static const std::string* getIdFieldNames();

    struct PWIZ_API_DECL Config ///< Controls the format of the text file.
    {
        bool headers;
        std::vector<IdField> fields;
        IdField sort;

        std::string recordDelim;
        std::string fieldDelim;

        Config();
        Config(const Config& config);
    };

    /// Constructor with Config
    Serializer_Text(const Config& config = Config());

    /// writes IdentData object to ostream as a text table
    void write(std::ostream& os, const IdentData& mzid,
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0) const;

    /// read in IdentData object from a delimited text fromat.
    void read(boost::shared_ptr<std::istream> is, IdentData& mzid) const;

    private:
    class Impl;
    boost::shared_ptr<Impl> impl_;
    Serializer_Text(Serializer_Text&);
    Serializer_Text& operator=(Serializer_Text&);
};


} // namespace identdata
} // namespace pwiz

#endif // _SERIALIZER_TEXT_HPP_
