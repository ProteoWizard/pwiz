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
#include "CPeriodicTable.h"

using namespace std;

/*
CPeriodicTable::CPeriodicTable(){
  table = new vector<element>;
  loadTable("Hardklor.dat");
}
*/

CPeriodicTable::CPeriodicTable(const char* c){
  loadTable(c);
}

CPeriodicTable::~CPeriodicTable(){
}

element& CPeriodicTable::at(int i){
  return table.at(i);
}

void CPeriodicTable::defaultValues(){
  string s[109]={"X","H","He","Li","Be","B","C","N","O","F","Ne","Na","Mg","Al","Si","P","S","Cl","Ar","K",
    "Ca","Sc","Ti","V","Cr","Mn","Fe","Co","Ni","Cu","Zn","Ga","Ge","As","Se","Br","Kr","Rb","Sr","Y","Zr",
    "Nb","Mo","Tc","Ru","Rh","Pd","Ag","Cd","In","Sn","Sb","Te","I","Xe","Cs","Ba","La","Ce","Pr","Nd","Pm",
    "Sm","Eu","Gd","Tb","Dy","Ho","Er","Tm","Yb","Lu","Hf","Ta","W","Re","Os","Ir","Pt","Au","Hg","Tl","Pb",
    "Bi","Po","At","Rn","Fr","Ra","Ac","Th","Pa","U","Np","Pu","Am","Cm","Bk","Cf","Es","Fm","Md","No","Lr",
    "Hx","Cx","Nx","Ox","Sx"};
  double m[109]={0.000000000000,1.007824600000,3.016030000000,6.015121000000,9.012182000000,10.012937000000,
    12.000000000000,14.003073200000,15.994914100000,18.998403200000,19.992435000000,22.989767000000,
    23.985042000000,26.981539000000,27.976927000000,30.973762000000,31.972070000000,34.968853100000,
    35.967545000000,38.963707000000,39.962591000000,44.955910000000,45.952629000000,49.947161000000,
    49.946046000000,54.938047000000,53.939612000000,58.933198000000,57.935346000000,62.939598000000,
    63.929145000000,68.925580000000,69.924250000000,74.921594000000,73.922475000000,78.918336000000,
    77.914000000000,84.911794000000,83.913430000000,88.905849000000,89.904703000000,92.906377000000,
    91.906808000000,98.000000000000,95.907599000000,102.905500000000,101.905634000000,106.905092000000,
    105.906461000000,112.904061000000,111.904826000000,120.903821000000,119.904048000000,126.904473000000,
    123.905894000000,132.905429000000,129.906282000000,137.907110000000,135.907140000000,140.907647000000,
    141.907719000000,145.000000000000,143.911998000000,150.919847000000,151.919786000000,158.925342000000,
    155.925277000000,164.930319000000,161.928775000000,168.934212000000,167.933894000000,174.940770000000,
    173.940044000000,179.947462000000,179.946701000000,184.952951000000,183.952488000000,190.960584000000,
    189.959917000000,196.966543000000,195.965807000000,202.972320000000,203.973020000000,208.980374000000,
    209.000000000000,210.000000000000,222.000000000000,223.000000000000,226.025000000000,227.028000000000,
    232.038054000000,231.035900000000,234.040946000000,237.048000000000,244.000000000000,243.000000000000,
    247.000000000000,247.000000000000,251.000000000000,252.000000000000,257.000000000000,258.000000000000,
    259.000000000000,260.000000000000,1.007824600000,12.000000000000,14.003073200000,15.994914100000,
    31.972070000000};

  element e;
  unsigned int i;
  for(i=0;i<109;i++){
    e.atomicNum=i;
    strcpy(e.symbol,&s[i][0]);
    e.mass=m[i];
    table.push_back(e);
  }

}

void CPeriodicTable::loadTable(const char* c){
  FILE* fptr;
  element e;

  if(c==NULL || strlen(c)==0) {
    defaultValues();
    return;
  }
  
  fptr = fopen(c,"rt");
  if(fptr==NULL) {
    cout << "Cannot open periodic table! " << c << "." << endl;
    return;
  }
  
  /* 
     This loop reads in table entries, line by line.
     It has basic error detection (missing data), but additional
     checks should be made to confirm the content of data
  */
  int ret;
  while(!feof(fptr)){
    ret=fscanf(fptr,"%d\t%s\t%lf\n",&e.atomicNum,e.symbol,&e.mass);   
    table.push_back(e);   
  }

  fclose(fptr);
  
}

int CPeriodicTable::size(){
  return (int)table.size();
}
