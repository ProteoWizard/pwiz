//
// Original Author: Parag Mallick
//
// Copyright 2009 Center for Applied Molecular Medicine 
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





// #include "fasta.h" // interface to fasta file handling
// using bioinfo::fasta;

 #include "classify.hpp"
 #include "config.hpp"


 void go(const Config& config)
 {
   cout << "outputPath: " << config._outputPath << endl;
   cout << "extension: " << config._extension << endl;
   namespace bfs = boost::filesystem;
   bfs::create_directories(config._outputPath);

   map<char,vector<double> > propertyMap;

   ProteotypicClassifier classificationBox(config, config._experimentalDesign);

  classificationBox.readPropertyMap(propertyMap, config._propertyFile);

  for (vector<string>::const_iterator it=config._filenames.begin(); it!=config._filenames.end(); ++it)
    {
      try
        {
          if((config._inputFormat == "FASTA")){
            classificationBox.processFASTAFile(*it, propertyMap);
          }
          else if((config._inputFormat == "TXT")){
            classificationBox.processTXTFile(*it, propertyMap);
          }
          else{
            throw runtime_error("File Format not understood - expected input either FASTA or TXT");
          }
        }
      catch (exception& e)
        {
          cout << e.what() << endl;
          cout << "Error processing file " << *it << endl; 
        }
      catch (...)
        {
          cout << "Unknown error.\n";
          cout << "Error processing file " << *it << endl; 
        }
    }
}



Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: peptideSieve [options] [files]\n"
          << "PeptideSieve: Identify Proteotypic Peptides from a FASTA or TXT file.\n"
          << "Version - 0.6"<<endl;

    Config config;
    string filelistFilename;

    po::options_description od_config("Options");
    od_config.add_options()
      ("outputDirectory,O",
       po::value<string>(&config._outputPath)->default_value(config._outputPath),
       "\n: set output directory")
      ("outputExtension,e",
       po::value<string>(&config._extension)->default_value(config._extension),
       "\n: set extension for output files")
      ("outputFile,o",
       po::value<string>(&config._outputFile)->default_value(config._outputFile),
       "\n: output file name if not input.extension")      
      ("propertyFile,P", //WCH: it was small p conflicting with pValue!!!
                         //     most users will want to change pValue then property file
       po::value<string>(&config._propertyFile)->default_value(config._propertyFile),
       "\n: set property file")
      ("inputFormat,f",
       po::value<string>(&config._inputFormat)->default_value(config._inputFormat),
       "\n: FASTA or TXT, specifying input format")
      ("minSeqLength,l",
       po::value<int>(&config._minSeqLength)->default_value(config._minSeqLength),
       "\n: minimum sequence length to consider")
      ("maxSeqLength,L",
       po::value<int>(&config._maxSeqLength)->default_value(config._maxSeqLength),
       "\n: maximum sequence length to consider")
      ("minMass,m",
       po::value<double>(&config._minMass)->default_value(config._minMass),
       "\n: minimum mass to consider")
      ("maxMass,M",
       po::value<double>(&config._maxMass)->default_value(config._maxMass),
       "\n: maximum mass to consider")
      ("numAllowedMisCleavages,c",
       po::value<int>(&config._numAllowedMisCleavages)->default_value(config._numAllowedMisCleavages),
       "\n: maximum number of miscleavages to consider")
      //      ("numPeptidesPerProt,n",
      //       po::value<int>(&config._numPeptidesPerProt)->default_value(config._numPeptidesPerProt),
      //       ": maximum number of peptides to return")
      ("saveConvertedFile,s",
       "\n: save the converted propertyFile")
      ("help,h",
       "\n: display usage information")
      ("experimentalDesign,d",
       po::value<string>(&config._experimentalDesign)->default_value(config._experimentalDesign),
       "\n: which design to return, any of the following, in quotes, comma separated \"PAGE_MALDI.txt,PAGE_ESI.txt,MUDPIT_ESI.txt,MUDPIT_ICAT.txt\"")
      ("pValue,p",
       po::value<double>(&config._pValue)->default_value(config._pValue),
       "\n: only return peptides with p values greater than X");

    // append options description to usage string

    usage << od_config;
    usage << endl;
    usage << "example usages:" <<endl;
    usage << "\t" << "Simple Run with Fasta : PeptideSieve shortExample.tfa"<<endl;
    usage << "\t" << "Simple Run with txt: PeptideSieve -f TXT example.txt"<<endl;
    usage << "\t" << "Specify Classifiers: PeptideSieve -d \"MUDPIT_ESI,PAGE_MALDI\" -f TXT example.txt"<<endl;
    usage << "\t" << "Make Properties File and Quit: PeptideSieve -d "" -s -f TXT example.txt"<<endl;

    // handle positional arguments

    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);

    // parse command line

    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // remember filenames from command line

    if (vm.count("saveConvertedFile")){//add option to save properties file
      config._savePropertiesFile = true;
    }    

    if (vm.count(label_args))
      config._filenames = vm[label_args].as< vector<string> >();
    
    
    // parse filelist if required
    
    if (!filelistFilename.empty()){
      ifstream is(filelistFilename.c_str());
      while (is)
	{
	  string filename;
	  getline(is, filename);
	  if (is) config._filenames.push_back(filename);
	}
    }
    
    // check stuff
    
    if ((config._filenames.empty()) || vm.count("help"))
      throw runtime_error(usage.str());
    
    
    return config;
}



int main(int argc, const char **argv){
  
    namespace bfs = boost::filesystem;
    bfs::path::default_name_check(bfs::native);

    try
    {
        Config config = parseCommandLine(argc, argv);        
        go(config);
        return 0;
    }
    catch (exception& e)
    {
      cout << "ERROR : "<< e.what() << endl;
    }
    catch (...)
      {
        cout << "[peptideSieve.cpp::main()] Abnormal termination.\n";
      }
  return 0;
}




