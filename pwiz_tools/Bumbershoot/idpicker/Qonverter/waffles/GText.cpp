#include "GText.h"
#include "GHashTable.h"
#include "GHeap.h"
#include "GStemmer.h"
#include <math.h>

namespace GClasses {

using std::vector;

GWordIterator::GWordIterator(const char* text, size_t len)
: m_pText(text), m_len(len)
{
}

GWordIterator::~GWordIterator()
{
}

bool GWordIterator_isWordChar(char c)
{
	if(c >= 'a' && c <= 'z')
		return true;
	if(c >= 'A' && c <= 'Z')
		return true;
	return false;
}

bool GWordIterator::next(const char** ppWord, size_t* pLen)
{
	while(m_len > 0 && !GWordIterator_isWordChar(*m_pText))
	{
		m_pText++;
		m_len--;
	}
	*ppWord = m_pText;
	while(m_len > 0 && GWordIterator_isWordChar(*m_pText))
	{
		m_pText++;
		m_len--;
	}
	*pLen = m_pText - *ppWord;
	return *pLen > 0;
}




const char* g_szStopWords[] = 
{
	"a","about","all","also","although","an","and","any","are","as","at",
	"be","but","by",
	"can",
	"did",
	"each","every",
	"for","from",
	"had","have","he","her","him","his","how",
	"i","if","in","is","it","its",
	"my",
	"nbsp","next","no","not",
	"of","on","one","or","our","out",
	"quite",
	"really",
	"so","some",
	"that","the","them","then","there","this","to","too",
	"use",
	"very",
	"was","we","what","when","where","who","will","with",
	"you",
};

GVocabulary::GVocabulary(bool stemWords)
: m_minWordSize(4), m_vocabSize(0)
{
	if(stemWords)
		m_pStemmer = new GStemmer();
	else
		m_pStemmer = NULL;
	m_pStopWords = new GConstStringHashTable(113, false);
	m_pVocabulary = new GConstStringToIndexHashTable(3571, false);
	m_pHeap = new GHeap(1024);
	m_docNumber = 0;
	m_pWordStats = NULL;
}

GVocabulary::~GVocabulary()
{
	delete(m_pStemmer);
	delete(m_pStopWords);
	delete(m_pVocabulary);
	delete(m_pHeap);
	delete(m_pWordStats);
}

void GVocabulary::addStopWord(const char* szWord)
{
	const char* pWord = szWord;
	if(m_pStemmer)
		pWord = m_pStemmer->getStem(szWord, strlen(szWord));
	m_pStopWords->add(m_pHeap->add(pWord), NULL);
}

void GVocabulary::addTypicalStopWords()
{
	for(size_t i = 0; i < (sizeof(g_szStopWords) / sizeof(const char*)); i++)
		m_pStopWords->add(g_szStopWords[i], NULL);
}

void GVocabulary::addWord(const char* szWord, size_t nLen)
{
	if(nLen < m_minWordSize)
		return;

	// Find the stem
	const char* szStem;
	if(m_pStemmer)
		szStem = m_pStemmer->getStem(szWord, nLen);
	else
	{
		nLen = std::min((size_t)63, nLen);
		memcpy(wordBuf, szWord, nLen); // todo: make lowercase
		wordBuf[nLen] = '\0';
		szStem = wordBuf;
	}

	// Don't add stop words
	void* pValue;
	if(m_pStopWords->get(szStem, &pValue))
		return; // it's a stop word

	// Check for existing words
	size_t nIndex;
	if(m_pVocabulary->get(szStem, &nIndex))
	{
		// Track word stats
		if(m_pWordStats)
		{
			GAssert(nIndex < m_pWordStats->size());
			GWordStats& wordStats = (*m_pWordStats)[nIndex];
			if(wordStats.m_lastDocContainingWord != m_docNumber)
			{
				wordStats.m_lastDocContainingWord = m_docNumber;
				wordStats.m_docsContainingWord++;
				wordStats.m_curDocFreq = 0;
			}
			wordStats.m_curDocFreq++;
			wordStats.m_maxWordFreq = std::max(wordStats.m_curDocFreq, wordStats.m_maxWordFreq);
		}
		return;
	}

	// Add the word to the vocabulary
	nIndex = m_vocabSize;
	char* pStoredWord = m_pHeap->add(szStem);
	m_pVocabulary->add(pStoredWord, m_vocabSize++);

	// Track word stats
	if(m_pWordStats)
	{
		m_pWordStats->resize(m_vocabSize);
		GWordStats& wordStats = (*m_pWordStats)[nIndex];
		wordStats.m_lastDocContainingWord = m_docNumber;
		wordStats.m_szWord = pStoredWord;
	}
}

GWordStats& GVocabulary::stats(size_t word)
{
	if(!m_pWordStats)
		ThrowError("You didn't call newDoc before adding vocabulary words, so stats weren't tracked");
	return (*m_pWordStats)[word];
}

void GVocabulary::addWordsFromTextBlock(const char* text, size_t len)
{
	GWordIterator it(text, len);
	const char* pWord;
	size_t wordLen;
	while(true)
	{
		if(!it.next(&pWord, &wordLen))
			break;
		addWord(pWord, wordLen);
	}
}

size_t GVocabulary::wordIndex(const char* szWord, size_t len)
{
	// Find the stem
	const char* szStem;
	if(m_pStemmer)
		szStem = m_pStemmer->getStem(szWord, len);
	else
	{
		len = std::min((size_t)63, len);
		memcpy(wordBuf, szWord, len); // todo: make lowercase
		wordBuf[len] = '\0';
		szStem = wordBuf;
	}

	// Look up the stem
	size_t val;
	if(!m_pVocabulary->get(szStem, &val))
		return INVALID_INDEX;
	return val;
}

void GVocabulary::newDoc()
{
	if(!m_pWordStats)
	{
		if(m_vocabSize > 0)
			ThrowError("If you call newDoc, then you must call it before the first word is added");
		m_pWordStats = new vector<GWordStats>();
		m_docNumber = 0;
		return;
	}
	m_docNumber++;
}

double GVocabulary::weight(size_t word)
{
	GWordStats& ws = stats(word);
	return log((double)docCount() / ws.m_docsContainingWord) / ws.m_maxWordFreq;
}

} // namespace GClasses

