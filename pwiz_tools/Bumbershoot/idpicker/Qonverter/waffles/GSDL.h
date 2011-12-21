#ifndef __GSDL_H__
#define __GSDL_H__

namespace GClasses {

/// A collection of routines that are useful when interfacing with SDL
class GSDL
{
public:
	/// Figures out which character to display if the shift key is held down
	static char shiftKey(char c);

	/// This maps number-pad keys to their corresponding normal keys,
	/// and returns '\0' for all other special keys
	static char filterKey(int key);
};

} // namespace GClasses

#endif // __GSDL_H__
