#ifndef __GTEXT_H__
#define __GTEXT_H__

#include <sys/types.h>
#include <cstddef>
#include <vector>

namespace GClasses {

class GConstStringHashTable;
class GConstStringToIndexHashTable;
class GStemmer;
class GHeap;


/// This iterates over the words in a block of text
class GWordIterator
{
protected:
	const char* m_pText;
	size_t m_len;

public:
	GWordIterator(const char* text, size_t len);
	~GWordIterator();

	/// Obtains the next word in the block of text. Returns false if there are no words left.
	bool next(const char** ppWord, size_t* pLen);
};


/// Stores statistics about each word in a GVocabulary
class GWordStats
{
public:
	size_t m_curDocFreq; // the number of times the word has been found in the current document
	size_t m_maxWordFreq; // the max number of times the word is known to occur in any one document
	size_t m_docsContainingWord; // the number of documents that contain this word at all
	size_t m_lastDocContainingWord; // the most recent document id in which this word has been found
	const char* m_szWord;

	GWordStats()
	: m_curDocFreq(1), m_maxWordFreq(1), m_docsContainingWord(1), m_lastDocContainingWord(0), m_szWord(NULL)
	{
	}
};


/// This is a helper class which is useful for text-mining. It collects words,
/// stems them, filters them through a list of stop-words, and assigns a discrete
/// number to each word.
class GVocabulary
{
protected:
	GStemmer* m_pStemmer;
	size_t m_minWordSize;
	size_t m_vocabSize;
	GConstStringHashTable* m_pStopWords;
	GConstStringToIndexHashTable* m_pVocabulary;
	GHeap* m_pHeap;
	char wordBuf[64];
	size_t m_docNumber;
	std::vector<GWordStats>* m_pWordStats;

public:
	GVocabulary(bool stemWords);
	~GVocabulary();

	/// Sets the minimum word size. Smaller words will be ignored. The
	/// default is 4.
	void setMinWordSize(size_t n) { m_minWordSize = n; }

	/// Adds a stop word (a common word that should always be ignored)
	void addStopWord(const char* szWord);

	/// Adds a typical set of stop words
	void addTypicalStopWords();

	/// Returns the number of unique words in this vocabulary
	size_t wordCount() { return m_vocabSize; }

	/// Adds a word to the vocabulary. (If the word is too short
	/// or is in the stop-word list, it will not be added.)
	void addWord(const char* szWord, size_t nLen);

	/// Adds all the words in the text block to the vocabulary
	void addWordsFromTextBlock(const char* text, size_t len);

	/// Returns the index of the specified word. Returns -1 if
	/// the word is not in the vocabulary (or is too short or
	/// is a stop word).
	size_t wordIndex(const char* szWord, size_t len);

	/// Returns a pointer to the heap this uses to store strings
	GHeap* heap() { return m_pHeap; }

	/// If you want this to track statistics about the number of docs
	/// that contain each word, and the max number of times each word occurs
	/// in any doc, then you should call this method each time you start
	/// adding words from a new document (including the first one). If
	/// you don't want to track such stats, you need never call this method.
	/// If you call this method, but you didn't call it before the first
	/// word was added, it will throw an exception.
	void newDoc();

	/// Returns the stats about a word. Throws if you weren't tracking
	/// stats (ie if you didn't call newDoc before each new document).
	GWordStats& stats(size_t word);

	/// Returns the number of documents from which words have been added so far.
	size_t docCount() { return m_docNumber + 1; }

	/// Computes the weight that should be added to a document vector
	/// for each occurrence of a word in the vector-space document model.
	/// It is log(number_of_docs/docs_containing_word)/max_word_frequency.
	double weight(size_t word);
};

} // namespace GClasses

#endif // __GTEXT_H__
