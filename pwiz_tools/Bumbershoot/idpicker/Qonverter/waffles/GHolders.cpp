#include "GHolders.h"
#include "GError.h"

namespace GClasses {

#ifdef _DEBUG
GTempBufSentinel::GTempBufSentinel(void* pBuf)
: m_pBuf(pBuf)
{
	*(char*)pBuf = 'S';
}

GTempBufSentinel::~GTempBufSentinel()
{
	GAssert(*(char*)m_pBuf == 'S'); // buffer overrun!
}
#endif // _DEBUG



void FileHolder::reset(FILE* pFile)
{
	if(m_pFile && pFile != m_pFile)
	{
		if(fclose(m_pFile) != 0)
			GAssert(false);
	}
	m_pFile = pFile;
}

} // namespace GClasses
