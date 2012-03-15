/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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

using namespace std;
namespace ops = boost::program_options;

void ParseCommandLine(const int argc,
                      char** const argv,
                      ops::variables_map& options_table);

int main(int argc, char** argv){

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

