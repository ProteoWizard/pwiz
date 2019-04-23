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

/*
 * A tester function to evaluate how well the WeibullPvalue class is
 * estimating parameters from a given set of numbers.  Takes as input
 * a file containing real numbers (one per row) and outputs the
 * estimated eta, beta, and shift parameters.
 */


#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include "CommandLine.h"
#include "WeibullPvalue.h"

namespace ops = boost::program_options;

void ParseCommandLine(const int argc,
                      char** const argv,
                      ops::variables_map& options_table);

int main(int argc, char** argv){
  bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
  BiblioSpec::enable_utf8_path_operations();

  // delcare storage for options values
  ops::variables_map options_table;

  ParseCommandLine(argc, argv, options_table);

  /*
  string usage = "TestWeibull <data file>";

  if( argc != 2 ){
    cerr << usage << endl;
    exit(1);
  }
  string dataFileName = argv[1];
  */
  int c = options_table.count("data-file");

  string dataFileName = options_table["data-file"].as<string>();
  ifstream dataFile(dataFileName.c_str());
  if( ! dataFile.is_open() ){
    cerr << "Could not open " << dataFileName << endl;
    exit(1);
  }

  vector<double> scores;
  double curScore;
  while ( !dataFile.eof() ){
    dataFile >> curScore ;
    scores.push_back(curScore);
  }
  // always adds the last score twice
  scores.pop_back();

  int numPoints = scores.size();
  cerr << "Read " << numPoints << " values from " << dataFileName << endl;
  dataFile.close();


  WeibullPvalue estimator(options_table);

  estimator.estimateParams(scores);
  cout << "eta\t" << estimator.getEta()
       << "\tbeta\t" << estimator.getBeta()
       << "\tshift\t" << estimator.getShift()
       << endl;

  // also get the p-value for the largest value
  sort(scores.begin(), scores.end());
  double pval = estimator.computePvalue(scores.at(numPoints - 1)); 
  cout << "P-value of " << scores.at(numPoints-1) << " is " << pval << endl;
  int half_idx = numPoints/2;
  pval = estimator.computePvalue(scores.at(half_idx)); 
  cout << "P-value of " << scores.at(half_idx) << " is " << pval << endl;
}


void ParseCommandLine(const int argc,
                      char** const argv,
                      ops::variables_map& options_table){
  // define optional command line args
  options_description options("options");
  try {
    
    options.add_options()
      ("weibull-param-file,W",
       value<string>(),
       "Return estimated Weibull params for each spectrum in a file named ARG.")
      
      ("fraction-to-fit,f",
       value<double>()->default_value(0.5),
       "Fraction of scores to use in Weibull parameter estimation.")
      
      ("correlation-tolerance,c",
       value<double>()->default_value(0.1),
       //value<double>()->default_value(0.0005),
       "The amount by which the correlation can decrease from the max before halting the Weibull shift parameter estimation.")

      ("print-all-params,a",
       value<bool>()->default_value(false),
       "Print to stdout estimated parameters at each shift value.")
      ;

    // define required args
    vector<const char*> argNames;
    argNames.push_back("data-file");

    CommandLine parser("test-weibull", options, argNames, false);
    // TODO fix bug in CommandLine that doesn't work when false
    //CommandLine parser("test-weibull", options, argNames, true);
    parser.parse(argc, argv, options_table);

  } catch(exception& e) {
    cerr << "ERROR: " << e.what() << "." << endl;
    exit(1);
  } catch(...) {
    cerr << "Encountered exception while parsing command line." << endl;
    exit(1);
  }
}

