//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#define PWIZ_SOURCE

#include "IPIFASTADatabase.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>
#include <cstdlib>


namespace pwiz {
namespace proteome {

IPIFASTADatabase::const_iterator IPIFASTADatabase::begin()
{
    return records().begin();
}

IPIFASTADatabase::const_iterator IPIFASTADatabase::end()
{
    return records().end();
}

class IPIFASTADatabase::Impl
{
    public:
    Impl(const string& filename);
    const vector<Record>& records() const {return records_;}

    private:
    vector<Record> records_;

    void readRecords(istream& is);
};


IPIFASTADatabase::Impl::Impl(const string& filename)
{
    ifstream is(filename.c_str()); 
    if (!is) throw runtime_error(("[IPIFASTADatabase::Impl::Impl()] Unable to open file " + filename).c_str());

    //cout << "Reading records from " << filename << "..." << flush;
    readRecords(is);
    //cout << "done.\n";
}


void IPIFASTADatabase::Impl::readRecords(istream& is)
{
    Record* current = 0;

    for (string buffer; getline(is, buffer); )
    {
      //      std::cout<<"||||"<<buffer<<"$$$$"<<std::endl;
        if (buffer.find(">IPI:IPI") == 0)
        {
            // start a new record, and set current pointer
            records_.push_back(Record(atoi(buffer.c_str()+8)));
            current = &records_.back(); 
	    current->fullSeqDescription = buffer;
            // get protein ID (e.g. IPI number, or whatever precedes first pipe)
            // TODO: Increase flexibility (e.g. what if no pipe, get all ids between pipes as different 
            // variables, etc ... 

            const char* pipe = "|";
            if (buffer.find(pipe) != string::npos)
                {
                    const size_t& pipeLocation = buffer.find(pipe);                    
                    char protein[30];
                    memset(protein, '\0', 30);

                    buffer.copy(protein, pipeLocation - 1, 1);  // skip initial '>'
                    current->faID = protein;

                }
            
            else current->faID ="unknown";

        }

	else if(buffer.find(">") == 0){  //there is likely a better way to handle these cases.
	              // start a new record, and set current pointer
            records_.push_back(Record(atoi(buffer.c_str())));
            current = &records_.back(); 
	    current->fullSeqDescription = buffer;
            // get protein ID (e.g. IPI number, or whatever precedes first space)
            // TODO: Increase flexibility (e.g. what if no pipe, get all ids between pipes as different 
            // variables, etc ... 

            const char* space = " ";
            if (buffer.find(space) != string::npos)
	      {
		const size_t& spaceLocation = buffer.find(space);                    
		char protein[50];
		memset(protein, '\0', 50);
		
		buffer.copy(protein, spaceLocation - 1, 1);  // skip initial '>'
		current->faID = protein;
		
	      }
            
            else current->faID ="unknown";
	}
        else if (current)
        {
            // update current record with next line of the sequence
	   if(buffer.find("*") != string::npos){
	     const size_t& stringLocation = buffer.find("*");                    
	     buffer.erase(stringLocation);
	   } 
	   current->sequence += buffer;
        }
    }
}


PWIZ_API_DECL IPIFASTADatabase::IPIFASTADatabase(const string& filename) : impl_(new Impl(filename)) {}
PWIZ_API_DECL IPIFASTADatabase::~IPIFASTADatabase(){} // auto-destruction of impl_
PWIZ_API_DECL const vector<IPIFASTADatabase::Record>& IPIFASTADatabase::records() const {return impl_->records();} 


PWIZ_API_DECL ostream& operator<<(ostream& os, const IPIFASTADatabase::Record& record)
{
    os << ">IPI:IPI" << setfill('0') << setw(8) << record.id << endl;
    for (unsigned int i=0; i<record.sequence.size(); i++)
    {
        os << record.sequence[i];
        if (i%60==59) os << endl;
    }
    return os;
}


} // namespace proteome
} // namespace pwiz

