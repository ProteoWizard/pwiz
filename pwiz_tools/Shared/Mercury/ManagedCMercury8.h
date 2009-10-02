#pragma once
using namespace System::Collections::Generic;
namespace mercury {
	inline std::string ToStdString(System::String^ source)
	{
		int len = (( source->Length+1) * 2);
		char *ch = new char[ len ];
		bool result ;
		{
			cli::array<wchar_t>^ rgwch = source->ToCharArray();
			for (int i = 0; i < source->Length; i++) {
				ch[i] = (char) rgwch[i];
			}
			ch[source->Length] = '\0';
		}
		std::string target = ch;
		delete ch;
		return target;
	}

	public ref class ManagedCMercury8 {
	public:
		ManagedCMercury8() {
			pmercury8 = new CMercury8();
		}
		~ManagedCMercury8() {
			delete pmercury8;
		}
		Dictionary<double, double>^ Calculate(System::String^ molecularFormula, int charge);
		void SetIsotopeAbundances(Dictionary<System::String^, Dictionary<double, double>^>^ abundances);
	private:
		CMercury8 *pmercury8;
	};
}