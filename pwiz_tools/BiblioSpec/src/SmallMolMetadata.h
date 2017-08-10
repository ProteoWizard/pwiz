//
// $Id: PSM.h 9228 2015-12-22 00:28:23Z kaipot $
//
//
// Original author: Brian Pratt <bspratt@u.washington.edu>
//
// Copyright 2016 University of Washington - Seattle, WA 98195
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

#pragma once

#include <string>
#include <boost/algorithm/string/replace.hpp>

/**
 * \struct SmallMolMetadata
 * \brief Information peculiar to small molecules
 */
struct SmallMolMetadata {
  std::string inchiKey; ///< this is our baseline identifier
  std::string precursorAdduct; ///< should agree with charge
  std::string chemicalFormula; ///< formula without adduct
  std::string moleculeName; ///< friendly name
  std::string otherKeys; //< of form <idType:value>\t<idType:value> etc as in "CAS:58-08-2\tinchi:1S/C8H10N4O2/c1-10-4-9-6-5(10)7(13)12(3)8(14)11(6)2/h4H,1-3H3"

  SmallMolMetadata(){};

  SmallMolMetadata(const SmallMolMetadata &rhs)
  {
      *this = rhs;
  }

  virtual ~SmallMolMetadata(){ };

  // Get a comma seperated list of column names with type decls
  static const char *sql_col_decls() {  // This order matters - it's used for sorting
      return "moleculeName VARCHAR(128), chemicalFormula VARCHAR(128), precursorAdduct VARCHAR(128), inchiKey VARCHAR(128), otherKeys VARCHAR(128)";
  }

  // Get a comma seperated list of column names without the type decls
  static string sql_cols() { 
      string result(sql_col_decls());
      boost::replace_all(result, " VARCHAR(128)", "");
      return result;
  }

  SmallMolMetadata& operator= (const SmallMolMetadata& rhs)
  {
    inchiKey = rhs.inchiKey;
	precursorAdduct = rhs.precursorAdduct;
    chemicalFormula = rhs.chemicalFormula;
    moleculeName = rhs.moleculeName;
    otherKeys = rhs.otherKeys;
    return *this;
  }

  bool IsCompleteEnough() const
  {
	  // Need at least name, adduct and formula to be a useful description
	  return (!moleculeName.empty() && !precursorAdduct.empty() && !chemicalFormula.empty());
  }

  void clear(){
    inchiKey.clear();
	precursorAdduct.clear();
    chemicalFormula.clear();
    moleculeName.clear();
    otherKeys.clear();
  };

};

