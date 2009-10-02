#include "stdafx.h"
#include "mercury.h"
#include "CMercury8.h"
#include "ManagedCMercury8.h"
using namespace System::Collections::Generic;
namespace mercury {
	Dictionary<double, double>^ ManagedCMercury8::Calculate(System::String^ molecularFormula, int charge)
	{
		Dictionary<double, double>^ result 
			= gcnew Dictionary<double, double>();
		int i;
		int NumElements=0;			/* Number of elements in molecular formula */
		  
		if (molecularFormula->Length == 0) {
			return result;
		};
		  
		//Parse the formula, check for validity
		if (pmercury8->ParseMF((char *)ToStdString(molecularFormula).c_str()) == -1)     {
			return result;
		};

		//Run the user requested Mercury
		if(pmercury8->bAccMass) {
			pmercury8->AccurateMass(charge);
		}
		else {
			pmercury8->Mercury(charge);
		}
		  
		//If the user requested relative abundance, convert data
		if(pmercury8->bRelAbun) {
			pmercury8->RelativeAbundance(pmercury8->FixedData);
		}

		for (int i = 0; i < pmercury8->FixedData.size(); i++) {
			result->Add(pmercury8->FixedData[i].mass, pmercury8->FixedData[i].data);
		}
		    
		//Clear all non-user input so object can be reused:
		pmercury8->Reset();
		return result;
  	}
	void ManagedCMercury8::SetIsotopeAbundances(Dictionary<System::String^, Dictionary<double, double>^>^ abundances) {
		pmercury8->Element.clear();
		for (Dictionary<System::String^, Dictionary<double, double>^>::Enumerator it = abundances->GetEnumerator();
			it.MoveNext();) {
				Atomic5 atomic5;
				atomic5.Symbol = ToStdString(it.Current.Key);
				atomic5.NumIsotopes = it.Current.Value->Count;
				atomic5.IntMass.reserve(atomic5.NumIsotopes);
				atomic5.IsoMass.reserve(atomic5.NumIsotopes);
				atomic5.IsoProb.reserve(atomic5.NumIsotopes);
				for (Dictionary<double, double>::Enumerator isotopeIt = it.Current.Value->GetEnumerator();
					isotopeIt.MoveNext();) {
						atomic5.IntMass.push_back((int) isotopeIt.Current.Key);
						atomic5.IsoMass.push_back(isotopeIt.Current.Key);
						atomic5.IsoProb.push_back(isotopeIt.Current.Value);
				}
				pmercury8->Element[atomic5.Symbol] = atomic5;
		}
	}
}

