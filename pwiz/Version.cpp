//
// Version.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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
#include "pwiz/Version.hpp"
#include <sstream>
#ifdef PWIZ_USER_VERSION_INFO_H // in case you need to add any info version of your own
#include PWIZ_USER_VERSION_INFO_H  // must define PWIZ_USER_VERSION_INFO_H_STR for use below
#endif
namespace pwiz {


using std::string;


int Version::Major()                {return 1;}
int Version::Minor()                {return 5;}
int Version::Revision()             {return 0;}
string Version::LastModified()      {return "2/5/2008";}
string Version::str()               
{
	std::ostringstream v;
	v << Major(); v << ".";
	v << Minor(); v << ".";
	v << Revision();
#ifdef PWIZ_USER_VERSION_INFO_H
	v << " (" << PWIZ_USER_VERSION_INFO_H_STR << ")";
#endif
	return v.str();
}

} // namespace pwiz


