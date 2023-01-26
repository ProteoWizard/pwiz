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
#include "CHardklorOutput.h"

using namespace std;

bool CHardklorOutput::openFile(char* fileName, hkOutputFormat format){
	
	if(outFile!=NULL) closeFile();

	outFile=fopen(fileName,"wt");
	if(outFile==NULL){
		cout << "Cannot open output file: " << fileName << endl;
		return false;
	}

	//output header
	fprintf(outFile,"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
	fprintf(outFile,"<?xml-stylesheet type=\"text/xsl\" href=\"pepXML_std.xsl\"?>");
	fprintf(outFile,"<ms_analysis date=\"\" summary_xml=\"\" xmlns=\"http://regis-web.systemsbiology.net/pepXML\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd\">");
  
	return true;
}

bool CHardklorOutput::exportScan(){

	if(outFile==NULL) {
		cout << "No file open for export." << endl;
		return false;
	}

	return true;
}

void CHardklorOutput::closeFile(){
	if(outFile!=NULL) fclose(outFile);
}