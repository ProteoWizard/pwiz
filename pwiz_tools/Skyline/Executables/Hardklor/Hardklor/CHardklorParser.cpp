/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#include "CHardklorParser.h"

using namespace std;
using namespace MSToolkit;

CHardklorParser::CHardklorParser(){
  vQueue = new vector<CHardklorSetting>;
}

CHardklorParser::~CHardklorParser(){
  delete vQueue;
}

//Takes a command line and breaks it into tokens
//Tokens are then read and used to set global and local parameters
void CHardklorParser::parse(char* cmd) {

  int j;
  bool isGlobal=true;
  sMolecule m;
  char *tok;
  char* tmpstr = static_cast<char*>(malloc(strlen(cmd) + 1));
  string tstr;
  vector<string> vs;

  /*
	//For modifications
	CHardklorVariant v;
	CPeriodicTable* PT;
	int j,k;
	string atom;
	string isotope;
	string percent;
	bool badMod;
	int atomNum;
	bool bNew;
  */

	bool bFile;
	string paramStr;

  CHardklorSetting hs;

	//Replace first # with a terminator
	tok=strstr(cmd,"#");
	if(tok!=NULL) strncpy(tok,"\0",1);

	//if we have only white space, exit here
	strcpy(tmpstr,cmd);
	tok=strtok(tmpstr," \t\n\r");
	if(tok==NULL)
    {
		free(tmpstr);
        return;
    }

	//Check if we have a parameter (has '=' in it) or a file request.
	tok=strstr(cmd,"=");
	if(tok==NULL) bFile=true;
	else bFile=false;

	//Read file and return, if needed
	if(bFile){

		hs=global;
		       
		//on systems that allow a space in the path, require quotes (") to capture
    //complete file name
    strcpy(tmpstr,cmd);

    //Check for quote
    if(tmpstr[0]=='\"'){
			
			//continue reading tokens until another quote is found
			j=1;
			while(true){
				if(j==strlen(tmpstr)){
					cout << "Invalid input file." << endl;
					exit(-1);
				}
				if(tmpstr[j]=='\"') break;
				j++;
			}
			tmpstr[j]='\0';
			hs.inFile = &tmpstr[1];
			j++;
		} else {
			tok=strtok(tmpstr," \t\n\r");
			hs.inFile = tmpstr;
			j=(int)strlen(tmpstr);
		}

		//Find first non-whitespace
		while(true){
			if(j>=(int)strlen(cmd)){
				cout << "Invalid output file." << endl;
				exit(-1);
			}
			if(cmd[j]!=' ' && cmd[j]!='\t') break;
			j++;
		}

		strcpy(tmpstr,&cmd[j]);

    //Check for quote
    if(tmpstr[0]=='\"'){
			
			//continue reading tokens until another quote is found
			j=1;
			while(true){
				if(j==strlen(tmpstr)){
					cout << "Invalid output file." << endl;
					exit(-1);
				}
				if(tmpstr[j]=='\"') break;
				j++;
			}
			tmpstr[j]='\0';
			hs.outFile = &tmpstr[1];
			j++;
		} else {
			tok=strtok(tmpstr," \t\n\r");
			hs.outFile=tmpstr;
			j=(int)strlen(tmpstr);
		}

		//cout << hs.inFile << "\t" << hs.outFile << endl;

		hs.fileFormat = getFileFormat(hs.inFile.c_str());
		vQueue->push_back(hs);
		free(tmpstr);
		return;
	}
	free(tmpstr);

	char* upStr = static_cast<char*>(malloc(strlen(cmd) + 1));
	//Read parameter
	tok=strtok(cmd," \t=\n\r");
	if(tok==NULL) return;
    paramStr=tok;
	const char* param = paramStr.c_str();
	tok=strtok(NULL," \t=\n\r");
	if(tok==NULL) {
		warn(param,0);
		free(upStr);
		return;
	}

	//process parameter
	if(strcmp(param,"algorithm")==0){
		for(j=0;j<(int)strlen(tok);j++) upStr[j]=toupper(tok[j]);
		upStr[j]='\0';
		if(strcmp(upStr,"BASIC")==0) global.algorithm=Basic;
		else if (strcmp(upStr,"VERSION1")==0) global.algorithm=FastFewestPeptides;
		else if (strcmp(upStr,"VERSION2")==0) global.algorithm=Version2;
		else {
			global.algorithm=Version2;
			warn("Unknown algorithm. Defaulting to Version2.",2);
		}

	} else if(strcmp(param,"averagine_mod")==0){
    if(strlen(tok)==1 && tok[0]=='0') global.variant->clear();
    else {
      tstr=tok;
      tok=strtok(NULL," \t\n\r");
      while(tok!=NULL){
        tstr+=" ";
        tstr+=tok;
        tok=strtok(NULL," \t\n\r");
      }      
      if(!makeVariant(&tstr[0])) warn("Invalid averagine_mod value. Skipping averagine_mod.",2);
    }

	} else if(strcmp(param,"boxcar_averaging")==0){
		global.boxcar=atoi(tok);
    if(global.boxcar>0 && global.boxcar%2==0) {
			global.boxcar++;
			warn("boxcar_averaging value is even number. Incrementing by 1.",2);
		}

	} else if(strcmp(param,"boxcar_filter")==0){
		global.boxcarFilter=atoi(tok);

	} else if(strcmp(param,"boxcar_filter_ppm")==0){
		global.ppm=atof(tok);

	} else if(strcmp(param,"centroided")==0){
		if(atoi(tok)!=0) global.centroid=true;
		else global.centroid=false;

	} else if(strcmp(param,"charge_algorithm")==0){
		for(j=0;j<(int)strlen(tok);j++) upStr[j]=toupper(tok[j]);
		upStr[j]='\0';
		if(strcmp(upStr,"QUICK")==0) global.chargeMode='Q';
		else if (strcmp(upStr,"FFT")==0) global.chargeMode='F';
		else if (strcmp(upStr,"PATTERSON")==0) global.chargeMode='P';
		else if (strcmp(upStr,"SENKO")==0) global.chargeMode='S';
		else if (strcmp(upStr,"NONE")==0) global.chargeMode='B';
		else {
			global.chargeMode='Q';
			warn("Unknown charge algorithm. Defaulting to Quick.",2);
		}

	} else if(strcmp(param,"charge_max")==0){
		global.maxCharge=atoi(tok);

	} else if(strcmp(param,"charge_min")==0){
		global.minCharge=atoi(tok);

	} else if(strcmp(param,"correlation")==0){
		global.corr=atof(tok);

	} else if(strcmp(param,"depth")==0){
		global.depth=atoi(tok);

	} else if(strcmp(param,"distribution_area")==0){
		if(atoi(tok)!=0) global.distArea=true;
		else global.distArea=false;

	} else if(strcmp(param,"hardklor_data")==0){
		global.HardklorFile = tok;

	} else if(strcmp(param,"instrument")==0){
		for(j=0;j<(int)strlen(tok);j++) upStr[j]=toupper(tok[j]);
		upStr[j]='\0';
		if(strcmp(upStr,"FTICR")==0) global.msType=FTICR;
		else if (strcmp(upStr,"ORBITRAP")==0) global.msType=OrbiTrap;
		else if (strcmp(upStr,"TOF")==0) global.msType=TOF;
		else if (strcmp(upStr,"QIT")==0) global.msType=QIT;
		else {
			global.msType=OrbiTrap;
			warn("Unknown instrument type. Defaulting to Orbitrap.",2);
		}

	} else if(strcmp(param,"isotope_data")==0){
    global.MercuryFile = tok;
	if (global.MercuryFile[0]=='\"')
	{
		// Remove quotes
		global.MercuryFile =  global.MercuryFile.substr(1, global.MercuryFile.length()-2);
	}
	} else if(strcmp(param,"max_features")==0){

  } else if(strcmp(param,"molecule_max_mz")==0){
    global.maxMolMZ=atof(tok);

	} else if(strcmp(param,"ms_level")==0){
    if(atoi(tok)==3){
      global.mzXMLFilter=MS3;
      global.msLevel=3;
    } else if(atoi(tok)==2) {
      global.mzXMLFilter=MS2;
      global.msLevel=2;
    } else {
      global.mzXMLFilter=MS1;
      global.msLevel=1;
    }

	} else if(strcmp(param,"mz_max")==0){
		global.window.dUpper=atof(tok);

	} else if(strcmp(param,"mz_min")==0){
		global.window.dLower=atof(tok);

	} else if(strcmp(param,"mz_window")==0){
		global.winSize=atof(tok);

	} else if(strcmp(param,"resolution")==0){
		global.res400=atof(tok);

	} else if(strcmp(param,"scan_range_max")==0){
		global.scan.iUpper=atoi(tok);

	} else if(strcmp(param,"scan_range_min")==0){
		global.scan.iLower=atoi(tok);

	} else if(strcmp(param,"sensitivity")==0){
	} else if(strcmp(param,"signal_to_noise")==0){
		global.sn=atof(tok);

	} else if(strcmp(param,"smooth")==0){
		global.smooth=atoi(tok);

	} else if(strcmp(param,"sn_window")==0){
		global.snWindow=atof(tok);

	} else if(strcmp(param,"static_sn")==0){
		if(atoi(tok)!=0) global.staticSN=true;
		else global.staticSN=false;

	} else if(strcmp(param,"xml")==0){
		if(atoi(tok)!=0) global.xml=true;
		else global.xml=false;

	} else {
		warn(param,1);
	}
	free(upStr);
}

bool CHardklorParser::parseCMD(int argc, char* argv[]){

  int i=2;
  char tstr[512];

  while(i<argc-2){
    if(argv[i][0]=='-'){
      strcpy(tstr,&argv[i][1]);
      strcat(tstr," = ");
      strcat(tstr,argv[i+1]);
      parse(tstr);
    } else {
      cout << "There was an error with your command line parameters." << endl;
      return false;
    }
    i+=2;
  }

  strcpy(tstr,argv[argc-2]);
  strcat(tstr," ");
  strcat(tstr,argv[argc-1]);
  parse(tstr);  

  return true;

}


//Reads in a config file and passes it to the parser
bool CHardklorParser::parseConfig(char* c){
  fstream fptr;
  char tstr[512];

  fptr.open(c,ios::in);
  if(!fptr.good()){
    cout << "Cannot open config file!" << endl;
    return false;
  }

  while(!fptr.eof()) {
    fptr.getline(tstr,512);
    if(tstr[0]==0) continue;
    if(tstr[0]=='#') continue;
    parse(tstr);
  }

  fptr.close();
  return true;
}

CHardklorSetting& CHardklorParser::queue(int i){
  return vQueue->at(i);
}

int CHardklorParser::size(){
  return (int)vQueue->size();
}

//Identifies file format from extension - Must conform to these conventions
MSFileFormat CHardklorParser::getFileFormat(const char* c){

	char *file = static_cast<char*>(malloc(strlen(c) + 1));
	std::string extStr;
	char *tok;

	strcpy(file,c);
	tok=strtok(file,".\n\r");
	while(tok!=NULL){
		extStr = tok;
		tok=strtok(NULL,".\n\r");
	}
	const char* ext = extStr.c_str();
	free(file);

	if(strcmp(ext,"ms1")==0 || strcmp(ext,"MS1")==0) return ms1;
	if(strcmp(ext,"ms2")==0 || strcmp(ext,"MS2")==0) return ms2;
	if(strcmp(ext,"bms1")==0 || strcmp(ext,"BMS1")==0) return bms1;
	if(strcmp(ext,"bms2")==0 || strcmp(ext,"BMS2")==0) return bms2;
#ifndef _NO_CMS
	if(strcmp(ext,"cms1")==0 || strcmp(ext,"CMS1")==0) return cms1;
	if(strcmp(ext,"cms2")==0 || strcmp(ext,"CMS2")==0) return cms2;
#endif
	if(strcmp(ext,"zs")==0 || strcmp(ext,"ZS")==0) return zs;
	if(strcmp(ext,"uzs")==0 || strcmp(ext,"UZS")==0) return uzs;
	if(strcmp(ext,"mzML")==0 || strcmp(ext,"MZML")==0) return mzML;
	if(strcmp(ext,"mzXML")==0 || strcmp(ext,"MZXML")==0) return mzXML;
	if(strcmp(ext,"mgf")==0 || strcmp(ext,"MGF")==0) return mgf;
	if(strcmp(ext,"mz5")==0 || strcmp(ext,"MZ5")==0) return mz5;
  if(strcmp(ext,"raw")==0 || strcmp(ext,"RAW")==0) return raw;
	return dunno;

}

void CHardklorParser::warn(const char* c, int i){
  switch(i){
  case 0:
    cout << "Parameter " << c << " has no value." << endl;
    break;
  case 1:
    cout << "Unknown parameter: " << c << endl;
    break;
  case 2:
  default:
    cout << c << endl;
    break;
  }
}

bool CHardklorParser::makeVariant(char* c){

  //For modifications
  CHardklorVariant v;
  CPeriodicTable* PT;
  int j,k;
  string atom;
  string isotope;
  string percent;
  int atomNum;
  bool bNew;
  
  char str[256];
  char* tok;
  strcpy(str,c);

  v.clear();
  PT = new CPeriodicTable(global.HardklorFile.c_str());  

  tok=strtok(str," \n\r");  
  while(tok!=NULL){
    if(isdigit(tok[0]) || tok[0]=='.'){
      //we have enrichment
      percent="";
      atom="";
      isotope="";
      
      //Get the APE
      for(j=0;j<(int)strlen(tok);j++){
        if(isdigit(tok[j]) || tok[j]=='.') {
          if(percent.size()==15){
            warn("Bad averagine_mod: Malformed modification flag, too many digits.",2);
            return false;
          }
          percent+=tok[j];
        } else {
          break;
        }
      }
      
      //Get the atom
      for(j=j;j<(int)strlen(tok);j++){
        if(isalpha(tok[j])) {
          if(atom.size()==2){
            warn("Bad averagine_mod: Malformed modification flag, invalid atom",2);
            return false;
          }
          atom+=tok[j];
        } else {
          break;
        }
      }

      //Get the isotope
      for(j=j;j<(int)strlen(tok);j++){
        if(isotope.size()==2){
          warn("Bad averagine_mod: Malformed modification flag, bad isotope",2);
          return false;
        }
        isotope+=tok[j];
      }

      //format the atom properly
      atom.at(0)=toupper(atom.at(0));
      if(atom.size()==2) atom.at(1)=tolower(atom.at(1));

      //Get the array number for the atom
      atomNum=-1;
      for(j=0;j<PT->size();j++){
        if(strcmp(PT->at(j).symbol,&atom[0])==0){
          atomNum=j;
          break;
        }
      }

      if(atomNum==-1){
        warn("Bad averagine_mod: Malformed modification flag, atom not in periodic table",2);
        return false;
      }

      v.addEnrich(atomNum,atoi(&isotope[0]),atof(&percent[0]));

    } else {
      //we have molecule
      percent="";
      atom="";
      bNew=true;

      //Get the atom
      for(j=0;j<(int)strlen(tok);j++){

        //Check for an atom symbol
        if(isalpha(tok[j])) {

          //Check if we were working on the count of the previous atom
          if(!bNew) {
            bNew=true;

            //Make sure the atom has uppercase-lowercase letter format;
            atom.at(0)=toupper(atom.at(0));
            if(atom.size()==2) atom.at(1)=tolower(atom.at(1));

            //Look up the new atom
            for(k=0;k<PT->size();k++){
              if(strcmp(PT->at(k).symbol,&atom[0])==0){
                //Add the new atom to the variant
                v.addAtom(k,atoi(&percent[0]));
                break;
              }
            }

            //Clear the fields
            percent="";
            atom="";
          }

          //Add this letter to the atom symbol
          if(atom.size()==2){
            warn("Bad averagine_mod: Malformed modification flag, invalid atom",2);
            return false;
          }
          atom+=tok[j];
        
        } else if(isdigit(tok[j])){
	
          //Whenever we find a digit, we have already found an atom symbol
          bNew=false;
          
          //Add this letter to the atom count
          if(percent.size()==12){
            warn("Bad averagine_mod: Malformed modification flag, unreasonable atom count",2);
            return false;
          }
          percent+=tok[j];
        
        }      
      }
      
      //process the last atom
      //Make sure the atom has uppercase-lowercase letter format;
      atom.at(0)=toupper(atom.at(0));
      if(atom.size()==2) atom.at(1)=tolower(atom.at(1));
      
      //Look up the new atom
      for(k=0;k<PT->size();k++){
        if(strcmp(PT->at(k).symbol,&atom[0])==0){
          
          //Add the new atom to the variant
          v.addAtom(k,atoi(&percent[0]));
          break;
        
        }
      }
    }
    tok=strtok(NULL," \n\r"); 
  }

  global.variant->push_back(v);
  delete PT;
  return true;

}
