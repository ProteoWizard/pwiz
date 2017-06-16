#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "isotopes.h"

/*
   A program for calculating isotope distributions
   2013 James E. Redman
   www.kombyonyx.com
   
   Based on J.A.Yergey, Int. J. Mass Spectrom. Ion Phys. 1983, 52, 337-349
*/


//these are two constants used for calculating Gaussian peaks
const double GC1 = 0.9394372785;
const double GC2 = 2.772588722;


using namespace std;

double fdiff(int f1, int f2)    //evaluate f1!/f2!
{
   unsigned int result = 1;     //use unsigned to get more range
   if (f1 >= f2)
   {
      for (int i = f1; i > f2; i--)
      {
         result *= i;
      }
      return result;
   }
   else
   {
      for (int i = f2; i > f1; i--)
      {
         result *= i;
      }
      return (double) 1.0 / (double) result;
   }
}

inline double powdiff(double r, double f1, double f2)   // return r^(f1-f2)
{
   return pow(r, f1 - f2);
}

int comp_isotope_abundance(const void *p1, const void *p2)      //function for comparing isotope abundances (for sorting)
{
   double ans = ((isotope *) p2)->abundance - ((isotope *) p1)->abundance;
   if (ans > 0)
      return 1;
   else if (ans < 0)
      return -1;
   return 0;
}

isotope::isotope(double _mass, double _abundance):mass(_mass), abundance(_abundance)
{
}

BaseSpectrum::~BaseSpectrum()
{
}

PeakList::PeakList()
{
}

PeakList::PeakList(int _npeaks)
{
   peaks.resize(_npeaks);
}

int PeakList::size()
{
   return peaks.size();
}

void PeakList::resize(int _size)
{
   peaks.resize(_size);
}

double PeakList::intensity(double mass)
{
   peaks.push_back(peak(mass, 0));      //temporary fudge  - fill in this fn later
   return peaks.back().abundance;       // should return reference to the intensity at a particular mass
}

peak & PeakList::operator[](int idx)    //return peak [idx]
{
   return peaks[idx];
}

void PeakList::push_back(peak & ob)     //add a peak at the end
{
   peaks.push_back(ob);
}

void PeakList::push_front(peak & ob)
{
   peaks.push_front(ob);
}

void PeakList::pop_back()
{
   peaks.pop_back();
}

void PeakList::pop_front()
{
   peaks.pop_front();
}

IntensityList::IntensityList()
{
}

IntensityList::IntensityList(int _npeaks)
{
   peaks.resize(_npeaks);
}

int IntensityList::size()
{
   return peaks.size();
}

void IntensityList::resize(int _npeaks)
{
   peaks.resize(_npeaks);
}

double &IntensityList::operator[] (int idx)
{
   return peaks[idx];
}

double IntensityList::intensity(double mass)
{
   return 0;                    //a dummy return value for now - need to match peaks and return appropriate peak intensity
}

void IntensityList::push_back(peak & ob)        //add a peak at the end -- not really work correctly as mass is ignored
{
   peaks.push_back(ob.abundance);
}

void IntensityList::push_front(peak & ob)       //add peak at the beginning - ignoring the mass
{
   peaks.push_front(ob.abundance);
}

void IntensityList::pop_back()
{
   peaks.pop_back();
}

void IntensityList::pop_front()
{
   peaks.pop_front();
}

GaussianList::GaussianList():fwhm(0.1)
{
}

GaussianList::GaussianList(int _npeaks):fwhm(0.5), PeakList(_npeaks)
{
}

double GaussianList::intensity(double mass)     //calculate intensity using a sum of gaussians of width fwhm
{
   deque < peak >::iterator lp = peaks.begin();
   double _intensity = 0;
   while (lp != peaks.end())
   {
      _intensity +=
         lp->abundance * GC1 * exp(-pow((mass - lp->mass) / fwhm, 2) * GC2) /
         fwhm;
      lp++;
   }
   return _intensity;
}

AtomIsoAbun::AtomIsoAbun():niso(NULL), z(0), isotopes(NULL)
{
   symbol[0] = '\0';
}

AtomIsoAbun::AtomIsoAbun(int _z, int _niso):z(_z), niso(_niso)
{
   isotopes = new isotope[niso];        //create arrays to hold masses and abundances
}

AtomIsoAbun::~AtomIsoAbun()
{
   delete[]isotopes;
}

AtomIsoAbun::AtomIsoAbun(const AtomIsoAbun & ob)
{
   niso = ob.niso;
   z = ob.z;
   isotopes = new isotope[niso];
   for (int i = 0; i < niso; i++)
   {
      isotopes[i] = ob.isotopes[i];
   }
   strcpy(symbol, ob.symbol);
}

AtomIsoAbun & AtomIsoAbun::operator=(AtomIsoAbun & ob)
{
   if (this != &ob)
   {
      delete[]isotopes;
      niso = ob.niso;
      z = ob.z;
      isotopes = new isotope[niso];
      for (int i = 0; i < niso; i++)
      {
         isotopes[i] = ob.isotopes[i];
      }
      strcpy(symbol, ob.symbol);
   }
   return *this;
}

bool AtomIsoAbun::operator<(const AtomIsoAbun & ob) const       // allow atoms to be compared/sorted by element symbol
{
   return strcmp(symbol, ob.symbol) < 0 ? true : false;
}

bool AtomIsoAbun::operator==(const AtomIsoAbun & ob) const              // == operator for atoms, does the comparison according to the element symbol
{
   return !strcmp(symbol, ob.symbol);
}

void AtomIsoAbun::Setniso(int _niso)
{
   delete[]isotopes;
   niso = _niso;
   isotopes = new isotope[niso];
}

int AtomIsoAbun::Getniso()
{
   return niso;
}

void AtomIsoAbun::Sort_abundance()
{
   qsort(isotopes, niso, sizeof(isotope), comp_isotope_abundance);
}

ostream & operator<<(ostream & stream, AtomIsoAbun & ob)
{
   stream << ob.z << ' ' << ob.symbol << '\n';
   stream << ob.niso << '\n';
   for (int i = 0; i < ob.niso; i++)
   {
      stream << ob.isotopes[i].mass << ' ' << ob.isotopes[i].
         abundance << '\n';
   }
   return stream;
}

istream & operator>>(istream & stream, AtomIsoAbun & ob)
{
   delete[]ob.isotopes;
   stream >> ob.z >> ob.symbol;
   stream >> ob.niso;
   ob.isotopes = new isotope[ob.niso];
   for (int i = 0; i < ob.niso; i++)
   {
      stream >> ob.isotopes[i].mass >> ob.isotopes[i].abundance;
   }
   return stream;
}

MolComposition::MolComposition():totalnat(0), nat(NULL), atom(NULL), z(0)
{
}

MolComposition::MolComposition(int _totalnat)
{
   totalnat = _totalnat;
   atom = new AtomIsoAbun[totalnat];
   nat = new int[totalnat];
   z = 0;
}

MolComposition::MolComposition(const MolComposition & ob)
{
   totalnat = ob.totalnat;
   z = ob.z;
   atom = new AtomIsoAbun[totalnat];
   nat = new int[totalnat];
   for (int i = 0; i < totalnat; i++)
   {
      nat[i] = ob.nat[i];
      atom[i] = ob.atom[i];
   }
}

MolComposition::~MolComposition()
{
   delete[]nat;
   delete[]atom;
}

MolComposition & MolComposition::operator=(MolComposition & ob)
{
   if (this != &ob)
   {
      delete[]nat;
      delete[]atom;
      totalnat = ob.totalnat;
      z = ob.z;
      atom = new AtomIsoAbun[totalnat];
      nat = new int[totalnat];
      for (int i = 0; i < totalnat; i++)
      {
         nat[i] = ob.nat[i];
         atom[i] = ob.atom[i];
      }
   }
   return *this;
}

ostream & operator<<(ostream & stream, MolComposition & ob)
{
   stream << ob.totalnat << '\n';
   stream << ob.z << '\n';
   for (int i = 0; i < ob.totalnat; i++)
   {
      stream << ob.nat[i] << '\n';
      stream << ob.atom[i];
   }
   return stream;
}

istream & operator>>(istream & stream, MolComposition & ob)
{
   delete[]ob.nat;
   delete[]ob.atom;
   stream >> ob.totalnat;
   stream >> ob.z;
   ob.atom = new AtomIsoAbun[ob.totalnat];
   ob.nat = new int[ob.totalnat];
   for (int i = 0; i < ob.totalnat; i++)
   {
      stream >> ob.nat[i];
      stream >> ob.atom[i];
   }
   return stream;
}

void MolComposition::Settotalnat(int _totalnat)
{
   delete[]nat;
   delete[]atom;
   totalnat = _totalnat;
   atom = new AtomIsoAbun[totalnat];
   nat = new int[totalnat];
}

int MolEnsemble::GenerateEnsemble(MolComposition & comp, double thr)    //calculate the isotopomer distribution, with probability threshold thr
{                               //calculates masses on the fly too
   totalnat = comp.totalnat;
   delete[]elements;            //get rid of any existing array of atom definitions and list of isotopomers
   molecule.clear();
   elements = new AtomEnsemble[totalnat];       //create a new ensemble for each element
   for (int i = 0; i < totalnat; i++)
   {                            //loop thru each atom
      AtomDistribution temp;    //create a new working distribution of atoms...
      double maxabundance;      //maximum abundance found so far
      double *localmax = new double[comp.atom[i].Getniso()];    //an array containing the local abundance maxima
      int maxidx = 0;
      temp.number.resize(comp.atom[i].Getniso(), 0);    // ...all with zero contents
      elements[i].atomdef = comp.atom[i];       //transfer the atom isotope definitions
      vector < int >::iterator j = temp.number.begin(); //iterator to the isotope under consideration
      const vector < int >::iterator end = temp.number.end() - 1;       //an iterator pointing at the last element of the array
      *j = comp.nat[i];         //initialize first (most abundant) isotope with the total number of atoms of this element
      maxabundance = temp.abundance = pow(comp.atom[i].isotopes[0].abundance, *j);      //calculate the abundance of the first permutation
      temp.CalculateMass(&comp.atom[i]);        //calculate mass of first permutation
      elements[i].distribution.push_back(temp); //add the initial state to the list
      localmax[0] = temp.abundance;
      bool backtrack = false;
      do
      {
         if (j != end && !backtrack)
         {                      //if not on the final isotope
            (*j)--;             //decrement number of atoms
            j++;                //move to the next isotope
            maxidx++;
            (*j)++;             //increment the number of atoms of the new isotope
            list < AtomDistribution >::iterator last =
               elements[i].distribution.end();
            last--;             // make an iterator to the last distribution added
            temp.abundance = last->abundance;
            for (int k = 0; k < comp.atom[i].Getniso(); k++)
            {                   //calculate abundance - based on the last calculated distribution
               temp.abundance *= fdiff(last->number[k], temp.number[k]);
               temp.abundance *=
                  powdiff(comp.atom[i].isotopes[k].abundance, temp.number[k],
                          last->number[k]);
            }
            temp.CalculateMass(&comp.atom[i]);
            if (temp.abundance > maxabundance * thr)
            {                   //add to list if it is abundant enough
               elements[i].distribution.push_back(temp);        //add the state to the list
               if (temp.abundance > maxabundance)
               {
                  maxabundance = temp.abundance;
               }
            }
            localmax[maxidx] = temp.abundance;  //have just gone to next isotope - therefore always a new maximum (as it would have been zero)
            if (temp.abundance > 1)
            {                   //check that value is plausible and die if it isn't
               delete[]elements;
               elements = NULL;
               delete[]localmax;
               return 6;        //cleanup and return error if something (math error) has gone wrong
            }
         }
         else
         {
            backtrack = false;
            vector < int >::iterator oldj = j;
            bool thru;
            do
            {
               if (maxidx > 0 && localmax[maxidx] > localmax[maxidx - 1])
               {                // check whether to carry back the maximum abundance to the previous level
                  localmax[maxidx - 1] = localmax[maxidx];
                  thru = false;
               }
               else
               {
                  thru = true;
               }
               j--;
               maxidx--;
            }
            while (j >= temp.number.begin() && !*j);    //go back to find the previous isotope with something in it, but stop at the beginning

            if (j >= temp.number.begin())
            {                   //check that have not run to the start of the array
               int carry = *oldj;       //this swaps the values of the last slot and the new adjacent slot (use a swap in case the two slots are the same)
               *oldj = *(j + 1);        //empty the last slot
               *(j + 1) = carry;        //transfer all the stuff from the end (or current isotope) back to the adjacent empty slot
            }
            if (thru && localmax[maxidx + 1] < maxabundance * thr)
            {                   //if it is pointless decreasing the number of the current isotope...
               *j += *(j + 1);  // sum up adjacent levels
               *(j + 1) = 0;
               backtrack = true;        //go up a level
            }
         }
      }
      while (j >= temp.number.begin()); //carry on until there is nothing left in the first isotope
      delete[]localmax;
   }
   //now multiplex the atom ensembles
   list < AtomDistribution >::iterator * atselect = new list < AtomDistribution >::iterator[totalnat];  //an array of list iterators to point at the selected atom distributions
   double *dummyabun = new double[totalnat + 1], *tempabun;
   tempabun = dummyabun + 1;    //create an abundance array with an element at -1 for initialization
   tempabun[-1] = 1;
   double *dummymass = new double[totalnat + 1], *tempmass;
   tempmass = dummymass + 1;    //mass array with -1 element for initialization
   tempmass[-1] = 0;
   int idx = 0;
   double maxabundance = 0;
   MolDistribution currmol;     //an object in which to construct the molecule
   currmol.atomlist.resize(totalnat);
   currmol.z = comp.z;          //set the charge to be the same for all molecules
   atselect[0] = elements[0].distribution.begin();      // initialize to first distribution of first element
   bool backtrack = false;
   do
   {
      if (!backtrack)
      {
         if (idx < totalnat)
         {                      //it's an incomplete molecule...
            tempabun[idx] = tempabun[idx - 1] * atselect[idx]->abundance;       //calculate incomplete molecule's mass and abundance
            tempmass[idx] = tempmass[idx - 1] + atselect[idx]->mass;
            if (!(backtrack = tempabun[idx] < maxabundance * thr))
            {                   //set backtrack flag if the incomplete molecule is not abundant enough
               idx++;           //if not backtracking then move forward
               if (idx < totalnat)
                  atselect[idx] = elements[idx].distribution.begin();
            }
         }
         else
         {                      //it's a complete molecule - must be abundant enough otherwise would have been chucked out on previous cycle
            for (int i = 0; i < totalnat; i++)
            {                   //create the composition of the molecule from combination of the individual atoms ensembles
               currmol.atomlist[i] = &*atselect[i];
            }
            currmol.abundance = tempabun[idx - 1];
            currmol.mass = tempmass[idx - 1] - comp.z * ElectronMass;   //correct mass for loss or gain of electron
            molecule.push_back(currmol);        //add to list
            if (currmol.abundance > maxabundance)
               maxabundance = currmol.abundance;
            idx--;              //move back to a valid value of idx
            backtrack = true;
         }
      }
      else
      {
         atselect[idx]++;       //try the next atom distribution
         if (atselect[idx] != elements[idx].distribution.end())
         {
            backtrack = false;  //if the distribution is valid, go forward...
         }
         else
         {
            idx--;              //if not, then go to the previous element, and backtrack again
         }
      }
   }
   while (idx >= 0);
   delete[]dummyabun;
   delete[]dummymass;
   delete[]atselect;
   return 0;
}

void MolEnsemble::MakeSpectrum(BaseSpectrum * obPtr, double degen)      //generate a mass spectrum in *obPtr, merging peaks within degen units
{
   molecule.sort();
   double lowmtoz, totalintensity, avemtoz;
   list < MolDistribution >::iterator i = molecule.begin();     //merge peaks within degen, with the weight average mass, and total intensity

   if (spectype == masstocharge)
   {
      while (i != molecule.end() && !i->z)
      {
         i++;
      }                         //if calculating mass-to-charge skip over molecules with no charge
   }
   if (i == molecule.end())
   {
      return;
   }                            //breakout if the end of the list has been hit

   int ez = (spectype == mass) ? 1 : i->z;      //if the spectrum type is mass then force the charge used in the calculations to +1, regardless of the actual charge
   lowmtoz = i->mass / ez;      // do I need to be careful? will this mess up if the ensemble contains molecules with different charge
   totalintensity = avemtoz = 0;

   while (i != molecule.end())
   {
      if (spectype == mass || i->z)
      {                         //if spectrum is mass-to-charge then must skip over any uncharged molecules
         ez = (spectype == mass) ? 1 : i->z;
         if (fabs(i->mass / ez - lowmtoz) <= degen)
         {                      //check whether adjacent peaks differ by less than degen, and merge them if they are
            totalintensity += i->abundance;
            avemtoz += i->abundance * i->mass / ez;     //weight according to abundance
         }
         else
         {
            peak ptemp(avemtoz / totalintensity, totalintensity);
            obPtr->push_back(ptemp);
            totalintensity = i->abundance;
            avemtoz = i->abundance * i->mass / ez;
         }
         lowmtoz = i->mass / ez;
      }
      i++;
   }
   if (totalintensity)
   {
      peak ptemp(avemtoz / totalintensity, totalintensity);
      obPtr->push_back(ptemp);  //add the final peak, provided that it exists
   }
}

double AtomDistribution::CalculateMass(AtomIsoAbun * ob)
{
   mass = 0;
   for (unsigned int i = 0; i < number.size(); i++)
   {
      mass += ob->isotopes[i].mass * number[i];
   }
   return mass;
}

ostream & operator<<(ostream & stream, AtomDistribution & ob)
{
   for (vector < int >::iterator i = ob.number.begin(); i < ob.number.end();
        i++)
   {
      stream << ' ' << *i;
   }
   return stream;
}

AtomEnsemble::AtomEnsemble()
{
}

AtomEnsemble::AtomEnsemble(int _z, int _niso)
{
   atomdef.Setniso(_niso);
   atomdef.z = _z;
}

AtomEnsemble::AtomEnsemble(AtomIsoAbun & _atomdef):atomdef(_atomdef)
{
}

ostream & operator<<(ostream & stream, AtomEnsemble & ob)
{
   stream << ob.atomdef;
   for (list < AtomDistribution >::iterator i = ob.distribution.begin();
        i != ob.distribution.end(); i++)
   {
      stream << *i;
   }
   return stream;
}

double MolDistribution::CalculateMass() //calculate molecule mass from sum of masses of pre-calculated atom distributions
{
   mass = 0;
   for (vector < AtomDistribution * >::iterator i = atomlist.begin();
        i != atomlist.end(); i++)
   {
      mass += (*i)->mass;
   }
   mass -= z * ElectronMass;    //take into account any added or removed electrons
   return mass;
}

bool MolDistribution::operator<(const MolDistribution & ob) const       //compare by mass (NOT mass to charge)
{                               //this may cause things to break if molecules have different charges
   return mass < ob.mass;
}

ostream & operator<<(ostream & stream, MolDistribution & ob)
{
   for (vector < AtomDistribution * >::iterator i = ob.atomlist.begin();
        i < ob.atomlist.end(); i++)
   {
      stream << **i;
   }
   stream << ' ' << ob.z;
   int p = stream.precision(8); //boost to 8 digits of precision for the mass
   stream << ' ' << ob.mass;
   stream.precision(p);
   stream << ' ' << ob.abundance << '\n';
   return stream;
}

MolEnsemble::MolEnsemble():elements(NULL), totalnat(0), spectype(mass)
{
}

MolEnsemble::MolEnsemble(MolComposition & comp, double thr)
{
   GenerateEnsemble(comp, thr);
}

MolEnsemble::MolEnsemble(const MolEnsemble & ob)
{
   totalnat = ob.totalnat;
   spectype = ob.spectype;
   elements = new AtomEnsemble[totalnat];
   for (int i = 0; i < totalnat; i++)
      elements[i] = ob.elements[i];
   molecule = ob.molecule;
}

MolEnsemble & MolEnsemble::operator=(MolEnsemble & ob)
{
   if (this != &ob)
   {
      delete[]elements;
      totalnat = ob.totalnat;
      spectype = ob.spectype;
      elements = new AtomEnsemble[totalnat];
      for (int i = 0; i < totalnat; i++)
         elements[i] = ob.elements[i];
      molecule = ob.molecule;
   }
   return *this;
}

MolEnsemble::~MolEnsemble()
{
   delete[]elements;
}

void MolEnsemble::CalculateMasses()     //calculate masses of all molecules in the ensemble
{
   for (list < MolDistribution >::iterator i = molecule.begin();
        i != molecule.end(); i++)
   {
      for (unsigned int j = 0; j < i->atomlist.size(); j++)
      {
         i->atomlist[j]->CalculateMass(&elements[j].atomdef);
      }
      i->CalculateMass();
   }
}

ostream & operator<<(ostream & stream, MolEnsemble & ob)
{
   for (int i = 0; i < ob.totalnat; i++)
   {
      stream << ob.elements[i].atomdef;
   }
   for (list < MolDistribution >::iterator i = ob.molecule.begin();
        i != ob.molecule.end(); i++)
   {
      stream << *i;
   }
   return stream;
}

IsoCalc::~IsoCalc()
{
}

IsoCalc::IsoCalc():atomsloaded(false), molloaded(false), normalized(false), calculated(false), autocalc(false), thr(0.001), degen(0.1)
{
}

IsoCalc::IsoCalc(char *filename)        //constructor that reads in an atom table from a file
{
}

int IsoCalc::ReadAtomTable(const char *filename)        //read the atom table from a file, returns 0 on success
{
   atomsloaded = false;         // reset this flag, in case the file open fails
   ifstream in(filename);
   if (!in)
      return 1;
   string buff;
   char sym[4];
   int _atno, _niso = 0;
   do
   {                            //do a first pass of the file to count how many isotopes for each element and
                                //create the necessary entries in the table in the same order that they are in the file
      in >> buff;
      if (isalpha(buff[0]))
      {
         if (_niso)
         {                      // need to check that we are not on the first entry in the file
            AtomIsoAbun temp(_atno, (_niso - 1) / 2);   //setup the new isotope abundance object and add it to the atomtable
            strcpy(temp.symbol, sym);
            if (find(atomtable.begin(), atomtable.end(), temp) !=
                atomtable.end())
               return 1;        //return an error if there are any duplicate definitions in the file
            atomtable.push_back(temp);
         }
         if (buff.size() < 4)
            strcpy(sym, buff.c_str());  //copy the symbol to another buffer
         else
            return 1;           // return an error if the symbol is too long (more than 3 characters)
         in >> _atno;
         if (in.rdstate() || _atno < 0)
            return 1;           //check for dodgy values
         _niso = 0;
      }
      _niso++;
   }
   while (!(in.rdstate() & ios::eofbit));
   AtomIsoAbun temp(_atno, (_niso - 1) / 2);    //deal with the case at the end of the file - where reads run into EOF instead of the next symbol
   strcpy(temp.symbol, sym);
   atomtable.push_back(temp);
   in.clear();
   in.seekg(0, ios_base::beg);  // do a second pass on the file to read in the details, return error 1 if anything goes wrong
   deque < AtomIsoAbun >::iterator j = atomtable.begin();
   do
   {
      in >> buff >> buff;       // 2 dummy reads to skip over the atom symbol and atomic number (already read these)
      if (in.rdstate())
         return 1;
      for (int i = 0; i < j->Getniso(); i++)
      {                         //read accurate mass and abundance values, then sort according to abundance
         in >> j->isotopes[i].mass;
         if (in.rdstate() || j->isotopes[i].mass < 0)
            return 1;           //dodgy value check for negative masses
         in >> j->isotopes[i].abundance;
         if (in.rdstate() || j->isotopes[i].abundance > 1
             || j->isotopes[i].abundance < 0)
            return 1;           //check for dodgy values of the isotope abundance
      }
      j->Sort_abundance();
      j++;
   }
   while (j != atomtable.end());
   atomsloaded = true;          //atom table loaded ok, so set the flag
   return 0;
}

int IsoCalc::SetComposition(char *formula)      //convert a string containing a molecular formula to a MolComposition, returns 0 on success
{
   if (!atomsloaded)
      return 3;
   molloaded = false;           //reset in case composition assignment fails
   calculated = false;
   normalized = false;
   if (!strlen(formula))
      return 2;
   string trimformula;  //create a new string containing the formula with white space stripped out
   char  *ptr = formula;
   while (*ptr != '\0')
   {
      if (!isspace(*ptr))
         trimformula += *ptr;
      ptr++;
   }
   set < string > atomtypes;    // a set containing the different atom types - do it this way to cope with formulas like C6H5OH where atoms are specified multiple times
   string currelement;
   size_t loc = 0;
   while (loc != trimformula.size())
   {
      if (isupper(trimformula[loc]))
      {                         // if current character is a capital letter - then it is the start of a new element
         if (currelement.size())
         {
            atomtypes.insert(currelement);      //add the previous element (if there was one) to the set
         }
         currelement = trimformula[loc];        //add the new character to the current element
      }
      else if (islower(trimformula[loc]) || trimformula[loc] == '*')
      {                         //append any lower case characters or the * character to indicate a label
         currelement += trimformula[loc];
      }
      loc++;
   }
   atomtypes.insert(currelement);       //add the final element
   molecule.Settotalnat(atomtypes.size());      //set the number of atomtypes
   set < string >::iterator i = atomtypes.begin();
   int c = 0;
   while (i != atomtypes.end())
   {                            //look up up atomic isotope distributions for each element
      deque < AtomIsoAbun >::iterator j = atomtable.begin();
      while (j != atomtable.end() && strcmp(j->symbol, i->c_str()))
      {
         j++;
      }
      if (j != atomtable.end())
      {
         molecule.nat[c] = 0;
         molecule.atom[c] = *j;
      }
      else
         return 2;
      c++;
      i++;
   }
   currelement = "";
   string number;       // the number of atoms
   loc = 0;
   while (loc != trimformula.size())
   {                            //do a second parse on the formula to tally up the number of each type of atom
      if (isupper(trimformula[loc]))
      {                         // if current character is a capital letter - then it is the start of a new element
         if (currelement.size())
         {                      //if it is not the very first character, add on the appropriate number of atoms
            c = 0;
            while (strcmp(molecule.atom[c].symbol, currelement.c_str()))
            {
               c++;
            }                   //look up where to add to
            molecule.nat[c] += number.size()? atoi(number.c_str()) : 1; // because CH4 means C1H4
         }
         currelement = trimformula[loc];        //add the new character to the current element
         number = "";           //zero the number
      }
      else if (islower(trimformula[loc]) || trimformula[loc] == '*')
      {                         //append any lower case characters or the * character to indicate a label
         currelement += trimformula[loc];
      }
      else if (isdigit(trimformula[loc]))
      {
         number += trimformula[loc];
      }
      else
      {
         return 2;
      }
      loc++;
   }
   c = 0;
   while (strcmp(molecule.atom[c].symbol, currelement.c_str()))
   {
      c++;
   }                            // deal with the final element - look up where to add to
   molecule.nat[c] += number.size()? atoi(number.c_str()) : 1;  // because CH4 means C1H4
   molloaded = true;
   if (autocalc)
      return Calculate();       //if autocalculate, then need to recalculate the distribution now
   return 0;
}

int IsoCalc::Calculate()        //calculate the isotope distribution AND spectrum, return 0 on success
{
   int errnr;
   if (!atomsloaded)
      return 3;                 //abort if there is no data or molecule
   if (!molloaded)
      return 4;
   calculated = false;          //in case anything goes wrong
   normalized = false;
   errnr = ensemble.GenerateEnsemble(molecule, thr);
   if (errnr)
      return errnr;
   ensemble.MakeSpectrum(&masspeaks, degen);
   calculated = true;
   return 0;
}

int IsoCalc::GetNPeaks(int &_npeaks)    //return the number of peaks in the peaklist
{
   if (!calculated)
   {
      _npeaks = 0;
   }                            // could return an error 5 here, but simply give 0 as the number of peaks
   _npeaks = masspeaks.size();
   return 0;
}

int IsoCalc::Mass(int n, double &m)     //mass (m) of peak no. n (zero based indexing), return 0 on success
{
   if (!calculated)
   {
      m = 0;
      return 5;
   }
   if (n < 0 || n >= masspeaks.size())
   {
      m = 0;
      return 6;
   }
   m = masspeaks[n].mass;
   return 0;
}

int IsoCalc::Abundance(int n, double &abun)     //abundance of peak n (zero based indexing, return 0 on success
{
   if (!calculated)
   {
      abun = 0;
      return 5;
   }
   if (n < 0 || n >= masspeaks.size())
   {
      abun = 0;
      return 6;
   }
   abun = masspeaks[n].abundance;
   return 0;
}

int IsoCalc::Peak(int n, double &mass, double &abun)    //mass and abundance of peak number n (zero based indexing)
{                               //return 0 on success
   if (!calculated)
   {
      mass = 0;
      abun = 0;
      return 5;
   }                            //give both mass and abundance as zero, and return an error
   if (n < 0 || n >= masspeaks.size())
   {
      mass = 0;
      abun = 0;
      return 6;
   }
   mass = masspeaks[n].mass;
   abun = masspeaks[n].abundance;
   return 0;
}

int IsoCalc::Normalize()        // normalize the intensity so that most intense peak = 100%
{
   if (!calculated)
      return 5;
   double max = 0;
   for (int i = 0; i < masspeaks.size(); i++)
   {
      if (masspeaks[i].abundance > max)
         max = masspeaks[i].abundance;
   }
   for (int i = 0; i < masspeaks.size(); i++)
   {
      masspeaks[i].abundance = 100 * masspeaks[i].abundance / max;
   }
   normalized = true;
   return 0;
}

int IsoCalc::SetFwhm(const double _fwhm)        //setter for the peak full width at half max, return 0 on success
{
   if (_fwhm <= 0)
      return 7;
   masspeaks.fwhm = _fwhm;
   return 0;
}

int IsoCalc::GetFwhm(double &_fwhm)     //getter for the peak full width at half max, return 0 on success
{
   _fwhm = masspeaks.fwhm;
   return 0;
}

int IsoCalc::SetThr(const double _thr)  // setter for isotope calculation threshold
{
   if (_thr < 0 || thr >= 1)
      return 7;
   thr = _thr;
   if (autocalc)
      Calculate();              //if autocalculate flag is set then will need to redo the isotope calculate
   return 0;
}

int IsoCalc::GetThr(double &_thr)       //getter for isotope calculation threshold
{
   _thr = thr;
   return 0;
}

int IsoCalc::Intensity(double m, double &i)     //supplies intensity i at mass m calculated using gaussians
{
   if (!calculated)
   {
      i = 0;
      return 5;
   }                            // give zero intensity, and return an error
   i = masspeaks.intensity(m);
   return 0;
}

int IsoCalc::SetAutoCalc(const bool _autocalc)  //setter for the autocalc flag
{
   autocalc = !autocalc;
   if (autocalc)
   {                            // update the calculation if required
      if (!calculated)
         Calculate();
   }
   return 0;
}

int IsoCalc::GetAutoCalc(bool & _autocalc)
{
   _autocalc = autocalc;
   return 0;
}

int IsoCalc::SetCharge(const int _z)    //update charge
{
   molecule.z = _z;
   if (autocalc)
      Calculate();
   return 0;
}

int IsoCalc::GetCharge(int &_z) //return charge
{
   _z = molecule.z;
   return 0;
}

int IsoCalc::SetDegen(const double _degen)
{
   if (degen <= 0)
      return 7;
   degen = _degen;
   if (autocalc)
      Calculate();              //if autocalculate flag is set then will need to redo the isotope calculate
   return 0;
}

int IsoCalc::GetDegen(double &_degen)
{
   _degen = degen;
   return 0;
}

int IsoCalc::SetMassToCharge(const bool _mtoz)  //sets the spectrum type
{
   if (_mtoz)
      ensemble.spectype = MolEnsemble::masstocharge;
   else
      ensemble.spectype = MolEnsemble::mass;
   if (autocalc)
   {                            // update the calculation if required
      if (!calculated)
         Calculate();
   }
   return 0;
}
