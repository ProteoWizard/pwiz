//
// $Id$
//
//
// Original author: Witold Wolski <wewolski@gmail.com>
//
// Copyright : ETH Zurich 
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed  on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#include <fstream>
#include <boost/cstdint.hpp>
#include <pwiz/data/msdata/MSDataFile.hpp>
#include <boost/filesystem.hpp>
#include <boost/program_options.hpp>
#include "pwiz/analysis/findmf/qtofpeakpickerfilter.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"


namespace b_po = boost::program_options;
namespace b_fs = boost::filesystem;

struct PPParams
{
  std::string infile_;
  std::string outfile_;
  //uint32_t nrthreads;
  double resolution_; // with of mz bins in ppms
  double smoothwidth_;
  boost::uint32_t integrationwidth_;
  bool area_;
  double threshold_;
  boost::uint32_t numberofpeaks_;

  std::string filestem_;
  b_fs::path outdir_;

  PPParams(): infile_() ,
    outfile_() ,
    resolution_(0.) ,
    smoothwidth_(1.),
    integrationwidth_(4) ,
    area_(false),
    threshold_(10.),
    numberofpeaks_(0),
    filestem_(),
    outdir_(){
  }

  void prepareOutputFile()
  {
    if( !b_fs::exists(infile_) )
      {
        return;
      }
    //filestem_ = boost::filesystem::path(infile).stem().stem().stem().string(); //createOutputs(p1.string() ,"fitered");
    outdir_ = b_fs::path(outfile_).parent_path();
    //create outdir
    try{
      if(!b_fs::exists(outdir_)){
          b_fs::create_directory(outdir_);
        }
    }catch(std::exception & e ){
      std::cout << e.what() << std::endl;
    }
  }
};//end struct param

//parameters
inline int defineParameters(
    int ac,
    char* av[],
    b_po::variables_map & vmgeneral
    )
{
  try
  {
    b_po::options_description general("File Handling:");
    general.add_options()
        ("help,H", "produce help message")
        ("version,V", "produces version information")
        ("in,I", b_po::value<std::string>(), "input file")
        ("out,O", b_po::value<std::string>(), "output file")
        ("config-file,C", b_po::value<std::string>(), "configuration file");

    b_po::options_description processing("Processing Options:");
    processing.add_options()
        ("resolution",b_po::value<double>()->default_value(20000.),
         "instrument resolution.")
        ("area", b_po::value<bool>()->default_value(true),"default area, otherwise store intensity (0).")
        ("threshold", b_po::value<double>()->default_value(10.),"removes peaks less than threshold times smallest intensity in spectrum")
	("numberofpeaks", b_po::value<uint32_t>()->default_value(0),"maximum number of peaks per spectrum (0 = no limit)")
        ;
	b_po::options_description advancedprocessing("Advanced Processing Options:");
      advancedprocessing.add_options()
	("widthint,i", b_po::value<int>()->default_value(2),"peak apex +- integration width")
		 ("smoothwidth",b_po::value<double>()->default_value(1.),"smoothing width")
        ;
       
    

    b_po::options_description cmdloptions;
    cmdloptions.add(general).add(processing).add(advancedprocessing);
    b_po::store( b_po::parse_command_line( ac , av , cmdloptions) , vmgeneral);
    b_po::notify(vmgeneral);
    std::string configfile;
    if(vmgeneral.count("config-file"))
      {
        configfile = vmgeneral["config-file"].as<std::string>();
      }

    b_po::options_description config_file_options;
    config_file_options.add(general).add(processing).add(advancedprocessing);
    if(configfile.size() > 0 && b_fs::exists(configfile))
      {
        std::ifstream ifs(configfile.c_str());
        store(parse_config_file(ifs, config_file_options), vmgeneral);
        b_po::notify(vmgeneral);
      }
    else if(configfile.size() == 0){
      }
    else
      {
        std::cerr << "Could not find config file." << std::endl;
        return -1;
      }

    if(!vmgeneral.count("in"))
      {
        std::cerr << "input file is obligatory" << std::endl;
        std::cerr << cmdloptions << "\n";
        return -1;
      }
    if(!vmgeneral.count("out"))
      {
        std::cerr << "output file is obligatory" << std::endl;
        std::cerr << cmdloptions << "\n";
        return -1;
      }
    if(vmgeneral.count("help"))
      {
        std::cerr << cmdloptions << "\n";
        return -1;
      }
    if(vmgeneral.count("version"))
      {
        std::cerr << "1.0.0.3" << "\n";
        return -1;
      }
  }
  catch(std::exception& e)
  {
    std::cerr << "error: " << e.what() << "\n";
    return -1;
  }
  catch(...)
  {
    std::cerr << "Exception of unknown type!\n";
  }
  return 0;
}//end parse command line

inline void analysisParameters(PPParams & ap,b_po::variables_map & vmgeneral){
  if(vmgeneral.count("in"))
    {
      ap.infile_ =  vmgeneral["in"].as<std::string>();
    }

  if(vmgeneral.count("out"))
    {
      ap.outfile_ = vmgeneral["out"].as<std::string>();
    }
	//factor 2 required ...
  ap.resolution_ = 2*vmgeneral["resolution"].as<double>();
  ap.smoothwidth_ = 2*vmgeneral["smoothwidth"].as<double>();
  ap.integrationwidth_ = 2*vmgeneral["widthint"].as<int>();
  ap.area_ = vmgeneral["area"].as<bool>(); //do you want to store areas
  ap.threshold_ = vmgeneral["threshold"].as<double>();
  ap.numberofpeaks_ = vmgeneral["numberofpeaks"].as<uint32_t>();
}

int main(int argc, char *argv[])
{
  b_po::variables_map vmgeneral;
  if(defineParameters(argc, argv, vmgeneral) != 0){
      return 0;
    }
  PPParams aparam;
  analysisParameters(aparam,vmgeneral);
  pwiz::msdata::ExtendedReaderList readers;
  pwiz::msdata::MSDataPtr msdataptr_ = pwiz::msdata::MSDataPtr(new pwiz::msdata::MSDataFile(aparam.infile_,&readers));
  pwiz::msdata::SpectrumListPtr sl = msdataptr_->run.spectrumListPtr;
  pwiz::msdata::SpectrumListPtr mp(
        new ralab::QTOFPeakPickerFilter(sl,
                                        aparam.resolution_,
                                        aparam.smoothwidth_,
                                        aparam.integrationwidth_,
                                        aparam.threshold_,
                                        aparam.area_,
					aparam.numberofpeaks_
                                        )
        );
  msdataptr_->run.spectrumListPtr = mp;
  pwiz::msdata::MSDataFile::Format format;
  if(std::string(".mzML").compare(b_fs::extension(aparam.outfile_)) ==0 ){
      format = pwiz::msdata::MSDataFile::Format_mzML;
    }
  else{
      format =  pwiz::msdata::MSDataFile::Format_mzXML;
    }
  pwiz::msdata::MSDataFile::write(*msdataptr_,
                                  aparam.outfile_,
                                  format
                                  ); // uncomment if you want mzXML
}
