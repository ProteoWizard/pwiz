#ifndef _CPSM_H
#define _CPSM_H

#include <string>
#include <vector>

typedef struct sPSMMod{
  char residue;
  int pos;
  double mass;
  sPSMMod(){
    residue = '?';
    pos=0;
    mass=0;
  }
}sPSMMod;

typedef struct sPSMScan{
  int scanNumber;
  double rTimeMin;
  double rTimeSec;
  std::string fileName;
  std::string scanID;
  sPSMScan(){
    scanNumber=0;
    rTimeMin=0;
    rTimeSec=0;
    fileName.clear();
    scanID.clear();
  }
}sPMSScan;

typedef struct sPSMScore{
  double value;
  std::string name;
  sPSMScore(){
    value=0;
    name.clear();
  }
}sPSMScore;

class CPSM{
public:

  //constructors and destructors
  CPSM();
  CPSM(const CPSM& c);
  ~CPSM();

  //data members
  int charge;
  int modCount;
  int proteinCount;
  int scoreCount;

  double calcNeutMass;
  double mass;
  double mzObs;

  std::string sequence;
  std::string sequenceMod;

  sPSMScan scanInfo;

  //operators
  CPSM& operator=(const CPSM& c);

  //functions
  void addMods(std::vector<sPSMMod>& v);
  void addProteins(std::vector<std::string>& v);
  void addScores(std::vector<sPSMScore>& v);

  sPSMMod getMod(int index);
  std::string getProtein(int index);
  sPSMScore getScore(int index);

private:
  sPSMMod* mods;
  sPSMScore* scores;
  std::string* proteins;

};

#endif