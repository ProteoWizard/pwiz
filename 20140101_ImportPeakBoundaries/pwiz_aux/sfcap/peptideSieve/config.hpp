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


#ifndef CONFIG_HPP
#define CONFIG_HPP

#include <stdio.h>
#include <math.h>
#include <iostream>
#include <fstream>
#include <string>
#include <vector>

#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
using namespace boost::program_options;

#include "pwiz/utility/proteome/IPIFASTADatabase.hpp"
#include "pwiz/utility/proteome/Digestion.hpp"
#include "pwiz/utility/misc/DateTime.hpp"

using namespace std;
using std::string;
using namespace std;
using namespace pwiz;
using namespace pwiz::proteome;
using boost::shared_ptr;


struct Config
{
  vector<string> _filenames;
  string _outputPath;
  string _extension;
  string _propertyFile;
  string _outputFile;
  string _inputFormat;
  int _minSeqLength,_maxSeqLength;
  double _minMass,_maxMass;
  int _numAllowedMisCleavages;
  //    int _numPeptidesPerProt;
  string _experimentalDesign;
  bool _savePropertiesFile;
  double _pValue;

  Config()
    : _outputPath(""),
       _extension(""),
       _propertyFile("properties.txt"), 
       _outputFile(""), 
       _inputFormat("FASTA"),
       _minSeqLength(6),
       _maxSeqLength(40),
       _minMass(400),
       _maxMass(3000),
       _numAllowedMisCleavages(0),
      //      _numPeptidesPerProt(3),
      _experimentalDesign("PAGE_MALDI.txt"),
      _savePropertiesFile(false),
      _pValue(.80)
  {};
  
  string outputFileName(const string & filename) const;
  string propertyFileName(const string & filename) const;
};

#endif
