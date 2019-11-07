//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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


/**
 * \file DelimitedFileReader.h
 *
 * A templated class for parsing delimited files.  The instantiator of
 * the object chooses the data structure that will be used for
 * collecting information from the parser.  Instantiator also passes
 * the reader an object that will be collecting the data after each
 * line has been parsed.  Can define the character that delimits
 * columns in the file, default is comma.
 */
#pragma once

#include "pwiz/utility/misc/Stream.hpp"
#include <vector>
#include "Verbosity.h"
#include "BlibException.h"
#include "DelimitedFileConsumer.h"
#include "boost/tokenizer.hpp"

typedef boost::tokenizer< boost::escaped_list_separator<char> > CsvTokenizer;
typedef boost::tokenizer< boost::escaped_list_separator<char> >::iterator
                                                           CsvTokenIterator;

namespace BiblioSpec{

/**
 * A class for defining a column to be parsed from a file.  Stores the
 * column name, the location in the file (column index), and a
 * function pointer which is used to store the column value in a data
 * storage object.
 */
template <class STORAGE_T> class ColumnTranslator{
 public:
    std::string name_;
    int position_;
    void (*inserter)(STORAGE_T& le, const std::string& value); 
    
 ColumnTranslator(const char* name, 
                  int pos, 
                  void (*fun)(STORAGE_T&, const std::string&))
     : name_(name), position_(pos), inserter(fun) { };
    
 // for sorting a list of ColumnTranslators by position
 friend bool operator< (const ColumnTranslator& left, 
                        const ColumnTranslator& right)
 {
     return (left.position_ < right.position_);
 }
};

/**
 * The DelimitedFileReader class.
 */
template <typename STORAGE_TYPE> class DelimitedFileReader {

 private:
    std::string filename_; 
    fstream delimFile_;
    // defines the delimiters and escape characters used to parse
    boost::escaped_list_separator<char>* separatorFunction_;
    // columns to grab, must be in the file
    std::vector< ColumnTranslator<STORAGE_TYPE> > targetColumns_;
    // optional columns to grab 
    std::vector< ColumnTranslator<STORAGE_TYPE> > optionalColumns_;
    STORAGE_TYPE lineEntry_; // object for storing one line from the file
    DelimitedFileConsumer<STORAGE_TYPE>* fileConsumer_; // where to send data
    int curLineNumber_;


 public:
    /**
     * The only constructor requries a pointer to a file consumer.
     */
     DelimitedFileReader(DelimitedFileConsumer<STORAGE_TYPE>* fc) 
     : separatorFunction_(NULL), fileConsumer_(fc), curLineNumber_(0) {};

    /**
     * Destructor closes file if open.
     */
    ~DelimitedFileReader(){
        delimFile_.close();
    }

    /**
     * Add the name of a column to look for and the function to call
     * when that column is read.  These columns are required to be in
     * the file and parsing will exit with an error if they are not
     * found.
     */
    void addRequiredColumn(const char* name,
                           void (*fun)(STORAGE_TYPE&, const std::string&)   ){ 
      targetColumns_.push_back(ColumnTranslator<STORAGE_TYPE>(name, -1, fun));

    }

    /**
     * Add the name of a column to look for and the function to call
     * when that column is read.  These columns are optional and will
     * be ignored if missing.
     */
    void addOptionalColumn(const char* name,
                           void (*fun)(STORAGE_TYPE&, const std::string&)   ){ 
      optionalColumns_.push_back(ColumnTranslator<STORAGE_TYPE>(name, -1, fun));
    }

    /**
     * Define what characters will separate columns, quote text, and
     * escape quotes and separators.  
     * 
     * Defaults are ',' as delimiter, " to quote text,  and \ to
     * excape characters.  With the defaults this string would break
     * up into 4 fields 
     *
     * field 1,"field2 with a ,comma",field 3 with a \" quote,field 4
     */
    void defineSeparators(char delimiter = ',', char quote =  '\"', 
                          char escape = '\\'){
        separatorFunction_  = 
            new boost::escaped_list_separator<char>(escape, delimiter, quote);
    }

    /**
     * Opens the file, parses the header, reads each line, inserting
     * the target columns into the selected data structure, and passes
     * the data structure to the consumer at the end of each line.
     */
    void parseFile(const char* filename){
        // open file
        openFile(filename);

        // parse header
        std::string line;
        getline(delimFile_, line);
        curLineNumber_++;
        parseHeader(line);

        // read remaining lines
        readRemainingLines();
    }

 private:
    /**
     * Open the given filename for reading and throw an exception on
     * error.
     */
    void openFile(const char* filename){
        Verbosity::debug("DelimitedFileReader opening '%s'.", filename);

        delimFile_.open(filename, std::fstream::in);
        if( delimFile_.bad() ){
            Verbosity::error("DelimitedFileReader could not open '%s'.", 
                             filename);
        };
    };

    /**
     * Read the first line of the file, looking for the requested
     * columns.  Update their translators with their positions in this file.
     */
    void parseHeader(const std::string& line){
        if(separatorFunction_ == NULL ){
            defineSeparators();
        }

        CsvTokenizer lineParser(line, *separatorFunction_);
        int colPosition = 0;
        // for each column in the header line
        CsvTokenIterator token = lineParser.begin();
        for(; token != lineParser.end(); ++token){
            // check each target column name for a match
            for(size_t i = 0; i < targetColumns_.size(); i++){
                if( *token == targetColumns_[i].name_ ){
                    targetColumns_[i].position_ = colPosition;
                }
            }

            // also check each optional column name
            for(size_t i = 0; i < optionalColumns_.size(); i++){
                if( *token == optionalColumns_[i].name_ ){
                    optionalColumns_[i].position_ = colPosition;
                }
            }
            colPosition++;
        } // next token in the line

        // check that all required columns were in the file
        for(size_t i = 0; i < targetColumns_.size(); i++){
            if( targetColumns_[i].position_ < 0 ){
                Verbosity::error("Failed to find required column named '%s'.",
                                 targetColumns_[i].name_.c_str());
            }
        }

        // add any optional columns we found
        for(size_t i = 0; i < optionalColumns_.size(); i++){
            if( optionalColumns_[i].position_ >= 0 ){
                targetColumns_.push_back(optionalColumns_[i]);
            }
        }

        // sort by column number so they can be fetched in order
        sort(targetColumns_.begin(), targetColumns_.end());
    }

    /**
     * After header has been parsed, read each of the remaining lines,
     * sending the lineEntry_ to the file consumer after each line.
     */
    void readRemainingLines(){
        // read the first non-header line
        std::string line;
        bool parseSuccess = true;
        std::string errorMsg;
        
        while( ! delimFile_.eof() ){
            getline(delimFile_, line);
            curLineNumber_++;

            if (line.empty() && delimFile_.eof())
                break;

            size_t colListIdx = 0;  // go through all target columns
            int lineColNumber = 0;  // compare to all file columns

            STORAGE_TYPE lineEntry;//create an empty container for each line
            try{
                CsvTokenizer lineParser(line, *separatorFunction_);
                CsvTokenIterator token = lineParser.begin();
                // for each token in this line
                for(; token != lineParser.end(); ++token) {
                    // if it's in the right position
                    if(lineColNumber == targetColumns_[colListIdx].position_ ){

                        // insert the value into the proper field
                        targetColumns_[colListIdx].inserter(lineEntry,(*token));
                        colListIdx++; // next target column

                        if( colListIdx == targetColumns_.size() ){
                            break;
                        }
                    }
                    lineColNumber++; // next token in the line
                }
                
                // end of this line, pass back the data
                fileConsumer_->addDataLine(lineEntry);

            } catch (BlibException& e) {
                parseSuccess = false;
                errorMsg = e.what();
            } catch (std::exception& e) {
                parseSuccess = false;
                errorMsg = e.what();
            } catch (std::string& s) {
                parseSuccess = false;
                errorMsg = s;
            } catch (...){
                errorMsg = "Unknown exception";
            }

            if(!parseSuccess){
                BlibException e(false, "%s caught at line %d, column %d",
                                errorMsg.c_str(), curLineNumber_, 
                                lineColNumber + 1);
                throw e;
            }
        }
    }


};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
