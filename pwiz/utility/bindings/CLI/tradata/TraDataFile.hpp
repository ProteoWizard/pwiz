//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
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


#ifndef _TRADATAFILE_HPP_CLI_
#define _TRADATAFILE_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "TraData.hpp"
#include "pwiz/data/tradata/TraDataFile.hpp"
#pragma warning( pop )

#include "pwiz/data/tradata/DefaultReaderList.hpp"

namespace pwiz {
namespace CLI {
namespace tradata {


/// TraData object plus file I/O
public ref class TraDataFile : public TraData
{
	DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::tradata, TraDataFile, TraData);
	
	public:
	
	/// <summary>
    /// constructs TraData object backed by file
    /// </summary>
	TraDataFile(System::String^ path);
	
	/// <summary>
    /// data format for write()
    /// </summary>
	enum class Format {Format_Text, Format_traML};
	
	ref class WriteConfig
	{
		public:
		property Format format;
		property bool gzipped; // if true, file is written as .gz
		
		WriteConfig()
		{
			format = Format::Format_traML;
		}
	};
	
	/// <summary>
    /// static write function for any TraData object with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    static void write(TraData^ trad, System::String^ filename);

    /// <summary>
    /// static write function for any TraData object with the specified configuration
    /// </summary>
    static void write(TraData^ trad, System::String^ filename, WriteConfig^ config);
	
	/// <summary>
    /// member write function with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    void write(System::String^ filename);

    /// <summary>
    /// member write function with the specified configuration
    /// </summary>
    void write(System::String^ filename, WriteConfig^ config);
	
};
} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _TRADATAFILE_HPP_CLI_