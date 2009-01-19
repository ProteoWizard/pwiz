//
// Original Author: Parag Mallick
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef INCLUDED_PROTEOTYPIC_RESULT
#define INCLUDED_PROTEOTYPIC_RESULT

#include <string>
using std::string;

struct ProteotypicResult
{
  string _protein;
  string _peptide;
  double _cz_esiResult;
  double _cz_maldiResult;
  double _isb_esiResult;
  double _isb_icatResult;

  ProteotypicResult() 
    : _protein(""),
      _peptide(""),
      _cz_esiResult(0.00),
      _cz_maldiResult(0.00),
      _isb_esiResult(0.00),
      _isb_icatResult(0.00)
  {};

};

#endif
