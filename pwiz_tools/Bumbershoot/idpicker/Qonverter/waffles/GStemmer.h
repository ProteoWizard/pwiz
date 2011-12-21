#ifndef __GSTEMMER_H__
#define __GSTEMMER_H__

#define GSTEMMER_MAX_WORD_SIZE 64

namespace GClasses {

struct stemmer;

/// This class just wraps the Porter Stemmer.
/// It finds the stems of words.  Examples:
/// "cats"->"cat"
/// "dogs"->"dog"
/// "fries"->"fri"
/// "fishes"->"fish"
/// "pies"->"pi"
/// "lovingly"->"lovingli"
/// "candy"->"candi"
/// "babies"->"babi"
/// "bus"->"bu"
/// "busses"->"buss"
/// "women"->"women"
/// "hasty"->"hasti"
/// "hastily"->"hastili"
/// "fly"->"fly"
/// "kisses"->"kiss"
/// "goes"->"goe"
/// "brought"->"brought"
/// As you can see the stems aren't always real words, but
/// that's okay as long as it produces the same stem for words
/// that have the same etymological roots. Even then it still
/// isn't perfect (notice it got "bus" wrong), but it should
/// still improve analysis somewhat in many cases.
class GStemmer
{
protected:
	struct stemmer* m_pPorterStemmer;
	char m_szBuf[GSTEMMER_MAX_WORD_SIZE];

public:
	GStemmer();
	~GStemmer();

	/// Pass in a word (you don't need to lowercase or null-terminate it) and this
	/// will return its stem. The buffer it returns is only valid until the next time
	/// you call GetStem.
	const char* getStem(const char* szWord, size_t nLen);
};

} // namespace GClasses

#endif // __GSTEMMER_H__
