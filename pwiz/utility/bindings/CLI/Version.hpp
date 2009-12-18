//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _VERSION_HPP_CLI_
#define _VERSION_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "../../../data/msdata/Version.hpp"
#include "../../../analysis/Version.hpp"
#include "../../../data/proteome/Version.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {


namespace msdata
{
    /// <summary>
    /// version information for the msdata namespace
    /// </summary>
    public ref class Version
    {
        public:
        static int Major() {return pwiz::msdata::Version::Major();}
        static int Minor() {return pwiz::msdata::Version::Minor();}
        static int Revision() {return pwiz::msdata::Version::Revision();}
        static System::String^ LastModified() {return gcnew System::String(pwiz::msdata::Version::LastModified().c_str());}
        static System::String^ ToString() {return gcnew System::String(pwiz::msdata::Version::str().c_str());}
    };
}


namespace analysis
{
    /// <summary>
    /// version information for the analysis namespace
    /// </summary>
    public ref class Version
    {
        public:
        static int Major() {return pwiz::analysis::Version::Major();}
        static int Minor() {return pwiz::analysis::Version::Minor();}
        static int Revision() {return pwiz::analysis::Version::Revision();}
        static System::String^ LastModified() {return gcnew System::String(pwiz::analysis::Version::LastModified().c_str());}
        static System::String^ ToString() {return gcnew System::String(pwiz::analysis::Version::str().c_str());}
    };
}


namespace proteome
{
    /// <summary>
    /// version information for the proteome namespace
    /// </summary>
    public ref class Version
    {
        public:
        static int Major() {return pwiz::proteome::Version::Major();}
        static int Minor() {return pwiz::proteome::Version::Minor();}
        static int Revision() {return pwiz::proteome::Version::Revision();}
        static System::String^ LastModified() {return gcnew System::String(pwiz::proteome::Version::LastModified().c_str());}
        static System::String^ ToString() {return gcnew System::String(pwiz::proteome::Version::str().c_str());}
    };
}


}
}

#endif // _VERSION_HPP_CLI_
