
#include <iostream>
#include <sstream>
#include <iomanip>
#include <fstream>
#include <string>
#include <vector>
#include <cmath>
#include <algorithm>
#include <math.h>
#include <boost/math/special_functions/gamma.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/lexical_cast.hpp>
using namespace std;

bool sortByScore(const double &lhs, const double &rhs)
{
    return lhs > rhs;
}
bool sortByScoreLtoH(const double &lhs, const double &rhs)
{
    return lhs < rhs;
}

struct Lipid {
    bool filled;
    string lipidIdent;
    string lipidFormula;
    double expMass;
    double calcMass;
    string charge;
    string time;
    double score;
    string mod;
    string spectra;
    string scores;
};

struct Spectra {
    int number;
    string lipidIdent;
    string lipidFormula;
    double expMass;
    double calcMass;
    string charge;
    string time;
    double score;
    string mod;
};

int main(int argc, const char* argv[]){

    cout << "LipidLama: Assessing the confidence of Greazy identifications." << endl << endl;

    const double PI = 3.141592653589793238463;

    vector<Lipid> lipids;

    string filename = argv[1];
    string str;
    ifstream file(filename.c_str());
    if (!file)
    {
        cout << "Unable to open file" << endl;
        cout << filename << endl;
        cout << "Greazy must be run successfully before running LipidLama." << endl;
        getline(cin, str);
        cin.get();
        exit(1);
    }

    string cut = argv[2];
    string off = "0." + cut;
    double cutoff;
    stringstream convert(off);
    convert >> cutoff;
    string top = argv[3];
    //string percent = "0." + top;
    double topPercent = boost::lexical_cast<double>(top);



    vector<Spectra> spectra;

    string line, location;
    int fileIndex = 0;
    int spectraIndex;
    bool second = false;
    bool third = false;
    
    vector<vector<double> > distros0(25);
    vector<vector<double> > distros1(25);
    vector<vector<string> > distros1lipid(25);
    vector<vector<double> > distros2(25);
    vector<vector<double> > parameters(25);
    vector<string> modifications;

    /*
    0: PC
    1: PE
    2: PI
    3: PG
    4: PS
    5: PA
    6: SM
    7: CL
    8: CL2
    9: PIP
    10: PIP2
    11: MPC
    12: MPE
    13: MPI
    14: MPG
    15: MPS
    16: MPA
    17: MSM
    18: M3CL
    19: MCL
    20: MPIP
    21: PEC
    22: PIC
    23: MPEC
    24: MPIC
    */

    //cout << "Building distributions... ";
    while (getline(file, line))
    {
        if (line == "SECONDHIGHESTSCORES")
        {
            second = true;
        }
        if (line == "MODIFICATIONS")
        {
            second = false;
            third = true;

        }
        if ((second == false) && (third == false))
        {
            if (fileIndex == 0)
            {
                location = line;
            }
            else
            {
                if (line == "---")
                {
                    spectra.push_back(Spectra());
                    spectraIndex = 0;
                }
                else
                {
                    if (spectraIndex == 0)
                    {                    
                        spectra[spectra.size()-1].number = atoi(line.c_str());
                    }
                    if (spectraIndex == 1)
                    {                    
                        spectra[spectra.size()-1].lipidIdent = line;
                    }
                    if (spectraIndex == 2)
                    {                    
                        spectra[spectra.size()-1].lipidFormula = line;
                    }
                    if (spectraIndex == 3)
                    {                    
                        spectra[spectra.size()-1].expMass = atof(line.c_str());
                    }
                    if (spectraIndex == 4)
                    {                    
                        spectra[spectra.size()-1].calcMass = atof(line.c_str());
                    }
                    if (spectraIndex == 5)
                    {                    
                        spectra[spectra.size()-1].charge = line;
                    }
                    if (spectraIndex == 6)
                    {    
                        if (atof(line.c_str()) == 100000)
                        {
                            spectra[spectra.size()-1].time = "null";
                        }
                        else
                        {
                            spectra[spectra.size()-1].time = line.c_str();
                        }
                    
                    }
                    if (spectraIndex == 7)
                    {                    
                        spectra[spectra.size()-1].mod = line;
                    }
                    if (spectraIndex == 8)
                    {                    
                        spectra[spectra.size()-1].score = atof(line.c_str());


                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PC" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[0].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[0].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PC" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[11].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[11].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PE(" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[1].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[1].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PE(" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[12].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[12].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PI(" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[2].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[2].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PI(" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[13].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[13].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PG" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[3].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[3].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PG" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[14].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[14].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PS" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[4].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[4].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PS" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[15].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[15].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PA" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[5].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[5].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "PA" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[16].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[16].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "SM" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[6].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[6].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "SM" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[17].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[17].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "CL" && spectra[spectra.size()-1].mod != "-2H" && spectra[spectra.size()-1].mod != "-2H + Na" && spectra[spectra.size()-1].mod != "-2H + Li" && spectra[spectra.size()-1].mod != "-2H + K" && spectra[spectra.size()-1].mod != "-2H + 3Na" && spectra[spectra.size()-1].mod != "-2H + 3Li" && spectra[spectra.size()-1].mod != "-2H + 3K")
                        {
                            distros1[7].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[7].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "CL" && spectra[spectra.size()-1].mod != "-2H" && (spectra[spectra.size()-1].mod == "-2H + Na" || spectra[spectra.size()-1].mod == "-2H + Li" || spectra[spectra.size()-1].mod == "-2H + K"))
                        {
                            distros1[19].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[19].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        /*
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "CL" && spectra[spectra.size()-1].mod != "-2H" && (spectra[spectra.size()-1].mod == "-2H + 3Na" || spectra[spectra.size()-1].mod == "-2H + 3Li" || spectra[spectra.size()-1].mod == "-2H + 3K"))
                        {
                            distros1[18].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[18].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        */
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 2) == "CL" && spectra[spectra.size()-1].mod == "-2H")
                        {
                            distros1[8].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[8].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PIP" && spectra[spectra.size()-1].mod != "-2H" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[9].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[9].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PIP" && spectra[spectra.size()-1].mod != "-2H" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[20].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[20].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 3) == "PIP" && spectra[spectra.size()-1].mod == "-2H")
                        {
                            distros1[10].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[10].push_back(spectra[spectra.size()-1].lipidIdent);
                        }

                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 6) == "PE-Cer" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[21].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[21].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 6) == "PE-Cer" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[23].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[23].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 6) == "PI-Cer" && spectra[spectra.size()-1].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
                        {
                            distros1[22].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[22].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                        if (spectra[spectra.size()-1].lipidIdent.substr(0, 6) == "PI-Cer" && (spectra[spectra.size()-1].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
                        {
                            distros1[24].push_back(spectra[spectra.size()-1].score);
                            distros1lipid[24].push_back(spectra[spectra.size()-1].lipidIdent);
                        }
                    }
                    spectraIndex++;
                }
            }
            fileIndex++;
        }
        if (second == true)
        {
            if (line.substr(0, 2) == "PC")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[0].push_back(value1);
                distros2[0].push_back(value2);
            }
            if (line.substr(0, 3) == "PE ")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[1].push_back(value1);
                distros2[1].push_back(value2);
            }
            if (line.substr(0, 3) == "PI ")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[2].push_back(value1);
                distros2[2].push_back(value2);
            }
            if (line.substr(0, 2) == "PG")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[3].push_back(value1);
                distros2[3].push_back(value2);
            }
            if (line.substr(0, 2) == "PS")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[4].push_back(value1);
                distros2[4].push_back(value2);
            }
            if (line.substr(0, 2) == "PA")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[5].push_back(value1);
                distros2[5].push_back(value2);
            }
            if (line.substr(0, 2) == "SM")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[6].push_back(value1);
                distros2[6].push_back(value2);
            }
            if (line.substr(0, 3) == "CL ")
            {
                string fline = line.substr(3);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[7].push_back(value1);
                distros2[7].push_back(value2);
            }
            if (line.substr(0, 3) == "CL2")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[8].push_back(value1);
                distros2[8].push_back(value2);
            }
            if (line.substr(0, 4) == "PIP ")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[9].push_back(value1);
                distros2[9].push_back(value2);
            }
            if (line.substr(0, 4) == "PIP2")
            {
                string fline = line.substr(5);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[10].push_back(value1);
                distros2[10].push_back(value2);
            }
            if (line.substr(0, 3) == "MPC")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[11].push_back(value1);
                distros2[11].push_back(value2);
            }
            if (line.substr(0, 4) == "MPE ")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[12].push_back(value1);
                distros2[12].push_back(value2);
            }
            if (line.substr(0, 4) == "MPI ")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[13].push_back(value1);
                distros2[13].push_back(value2);
            }
            if (line.substr(0, 3) == "MPG")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[14].push_back(value1);
                distros2[14].push_back(value2);
            }
            if (line.substr(0, 3) == "MPS")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[15].push_back(value1);
                distros2[15].push_back(value2);
            }
            if (line.substr(0, 3) == "MPA")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[16].push_back(value1);
                distros2[16].push_back(value2);
            }
            if (line.substr(0, 3) == "MSM")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[17].push_back(value1);
                distros2[17].push_back(value2);
            }
            /*
            if (line.substr(0, 4) == "M3CL")
            {
                string fline = line.substr(5);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[18].push_back(value1);
                distros2[18].push_back(value2);
            }
            */
            if (line.substr(0, 3) == "MCL")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[19].push_back(value1);
                distros2[19].push_back(value2);
            }
            if (line.substr(0, 4) == "MPIP")
            {
                string fline = line.substr(5);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[20].push_back(value1);
                distros2[20].push_back(value2);
            }

            if (line.substr(0, 3) == "PEC")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[21].push_back(value1);
                distros2[21].push_back(value2);
            }
            if (line.substr(0, 3) == "PIC")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[22].push_back(value1);
                distros2[22].push_back(value2);
            }
            if (line.substr(0, 4) == "MPEC")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[23].push_back(value1);
                distros2[23].push_back(value2);
            }
            if (line.substr(0, 4) == "MPIC")
            {
                string fline = line.substr(4);
                int p = fline.find(" ");
                string firstScore = fline.substr(0,p);
                string secondScore = fline.substr(p+1);
                double value1 = atof(firstScore.c_str());
                double value2 = atof(secondScore.c_str());
                distros0[24].push_back(value1);
                distros2[24].push_back(value2);
            }
        }
        if (third == true)
        {
            modifications.push_back(line);
        }
    }
    modifications.erase(modifications.begin()+0);

    for (size_t i=0; i<distros0.size(); i++)
    {
        sort(distros0[i].begin(), distros0[i].end(), sortByScore);
    }
    for (size_t i=0; i<distros2.size(); i++)
    {
        sort(distros2[i].begin(), distros2[i].end(), sortByScore);
    }    
    for (size_t i=0; i<distros0.size(); i++)
    {
        int index0 = 0;
        for (size_t j=0; j<distros0[i].size(); j++)
        {
                            
            if (distros0[i][j] != 0)
            {
                index0++;
            }
        }
        distros0[i].resize(index0);

        int index2 = 0;
        for (size_t j=0; j<distros2[i].size(); j++)
        {
                            
            if (distros2[i][j] != 0)
            {
                index2++;
            }
        }
        distros2[i].resize(index2);
    }
    for (size_t i=0; i<distros0.size(); i++)
    {
        sort(distros0[i].begin(), distros0[i].end(), sortByScoreLtoH);
    }
    for (size_t i=0; i<distros1.size(); i++)
    {
        sort(distros1[i].begin(), distros1[i].end(), sortByScoreLtoH);
    }
    for (size_t i=0; i<distros2.size(); i++)
    {
        sort(distros2[i].begin(), distros2[i].end(), sortByScoreLtoH);
    }

    vector<double> bandwidth(distros0.size());    
    if (cutoff > 0)
    {
        cout << "Computing bandwidths for selected lipid categories... ";
        for (size_t i=0; i<distros0.size(); i++)
        {
        
            // procede only if the distribution is not empty
            if (distros0[i].size() > 1)
            {
                // determine interquartile range
                double Q1, Q3, IQrange;
                int middle;
                int size = distros0[i].size();
                if (distros0[i].size() % 2 == 0)
                { 
                    middle = size/2;
                    if (middle % 2 == 0)
                    { 
                        Q1 = (distros0[i][middle/2] + distros0[i][middle/2-1])/2;
                        Q3 = (distros0[i][3*middle/2] + distros0[i][3*middle/2-1])/2;
                        IQrange =  Q3 - Q1;
                    }
                    else
                    {
                        Q1 = distros0[i][middle/2];
                        Q3 = distros0[i][3*middle/2];
                        IQrange =  Q3 - Q1;
                    }
                }
                else
                { 
                    middle = size/2 + 1;
                    if ((middle-1) % 2 == 0)
                    { 
                        Q1 = (distros0[i][(middle-1)/2] + distros0[i][(middle-1)/2-1])/2;
                        Q3 = (distros0[i][3*(middle-1)/2] + distros0[i][3*(middle-1)/2-1])/2;
                        IQrange =  Q3 - Q1;
                    }
                    else
                    {
                        Q1 = distros0[i][(middle-1)/2];
                        Q3 = distros0[i][3*(middle-1)/2];
                        IQrange =  Q3 - Q1;
                    }
                }
                //cout << i << "   " << Q3 << "   " << Q1 << "   " << IQrange << endl;

                // compute parameters for bandwidth selection
                double ss = distros0[i].size();    //sample size
                double a = 0.920*IQrange*pow(size,-1.0/7.0);
                double b = 0.912*IQrange*pow(size,-1.0/9.0);
                double Rk = 1.0/(2.0*sqrt(PI));
                double X, S, T;
                double t = 0.0;
                double s = 0.0;
                for (size_t j=0; j<distros0[i].size(); j++)
                {
                    for(size_t k=0; k<distros0[i].size(); k++)
                    {
                        X = distros0[i][j] - distros0[i][k];
                        s+= exp(-pow(X/a,2.0)/2.0)*(3.0-6.0*pow(X/a,2.0)+pow(X/a,4.0))/sqrt(2.0*PI);
                        t+= exp(-pow(X/b,2.0)/2.0)*(-15.0+45.0*pow(X/b,2.0)-15.0*pow(X/b,4.0)+pow(X/b,6.0))/sqrt(2.0*PI);
                    }
                }
                S = pow(ss*(ss-1.0),-1.0)*pow(a,-5.0)*s;
                T = -pow(ss*(ss-1.0),-1.0)*pow(b,-7.0)*t;

                double hh;
                double again = 10.0;

                // find the seed for Newton's method
                double func0 = 1;
                double h = .1;
                while (func0 > 0)
                {
                    double alpha;

                    alpha = 1.357*pow(S/T,1.0/7.0)*pow(h,5.0/7.0);


                    double sAlphaSum = 0.0;
                    for (size_t j=0; j<distros0[i].size(); j++)
                    {
                        for(size_t k=0; k<distros0[i].size(); k++)
                        {
                            X = distros0[i][j] - distros0[i][k];
                            sAlphaSum += (1/sqrt(2.0*PI))*exp(-pow(X/alpha,2.0)/2.0)*(3.0-6.0*pow(X/alpha,2.0)+pow(X/alpha,4.0));
                        }
                    }
                    double sAlpha  = pow(ss*(ss-1.0),-1.0)*pow(alpha, -5.0)*sAlphaSum;
                    func0 = pow(Rk/ss, 1.0/5.0)*pow(sAlpha, -1.0/5.0) - h;
                    //cout << h << "  f(h): " << func0 << endl;
                    h += 0.1;
                }
            
                // optimize the bandwidth
                while (again > 0.000000000001)
                {
                    // compute the function
                    double alpha;
                    alpha = 1.357*pow(S/T,1.0/7.0)*pow(h,5.0/7.0);
                    double sAlphaSum = 0.0;
                    for (size_t j=0; j<distros0[i].size(); j++)
                    {
                        for(size_t k=0; k<distros0[i].size(); k++)
                        {
                            X = distros0[i][j] - distros0[i][k];
                            sAlphaSum += (1/sqrt(2.0*PI))*exp(-pow(X/alpha,2.0)/2.0)*(3.0-6.0*pow(X/alpha,2.0)+pow(X/alpha,4.0));
                        }
                    }
                    double sAlpha  = pow(ss*(ss-1.0),-1.0)*pow(alpha, -5.0)*sAlphaSum;
                    double func;
                    func = pow(Rk/ss, 1.0/5.0)*pow(sAlpha, -1.0/5.0) - h;
                    double alphaD;
                    alphaD = 1.357*pow(S/T,1.0/7.0)*(5.0/7.0)*pow(h,-2.0/7.0);
                    double sAlphaSumD = 0.0;
                    for (size_t j=0; j<distros0[i].size(); j++)
                    {
                        for(size_t k=0; k<distros0[i].size(); k++)
                        {
                            X = distros0[i][j] - distros0[i][k];
                            sAlphaSumD += (1/sqrt(2.0*PI))*(exp(-pow(X/alpha,2.0)/2.0)*pow(X,2.0)*pow(alpha,-3.0)*alphaD*(3-6*pow(X/alpha,2.0)+pow(X/alpha,4.0)) 
                                    + exp(-pow(X/alpha,2.0)/2.0)*(12.0*pow(X,2.0)*pow(alpha,-3.0)*alphaD - 4.0*pow(X,4.0)*pow(alpha,-5.0)*alphaD));                
                        }
                    }
                    double sAlphaD = (1.0/(ss*(ss-1.0)))*(pow(alpha,-5.0)*sAlphaSumD - 5.0*pow(alpha,-6.0)*alphaD*sAlphaSum);
                    double funcD;
                    funcD = pow(Rk/ss,1.0/5.0)*(-1.0/5.0)*pow(sAlpha,-6.0/5.0)*sAlphaD - 1;
                    hh = h - func/funcD;
                    again = abs(hh-h);
                    h = abs(hh);
                }
            
                bandwidth[i] = abs(h);
                //cout << endl << "bandwidth " << i << "    " << bandwidth[i] << endl;
            }
        }
        cout << "done" <<endl;
        //return 0;
        for (size_t i=0; i<bandwidth.size(); i++)
        {
            //cout << bandwidth[i] << endl;
        }
    }

    vector<double> cutoffV(distros0.size(), 0);
    if (cutoff > 0)
    {
        vector<double> last(distros2.size(),0.0);
        vector<double> windowN(distros2.size(),0.0);

        for (size_t i=0; i<distros2.size(); i++)
        {
            //cout << i << "  " << last[i] << endl;
        }

        for (size_t i=0; i<distros2.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                //cout << distros2[i][distros2[i].size()-1] << endl;
                last[i] = max(distros0[i][distros0[i].size()-1], distros2[i][distros2[i].size()-1]);
            
                windowN[i] = floor((last[i]+5.0)/0.1); 
                //cout << first[i] << "  " << last[i] << "  " << windowN[i] << endl;
            }
        }

        cout << "Initialization of the kernel density estimates... ";
        // kernal density estimate for secondary hits
        vector<vector<vector<double> > > denEst2(distros2.size());
        vector<size_t> max2ind(distros2.size(),0);
        vector<double> max2val(distros2.size(),0);
        for (size_t i=0; i<distros2.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                size_t winN = (size_t) windowN[i];
                for (size_t k=0; k<winN+1; k++)
                {
                    double i2d = (double) k;
                    double point = i2d*(last[i]+5)/windowN[i];
                    double gkdSum = 0.0;
                    for (size_t j=0; j<distros2[i].size(); j++)
                    {
                        double score = distros2[i][j];
                        gkdSum += (1.0/(distros2[i].size()*bandwidth[i]))*pow(2.0*PI, -0.5)*exp(-0.5*pow((point-score)/bandwidth[i], 2.0));                
                    }
                    //if (i == 3)
                    //cout << point << "  " << gkdSum << endl;
                    if (gkdSum > max2val[i])
                    {
                        max2ind[i] = k;
                        max2val[i] = gkdSum;
                    }
                    vector<double> score_density;
                    score_density.push_back(point);
                    score_density.push_back(gkdSum);
                    denEst2[i].push_back(score_density);
                }
            }
        }


        // kernal density estimate for spectra
        vector<vector<vector<double> > > denEstSpec(distros0.size());
        vector<double> maxFull(distros0.size(),0);
        for (size_t i=0; i<distros0.size(); i++)
        {
            if (distros0[i].size() > 0)
            {
                size_t winN = (size_t) windowN[i];
                for (size_t k=0; k<winN+1; k++)
                {
                    double i2d = (double) k;
                    double point = i2d*(last[i]+5)/windowN[i];
                    double gkdSum = 0.0;
                    for (size_t j=0; j<distros0[i].size(); j++)
                    {
                        double score = distros0[i][j];
                        gkdSum += (1.0/(distros0[i].size()*bandwidth[i]))*pow(2.0*PI, -0.5)*exp(-0.5*pow((point-score)/bandwidth[i], 2.0));                
                    }
                    //if (i == 3)
                    //cout << point << "  " << gkdSum << endl;
                    if (gkdSum > maxFull[i])
                    {
                        maxFull[i] = gkdSum;
                    }
                    vector<double> score_spec;
                    score_spec.push_back(point);
                    score_spec.push_back(gkdSum);
                    denEstSpec[i].push_back(score_spec);
                }
            }
        }

        for (size_t i=0; i<denEst2.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                for (size_t j=0; j<denEst2[i].size(); j++)
                {
                    //cout << denEstSpec[i][j][0] << "    " << denEstSpec[i][j][1] << "    " << denEst2[i][j][1] << endl;
                }
            }
        }
    
        // proportion estimate - area subtraction (weighted) by difference between full and secondary denstiy estimates
        vector<double> proportion(distros0.size(), 0);
        for (size_t i=0; i<denEst2.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                double numSum = 0.0;
                double denSum = 0.0;
                for (size_t j=1; j<denEst2[i].size(); j++)
                {
                    if (denEst2[i][j][1]-denEstSpec[i][j][1] > 0)
                    {
                        double w = denEst2[i][j][1]-denEstSpec[i][j][1];
                        //double w = 1;
                        numSum += w*denEst2[i][j][1]*denEstSpec[i][j][1];
                        denSum += w*pow(denEst2[i][j][1], 2);
                    }
                
                }
                proportion[i] = 1-numSum/denSum;
                //cout << "proportion    " << i << "    " << proportion[i];
            }
        }

        // initial estimate of the kernal density for the identified spectra that are correct
        vector<vector<vector<double> > > denEst1(distros0.size());
        //cout << distros1.size() << endl;
        for (size_t i=0; i<distros0.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                size_t winN = (size_t) windowN[i];
                for (size_t k=0; k<winN+1; k++)
                {
                    double i2d = (double) k;
                    double point = i2d*(last[i]+5)/windowN[i];
                    double gkdSum = 0.0;
                    for (size_t j=0; j<distros0[i].size(); j++)
                    {
                        //cout << 1.0/distros0[i].size() << endl;
                        double score = distros0[i][j];
                        gkdSum += (1.0/(distros0[i].size()*bandwidth[i]))*pow(2.0*PI, -0.5)*exp(-0.5*pow((point-score)/bandwidth[i], 2.0));                
                    }
                    //cout << point << "  " << gkdSum - denEst2[i][k] << endl;
                    vector<double> score_density1;
                    score_density1.push_back(point);
                    score_density1.push_back(max(0.0,gkdSum - denEst2[i][k][1]));
                    denEst1[i].push_back(score_density1);
                    //cout << denEst1[i][k][0] << "    " << denEst1[i][k][1] << endl;
                }
            }
        }
    
        // initial estimate of the kernal density for the identified spectra from the combined false and true densities
        vector<vector<vector<double> > > denEstFull(distros0.size());
        for (size_t i=0; i<distros0.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                size_t winN = (size_t) windowN[i];
                for (size_t k=0; k<winN+1; k++)
                {
                    double i2d = (double) k;
                    double point = i2d*(last[i]+5)/windowN[i];
                    double gkdSum = proportion[i]*denEst1[i][k][1] + (1-proportion[i])*denEst2[i][k][1];
                    vector<double> score_densityFull;
                    score_densityFull.push_back(point);
                    score_densityFull.push_back(gkdSum);
                    denEstFull[i].push_back(score_densityFull);
                    //cout << denEstFull[i][k][0] << "    " << denEstFull[i][k][1] << endl;
                }
            }
        }
    
        // setup of the density values for each spectrum score
        vector<vector<vector<double> > > distros1Up(21), distros2Up(21), distrosFullUp(21);
        vector<vector<vector<int> > > distros1UpI(21), distros2UpI(21), distrosFullUpI(21);
        for (size_t i=0; i<distros1Up.size(); i++)
        {
            distros1Up[i].resize(distros0[i].size());
            distros2Up[i].resize(distros0[i].size());
            distrosFullUp[i].resize(distros0[i].size());
            distros1UpI[i].resize(distros0[i].size());
            distros2UpI[i].resize(distros0[i].size());
            distrosFullUpI[i].resize(distros0[i].size());
        }
        for (size_t i=0; i<distros1Up.size(); i++)
        {
            for (size_t j=0; j<distros1Up[i].size(); j++)
            {
                distros1Up[i][j].resize(2);
                distros2Up[i][j].resize(2);
                distrosFullUp[i][j].resize(2);
                distros1UpI[i][j].resize(2);
                distros2UpI[i][j].resize(2);
                distrosFullUpI[i][j].resize(2);
            }
        }

        for (size_t i=0; i<distros1Up.size(); i++)
        {
            for (size_t j=0; j<distros1Up[i].size(); j++)
            {
                //cout << distros0[i][j] << endl;
                for (size_t k=0; k<denEst1[i].size(); k++)           
                {
                    if (distros0[i][j] < denEst1[i][k][0])
                    {
                        distros1Up[i][j][0] = distros0[i][j];            // score
                        double slope = (denEst1[i][k][1] - denEst1[i][k-1][1])/(denEst1[i][k][0] - denEst1[i][k-1][0]);
                        distros1Up[i][j][1] = slope*(distros0[i][j] - denEst1[i][k][0]) + denEst1[i][k][1];        // distribution position of score
                        distros1UpI[i][j][0] = k-1;
                        distros1UpI[i][j][1] = k;
                        //cout << distros1Up[i][j][0] << "    " << distros1Up[i][j][1] << endl;
                        break;
                    }
                }
            }
            for (size_t j=0; j<distros2Up[i].size(); j++)
            {
                for (size_t k=0; k<denEst2[i].size(); k++)           
                {
                    if (distros0[i][j] < denEst2[i][k][0])
                    {
                        distros2Up[i][j][0] = distros0[i][j];            // score
                        double slope = (denEst2[i][k][1] - denEst2[i][k-1][1])/(denEst2[i][k][0] - denEst2[i][k-1][0]);
                        distros2Up[i][j][1] = slope*(distros0[i][j] - denEst2[i][k][0]) + denEst2[i][k][1];        // distribution position of score
                        distros2UpI[i][j][0] = k-1;
                        distros2UpI[i][j][1] = k;
                        //cout << distros2Up[i][j][0] << "    " << distros2Up[i][j][1] << endl;
                        break;
                    }
                }
            }
            for (size_t j=0; j<distrosFullUp[i].size(); j++)
            {
                //cout << distros1[i][j] << endl;
                for (size_t k=0; k<denEstFull[i].size(); k++)           
                {
                    if (distros0[i][j] < denEstFull[i][k][0])
                    {
                        //cout << denEst2[i][k-1][0] << "  " << distros0[i][j] << "  " << denEst2[i][k][0] << endl;
                        distrosFullUp[i][j][0] = distros0[i][j];
                        double slope = (denEstFull[i][k][1] - denEstFull[i][k-1][1])/(denEstFull[i][k][0] - denEstFull[i][k-1][0]);
                        distrosFullUp[i][j][1] = slope*(distros0[i][j] - denEstFull[i][k][0]) + denEstFull[i][k][1];
                        distrosFullUpI[i][j][0] = k-1;
                        distrosFullUpI[i][j][1] = k;
                        //cout << distrosFullUp[i][j][0] << "    " << distrosFullUp[i][j][1] << endl;
                        break;
                    }
                }
            }
        }
        cout << "done" << endl;
        cout << "Computation of the density estimates with an EM-like algorithm... "; 
        //cout << proportion[0] << endl;
        // EM like algorithm for kernal density estimates
        vector<vector<double> > prob(distros0.size());
        for (size_t i=0; i<distros0.size(); i++)
        {
        
            if (distros2[i].size() > 0)
            {    
                prob[i].resize(distros0[i].size(), 0.5);

                //cout << prob.size() << "   " << prob[i].size() << endl; // 618
                //double prop = 1.0;
                double maxProbDiff = 1;
                while (maxProbDiff > 0.00000000000001)
                {
                    //cout << i << "    " << maxProbDiff << endl;
                    maxProbDiff = 0;            
                    //cout << setprecision(20) << proportion[i] << endl;

                    // compute probability that spectrum j is correct
                    for (size_t j=0; j<prob[i].size(); j++)
                    {
                         double pro = proportion[i]*distros1Up[i][j][1]/(proportion[i]*distros1Up[i][j][1] + (1-proportion[i])*distros2Up[i][j][1]);
                         if (abs(pro - prob[i][j]) > maxProbDiff)
                         {
                             maxProbDiff = abs(pro - prob[i][j]);
                         }
                         prob[i][j] = pro;
                        //cout << prob[i][j] << endl;
                    }

                    // kernal density update
                    for (size_t j=0; j<denEst1[i].size(); j++)
                    {
                        double point = denEst1[i][j][0];
                        double kerSum = 0.0;
                        double probSum = 0.0;
                        for (size_t k=0; k<distros0[i].size(); k++)
                        {
                            double score = distros0[i][k];
                            kerSum += prob[i][k]*pow(2.0*PI, -0.5)*exp(-0.5*pow((point-score)/bandwidth[i], 2.0));
                            probSum += prob[i][k];
                        }
                        denEst1[i][j][1] = kerSum/(bandwidth[i]*probSum);
                    }
                
                    /*
                    // full kernal update
                    for (size_t j=0; j<denEstFull[i].size(); j++)
                    {
                        denEstFull[i][j][1] = denEst1[i][j][1] + denEst2[i][j][1];
                    }
                    */

                    // update of the positive density values for the spectrum scores

                    for (size_t j=0; j<distros1Up[i].size(); j++)
                    {
                        int n = distros1UpI[i][j][0]; // m-1
                        int m = distros1UpI[i][j][1];                                
                        double slope = (denEst1[i][m][1] - denEst1[i][n][1])/(denEst1[i][m][0] - denEst1[i][n][0]);
                        distros1Up[i][j][1] = slope*(distros0[i][j] - denEst1[i][m][0]) + denEst1[i][m][1];
                        //cout << distros1Up[i][j][0] << "    " << distros1Up[i][j][1] << endl;
                    }
                }
            }
            //cout << proportion[i] << endl;
        }
        cout << "done" << endl;
        for (size_t i=0; i<prob.size(); i++)
        {
            if (distros2[i].size() > 0)
            {    
                for (size_t j=0; j<prob[i].size(); j++)
                {
                    //cout << prob[i][j] << endl;
                }
            }
        }

        //cout << endl;
        for (size_t i=0; i<denEstFull.size(); i++)
        {    
            if (distros2[i].size() > 0)
            {    
                for (size_t j=0; j<denEstFull[i].size(); j++)
                {
                    denEstFull[i][j][1] = proportion[i]*denEst1[i][j][1] + (1-proportion[i])*denEst2[i][j][1];
                    //cout << denEstFull[i][j][0] << "    " << denEstFull[i][j][1] << endl;
                }
            }
        }

        for (size_t i=0; i<denEstFull.size(); i++)
        {    
            if (distros2[i].size() > 0)
            {    
                //cout << denEstFull[i][j][0] << "    " << denEst1[i][j][1] << "    " << denEst2[i][j][1] << "    " << proportion[i]*denEst1[i][j][1] << "    " << (1-proportion[i])*denEst2[i][j][1] << "    " << denEstFull[i][j][1] << endl;
                for (size_t j=0; j<denEstFull[i].size(); j++)
                {
                    //cout << denEstFull[i][j][0] << "    " << "    " << proportion[i]*denEst1[i][j][1] << "    " << denEstFull[i][j][1] << endl;
                    //cout << denEstFull[i][j][0] << "    " << denEst1[i][j][1] << "    " << denEst2[i][j][1] << "    " << proportion[i]*denEst1[i][j][1] << "    " << (1-proportion[i])*denEst2[i][j][1] << "    " << denEstFull[i][j][1] << endl;
                }
            }
        }

        for (size_t i=0; i<distros0.size(); i++)
        {
            if (distros2[i].size() > 0)
            {
                reverse(denEst1[i].begin(), denEst1[i].end());
                reverse(denEstFull[i].begin(), denEstFull[i].end());
            }
        }
    
        vector<vector<double> > area1(denEst1.size());
        vector<vector<double> > areaFull(denEst1.size());
        //vector<vector<double> > gridPoint(denEst1.size());
        for (size_t i=0; i<area1.size(); i++)
        {
            area1[i].resize(denEst1[i].size());
            areaFull[i].resize(denEstFull[i].size());
            //gridPoint[i].resize(denEst1[i].size());
        }


        for (size_t i=0; i<denEst1.size(); i++)
        {
            if (distros2[i].size() > 0)
                {    
                area1[i][0] = proportion[i]*denEst1[i][0][1]*(denEst1[i][0][0]-denEst1[i][1][0]);
                //gridPoint[i][0] = 0;
                for (size_t j=1; j<denEst1[i].size(); j++)
                {
                    area1[i][j] = (denEst1[i][j-1][0]-denEst1[i][j][0])*proportion[i]*denEst1[i][j][1] + area1[i][j-1];
                    //gridPoint[i][j] = denEst1[i][j][0];
                    //cout << denEst1[i][j][0] << "    " << area1[i][j] << endl;
                }
            }
        }

        for (size_t i=0; i<denEstFull.size(); i++)
        {
            if (distros2[i].size() > 0)
            {    
                areaFull[i][0] = denEstFull[i][0][1]*(denEstFull[i][0][0]-denEstFull[i][1][0]);;
                for (size_t j=1; j<denEstFull[i].size(); j++)
                {
                    areaFull[i][j] = (denEstFull[i][j-1][0]-denEstFull[i][j][0])*denEstFull[i][j][1] + areaFull[i][j-1];
                    //cout << denEstFull[i][j][0] << "    " << areaFull[i][j] << endl;
                }
            }
        }
    
        //vector<double> cutoffV(distros0.size(), 0);
        for (size_t i=0; i<cutoffV.size(); i++)
        {
            if (i<6 || (i>8 && i<17) || i == 20)
            {
                if (distros2[i].size() > 0)
                {
                    for (size_t j=0; j<area1[i].size(); j++)
                    {
                        double ratio = area1[i][j]/areaFull[i][j];
                        if (ratio > cutoff)
                        {
                            cutoffV[i] = denEst1[i][j][0];
                        }
                        //cout << denEst1[i][j][0] << "    " << area1[i][j] << "    " << areaFull[i][j] << "    " << area1[i][j]/areaFull[i][j] << endl;
                    }
                    //cout <<  i << "    cutoff    " << cutoff << "    " << cutoffV[i] << endl;
                }
            }
        }
    }


    /*
    vector<vector<int> > dupeLipidIndex(21);
    for (size_t i=0; i<dupeLipidIndex.size(); i++)
    {
        dupeLipidIndex[i].resize(distros1lipid[i].size());
        for (size_t j=0; j<dupeLipidIndex[i].size(); j++)
        {
            dupeLipidIndex[i][j] = 0;
        }
    }
    
    for (size_t i=0; i<distros1lipid.size(); i++)
    {
        for (size_t j=1; j<distros1lipid[i].size(); j++)
        {
            for (size_t k=0; k<j; k++)
            {
                if (distros1lipid[i][j] == distros1lipid[i][k])
                {
                    dupeLipidIndex[i][j] = 1;
                }
            }
        }
    }
    */
    //for (size_t i=0; i<dupeLipidIndex[6].size(); i++)
    //{
    //    cout << dupeLipidIndex[6][i] << endl;
    //}

    vector<vector<double> > distros1Temp;
    distros1Temp = distros1;
    for (size_t i=0; i<distros1Temp.size(); i++)
    {
        sort(distros1Temp[i].begin(), distros1Temp[i].end(), sortByScore);
    }    

    for (size_t i=0; i<distros1Temp.size(); i++)
    {        
        for (size_t j=0; j<distros1Temp[i].size(); j++)
        {
            //cout << distros1Temp[i][j] << endl;    
        }
    }    

    for (size_t i=0; i<cutoffV.size(); i++)
    {
        if ((cutoffV[i] == 0) && (distros1[i].size() > 0) && (topPercent < 100)) 
        {
            int tP = (int) topPercent;
            int cutoffindex = distros1Temp[i].size()*tP/100 - 1;
            if (cutoffindex < 0)
            {
                cutoffV[i] = distros1Temp[i][0] + 1;
            }
            else 
            {
                cutoffV[i] = distros1Temp[i][cutoffindex];
            }
            //cout << cutoffindex << endl;
            //cout << distros1Temp[i].size()*topPercent/100 << endl;
            
            /*
            vector<double> distros1Temp;
            distros1Temp = distros1[i];
            sort(distros1Temp.begin(), distros1Temp.end(), sortByScore);
            //cout << distros1[i].size() << "    " << distros1lipid[i].size() << endl;
            vector<double> percentCutoff;
            for (size_t j=0; j<dupeLipidIndex[i].size(); j++)
            {
                if (dupeLipidIndex[i][j] == 0)
                {
                    percentCutoff.push_back(distros1Temp[j]);
                }
            }
            //cout << percentCutoff.size() << endl;
            //cout << topPercent << endl;
            double cutProportion = percentCutoff.size()*(topPercent/100.0);
            //cout << cutProportion << endl;
            for (size_t j=0; j<percentCutoff.size(); j++)
            {
                
                //cout << i  << "    " << j << "    " << jDoub/size << "    " << cutProportion << endl;
                if (j+1 < cutProportion)
                {
                    cutoffV[i] = percentCutoff[j];
                }
            }
            */
            //cout << cutIndex << "    " << percentCutoff[cutIndex] << endl;
            //cout << "cutoff    " << i << "    " << cutProportion << "    " << cutoffV[i] << "    " << percentCutoff.size() << endl;
        }
        
    }

    for (size_t i=0; i<cutoffV.size(); i++)
    {
        //cout << i << "    " << cutoffV[i] << endl;
    }

    cout << "Writing results to .mzTab file... ";

    for (size_t i=0; i<spectra.size(); i++)
    {
        //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
        if (spectra[i].lipidIdent.substr(0,2) == "PC" && spectra[i].score >= cutoffV[0] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << "    " << cutoffV[0] << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "PC" && spectra[i].score >= cutoffV[11] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << "    " << cutoffV[0] << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,3) == "PE(" && spectra[i].score >= cutoffV[1] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,3) == "PE(" && spectra[i].score >= cutoffV[12] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,3) == "PI(" && spectra[i].score >= cutoffV[2] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,3) == "PI(" && spectra[i].score >= cutoffV[13] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,2) == "PG" && spectra[i].score >= cutoffV[3] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "PG" && spectra[i].score >= cutoffV[14] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,2) == "PS" && spectra[i].score >= cutoffV[4] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "PS" && spectra[i].score >= cutoffV[15] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,2) == "PA" && spectra[i].score >= cutoffV[5] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "PA" && spectra[i].score >= cutoffV[16] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,2) == "SM" && spectra[i].score >= cutoffV[6] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "SM" && spectra[i].score >= cutoffV[17] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }

        if (spectra[i].lipidIdent.substr(0,6) == "PE-Cer" && spectra[i].score >= cutoffV[21] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,6) == "PE-Cer" && spectra[i].score >= cutoffV[23] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,6) == "PI-Cer" && spectra[i].score >= cutoffV[22] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,6) == "PI-Cer" && spectra[i].score >= cutoffV[24] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }





        if (spectra[i].lipidIdent.substr(0,2) == "CL" && spectra[i].mod != "-2H" && spectra[i].score >= cutoffV[7] && spectra[spectra.size()-1].mod != "-2H + Na" && spectra[spectra.size()-1].mod != "-2H + Li" && spectra[spectra.size()-1].mod != "-2H + K" && spectra[spectra.size()-1].mod != "-2H + 3Na" && spectra[spectra.size()-1].mod != "-2H + 3Li" && spectra[spectra.size()-1].mod != "-2H + 3K")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,2) == "CL" && spectra[i].mod != "-2H" && spectra[i].score >= cutoffV[19] && (spectra[spectra.size()-1].mod == "-2H + Na" || spectra[spectra.size()-1].mod == "-2H + Li" || spectra[spectra.size()-1].mod == "-2H + K"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        /*
        if (spectra[i].lipidIdent.substr(0,2) == "CL" && spectra[i].mod != "-2H" && spectra[i].score >= cutoffV[18] && (spectra[spectra.size()-1].mod == "-2H + 3Na" || spectra[spectra.size()-1].mod == "-2H + 3Li" || spectra[spectra.size()-1].mod == "-2H + 3K"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        */
        if (spectra[i].lipidIdent.substr(0,2) == "CL" && spectra[i].mod == "-2H" && spectra[i].score >= cutoffV[8])
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,3) == "PIP" && spectra[i].mod != "-2H" && spectra[i].score >= cutoffV[9] && spectra[i].mod != "+Na" && spectra[spectra.size()-1].mod != "+K" && spectra[spectra.size()-1].mod != "+Li")
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,3) == "PIP" && spectra[i].mod != "-2H" && spectra[i].score >= cutoffV[20] && (spectra[i].mod == "+Na" || spectra[spectra.size()-1].mod == "+K" || spectra[spectra.size()-1].mod == "+Li"))
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
        if (spectra[i].lipidIdent.substr(0,3) == "PIP" && spectra[i].mod == "-2H" && spectra[i].score >= cutoffV[10])
        {
            //cout << spectra[i].lipidIdent << "  " << spectra[i].charge << "  " << spectra[i].score << endl;
            lipids.push_back(Lipid());
            lipids[lipids.size()-1].filled = false;
            lipids[lipids.size()-1].lipidIdent = spectra[i].lipidIdent;
            lipids[lipids.size()-1].lipidFormula = spectra[i].lipidFormula;
            lipids[lipids.size()-1].expMass = spectra[i].expMass;
            lipids[lipids.size()-1].calcMass = spectra[i].calcMass;
            lipids[lipids.size()-1].charge = spectra[i].charge;
            lipids[lipids.size()-1].mod = spectra[i].mod;
            lipids[lipids.size()-1].score = spectra[i].score;

            stringstream spectrastream;
            spectrastream << spectra[i].number;
            lipids[lipids.size()-1].spectra = spectrastream.str();

            stringstream timestream;
            timestream << setprecision(4) << spectra[i].time;
            lipids[lipids.size()-1].time = timestream.str();

            stringstream scorestream;
            scorestream << spectra[i].score;
            lipids[lipids.size()-1].scores = scorestream.str();
        }
    }

    for (size_t i=0; i<lipids.size(); i++)
    {
        if (lipids[i].time == "10000000")
        {
            lipids[i].time = "null";
        }
    }

    for (size_t i=0; i<lipids.size(); i++)
    {
        if (lipids[i].filled == false)
        {
            for (size_t j=i+1; j<lipids.size(); j++)
            {
                if ((lipids[i].lipidIdent == lipids[j].lipidIdent) && (lipids[i].mod == lipids[j].mod))
                {
                    lipids[j].filled = true;
                    lipids[i].time = lipids[i].time + "|" + lipids[j].time;
                    lipids[i].spectra = lipids[i].spectra + "|" + lipids[j].spectra;
                    lipids[i].scores = lipids[i].scores + "|" + lipids[j].scores;
                }
            }
        }
    }
    size_t dot = filename.find_last_of(".");
    string dotless = filename.substr(0,dot);

    string outfile = dotless + ".mzTab";
    ofstream gfile(outfile.c_str());
    gfile << "MTD    mzTab-version    1.0.0" << endl
            << "MTD    mzTab-mode    Summary" << endl
            << "MTD    mzTab-type    Identification" << endl
            << "MTD    description    Identification of phospholpids from tandem MS spectra" << endl
            << "MTD    ms_run[1]-location    file://" << location << endl
            << "MTD    smallmolecule_search_engine_score[1]    [, , Greazy,]" << endl
            << "MTD    fixed_mod[1]    [MS:1002453: No fixed modifications searched]" << endl;
    int modIndex = 1;
    for (size_t i=0; i<modifications.size(); i++)
    {
        gfile << "MTD    variable_mod[" << modIndex << "]    [CHEMMOD, CHEMMOD:" << modifications[i] << "]" << endl;
        modIndex++;
    }            
    gfile << endl << "SMH    identifier    chemical_formula    smiles    inchi_key    description    exp_mass_to_charge    calc_mass_to_charge    charge    retention_time    taxid    species    database    database_version    spectra_ref    search_engine    best_search_engine_score    modifications    opt_ms_run[1]_all_scores" << endl;
    
    for (size_t i=0; i<lipids.size(); i++)
    {
        if (lipids[i].filled == false)
        {
            gfile << "SML    " << lipids[i].lipidIdent << "    " << lipids[i].lipidFormula << "    " << "null    " << "null    " << "null    " 
                << lipids[i].expMass << "    " << lipids[i].calcMass << "    " << lipids[i].charge << "    " << lipids[i].time << "    null    " << "null    " << "null    " << "null    "
                << "ms_run[1]:scan=" << lipids[i].spectra << "    [,,Greazy,]    " << lipids[i].score << "    CHEMMOD:" << lipids[i].mod << "    " << lipids[i].scores << endl;
        }
    }
    cout << "done" << endl;
    return 0;
}