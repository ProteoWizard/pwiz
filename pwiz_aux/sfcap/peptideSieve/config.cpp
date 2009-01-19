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


#include "config.hpp"

string Config::propertyFileName(const string& filename) const
{
  namespace bfs = boost::filesystem;
  string tmpOutputPath;

  if(_savePropertiesFile == true){
    if(_outputPath.size() == 0){
      tmpOutputPath = ".";
    }
    else{
      tmpOutputPath = _outputPath;
    }
    
    string newFilename = bfs::basename(filename) + ".properties.txt";
    bfs::path fullPath = bfs::path(tmpOutputPath) / newFilename;
    return fullPath.string(); 
  }
  return "";
}

string Config::outputFileName(const string& filename) const
{
  namespace bfs = boost::filesystem;
  string tmpOutputPath;

  if(_outputFile.size() != 0){
    if(_outputPath.size() == 0){ //check to see if outputPath is set
      tmpOutputPath = ".";
    }
    bfs::path fullPath = bfs::path(tmpOutputPath) / _outputFile;
    return fullPath.string(); 
  }
  else if((_outputPath.size()==0) &&
	  (_extension.size()==0) &&
	  (_outputFile.size()==0)){

    return "";
  }
  else{
    string tmpExtension;
    string tmpOutputPath;

    if(_extension.size() == 0){
      tmpExtension = ".ptps.out";
    }
    else{
      tmpExtension = _extension;
    }
    if(_outputPath.size() == 0){
      tmpOutputPath = ".";
    }
    else{
      tmpOutputPath = _outputPath;
    }
    
    string newFilename = bfs::basename(filename) + tmpExtension;
    bfs::path fullPath = bfs::path(tmpOutputPath) / newFilename;
    return fullPath.string(); 
  }
  return "";
}
