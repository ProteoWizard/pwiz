/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <stdio.h>
#include <stdlib.h>
#include <stddef.h>
#include "GDom.h"
//#include "GXML.h"
#include "GFile.h"
#include "GHolders.h"
#include <vector>
#include <deque>
#include <sstream>
#include <fstream>
#include "GTokenizer.h"


namespace GClasses {

using std::vector;
using std::deque;

class GDomObjField
{
public:
	const char* m_pName;
	GDomNode* m_pValue;
	GDomObjField* m_pPrev;
};

/// An element in a GDom list
class GDomListItem
{
public:
	/// Pointer to the value contained in this list item
	GDomNode* m_pValue;

	/// Pointer to the previous node in the list
	GDomListItem* m_pPrev;
};


GDomListIterator::GDomListIterator(GDomNode* pNode)
{
	if(pNode->m_type != GDomNode::type_list)
		ThrowError("Not a list type");
	m_pList = pNode;
	m_remaining = m_pList->reverseItemOrder();
	m_pCurrent = m_pList->m_value.m_pLastItem;
}

GDomListIterator::~GDomListIterator()
{
	m_pList->reverseItemOrder();
}

GDomNode* GDomListIterator::current()
{
	return m_pCurrent ? m_pCurrent->m_pValue : NULL;
}

void GDomListIterator::advance()
{
	m_pCurrent = m_pCurrent->m_pPrev;
	m_remaining--;
}

size_t GDomListIterator::remaining()
{
	return m_remaining;
}


GDomNode* GDomNode::fieldIfExists(const char* szName)
{
	if(m_type != type_obj)
		ThrowError("not an obj");
	GDomObjField* pField;
	for(pField = m_value.m_pLastField; pField; pField = pField->m_pPrev)
	{
		if(strcmp(szName, pField->m_pName) == 0)
			return pField->m_pValue;
	}
	return NULL;
}

size_t GDomNode::reverseFieldOrder()
{
	GAssert(m_type == type_obj);
	size_t count = 0;
	GDomObjField* pNewHead = NULL;
	while(m_value.m_pLastField)
	{
		GDomObjField* pTemp = m_value.m_pLastField;
		m_value.m_pLastField = pTemp->m_pPrev;
		pTemp->m_pPrev = pNewHead;
		pNewHead = pTemp;
		count++;
	}
	m_value.m_pLastField = pNewHead;
	return count;
}

size_t GDomNode::reverseItemOrder()
{
	GAssert(m_type == type_list);
	size_t count = 0;
	GDomListItem* pNewHead = NULL;
	while(m_value.m_pLastItem)
	{
		GDomListItem* pTemp = m_value.m_pLastItem;
		m_value.m_pLastItem = pTemp->m_pPrev;
		pTemp->m_pPrev = pNewHead;
		pNewHead = pTemp;
		count++;
	}
	m_value.m_pLastItem = pNewHead;
	return count;
}

GDomNode* GDomNode::addField(GDom* pDoc, const char* szName, GDomNode* pNode)
{
	if(m_type != type_obj)
		ThrowError("not an obj");
	GDomObjField* pField = pDoc->newField();
	pField->m_pPrev = m_value.m_pLastField;
	m_value.m_pLastField = pField;
	GHeap* pHeap = pDoc->heap();
	pField->m_pName = pHeap->add(szName);
	pField->m_pValue = pNode;
	return pNode;
}

GDomNode* GDomNode::addItem(GDom* pDoc, GDomNode* pNode)
{
	if(m_type != type_list)
		ThrowError("not a list");
	GDomListItem* pItem = pDoc->newItem();
	pItem->m_pPrev = m_value.m_pLastItem;
	m_value.m_pLastItem = pItem;
	pItem->m_pValue = pNode;
	return pNode;
}

void writeJSONString(std::ostream& stream, const char* szString)
{
	stream << '"';
	while(*szString != '\0')
	{
		if(*szString < ' ')
		{
			switch(*szString)
			{
				case '\b': stream << "\\b"; break;
				case '\f': stream << "\\f"; break;
				case '\n': stream << "\\n"; break;
				case '\r': stream << "\\r"; break;
				case '\t': stream << "\\t"; break;
				default:
					stream << (*szString);
			}
		}
		else if(*szString == '\\')
			stream << "\\\\";
		else if(*szString == '"')
			stream << "\\\"";
		else
			stream << (*szString);
		szString++;
	}
	stream << '"';
}

size_t writeJSONStringCpp(std::ostream& stream, const char* szString)
{
	stream << "\\\"";
	size_t chars = 2;
	while(*szString != '\0')
	{
		if(*szString < ' ')
		{
			switch(*szString)
			{
				case '\b': stream << "\\\\b"; break;
				case '\f': stream << "\\\\f"; break;
				case '\n': stream << "\\\\n"; break;
				case '\r': stream << "\\\\r"; break;
				case '\t': stream << "\\\\t"; break;
				default:
					stream << (*szString);
			}
			chars += 3;
		}
		else if(*szString == '\\')
		{
			stream << "\\\\\\\\";
			chars += 4;
		}
		else if(*szString == '"')
		{
			stream << "\\\\\\\"";
			chars += 4;
		}
		else
		{
			stream << (*szString);
			chars++;
		}
		szString++;
	}
	stream << "\\\"";
	chars += 2;
	return chars;
}

void GDomNode::writeJson(std::ostream& stream)
{
	switch(m_type)
	{
		case type_obj:
			stream << "{";
			reverseFieldOrder();
			for(GDomObjField* pField = m_value.m_pLastField; pField; pField = pField->m_pPrev)
			{
				if(pField != m_value.m_pLastField)
					stream << ",";
				writeJSONString(stream, pField->m_pName);
				stream << ":";
				pField->m_pValue->writeJson(stream);
			}
			reverseFieldOrder();
			stream << "}";
			break;
		case type_list:
			stream << "[";
			reverseItemOrder();
			for(GDomListItem* pItem = m_value.m_pLastItem; pItem; pItem = pItem->m_pPrev)
			{
				if(pItem != m_value.m_pLastItem)
					stream << ",";
				pItem->m_pValue->writeJson(stream);
			}
			reverseItemOrder();
			stream << "]";
			break;
		case type_bool:
			stream << (m_value.m_bool ? "true" : "false");
			break;
		case type_int:
			stream << m_value.m_int;
			break;
		case type_double:
			stream << m_value.m_double;
			break;
		case type_string:
			writeJSONString(stream, m_value.m_string);
			break;
		case type_null:
			stream << "null";
			break;
		default:
			ThrowError("Unrecognized node type");
	}
}

size_t GDomNode::writeJsonCpp(std::ostream& stream, size_t col)
{
	switch(m_type)
	{
		case type_obj:
			stream << "{";
			col++;
			reverseFieldOrder();
			for(GDomObjField* pField = m_value.m_pLastField; pField; pField = pField->m_pPrev)
			{
				if(pField != m_value.m_pLastField)
				{
					stream << ",";
					col++;
				}
				if(col >= 200)
				{
					stream << "\"\n\"";
					col = 0;
				}
				col += writeJSONStringCpp(stream, pField->m_pName);
				stream << ":";
				col++;
				col = pField->m_pValue->writeJsonCpp(stream, col);
			}
			reverseFieldOrder();
			stream << "}";
			col++;
			break;
		case type_list:
			stream << "[";
			col++;
			reverseItemOrder();
			for(GDomListItem* pItem = m_value.m_pLastItem; pItem; pItem = pItem->m_pPrev)
			{
				if(pItem != m_value.m_pLastItem)
				{
					stream << ",";
					col++;
				}
				if(col >= 200)
				{
					stream << "\"\n\"";
					col = 0;
				}
				col = pItem->m_pValue->writeJsonCpp(stream, col);
			}
			reverseItemOrder();
			stream << "]";
			col++;
			break;
		case type_bool:
			stream << (m_value.m_bool ? "true" : "false");
			col += 4;
			break;
		case type_int:
			stream << m_value.m_int;
			col += 4; // just a guess
			break;
		case type_double:
			stream << m_value.m_double;
			col += 8; // just a guess
			break;
		case type_string:
			col += writeJSONStringCpp(stream, m_value.m_string);
			break;
		case type_null:
			stream << "null";
			col += 4;
			break;
		default:
			ThrowError("Unrecognized node type");
	}
	if(col >= 200)
	{
		stream << "\"\n\"";
		col = 0;
	}
	return col;
}

bool isXmlInlineType(int type)
{
	if(type == GDomNode::type_string ||
		type == GDomNode::type_int ||
		type == GDomNode::type_double ||
		type == GDomNode::type_bool ||
		type == GDomNode::type_null)
		return true;
	else
		return false;
}

void GDomNode::writeXmlInlineValue(std::ostream& stream)
{
	switch(m_type)
	{
		case type_string:
			stream << m_value.m_string; // todo: escape the string as necessary
			break;
		case type_int:
			stream << m_value.m_int;
			break;
		case type_double:
			stream << m_value.m_double;
			break;
		case type_bool:
			stream << (m_value.m_bool ? "true" : "false");
			break;
		case type_null:
			stream << "null";
			break;
		default:
			ThrowError("Type cannot be inlined");
	}
}

void GDomNode::writeXml(std::ostream& stream, const char* szLabel)
{
	switch(m_type)
	{
		case type_obj:
			{
			stream << "<" << szLabel;
			reverseFieldOrder();
			size_t nonInlinedChildren = 0;
			for(GDomObjField* pField = m_value.m_pLastField; pField; pField = pField->m_pPrev)
			{
				if(isXmlInlineType(pField->m_pValue->m_type))
				{
					stream << " " << pField->m_pName << "=\"";
					pField->m_pValue->writeXmlInlineValue(stream);
					stream << "\"";
				}
				else
					nonInlinedChildren++;
			}
			if(nonInlinedChildren == 0)
				stream << " />";
			else
			{
				stream << ">";
				for(GDomObjField* pField = m_value.m_pLastField; pField; pField = pField->m_pPrev)
				{
					if(!isXmlInlineType(pField->m_pValue->m_type))
						pField->m_pValue->writeXml(stream, pField->m_pName);
				}
				stream << "</" << szLabel << ">";
			}
			reverseFieldOrder();
			}
			return;
		case type_list:
			stream << "<" << szLabel << ">";
			reverseItemOrder();
			for(GDomListItem* pItem = m_value.m_pLastItem; pItem; pItem = pItem->m_pPrev)
				pItem->m_pValue->writeXml(stream, "i");
			reverseItemOrder();
			stream << "</" << szLabel << ">";
			return;
		case type_bool:
			stream << "<" << szLabel << ">";
			stream << (m_value.m_bool ? "true" : "false");
			stream << "</" << szLabel << ">";
			break;
		case type_int:
			stream << "<" << szLabel << ">";
			stream << m_value.m_int;
			stream << "</" << szLabel << ">";
			break;
		case type_double:
			stream << "<" << szLabel << ">";
			stream << m_value.m_double;
			stream << "</" << szLabel << ">";
			break;
		case type_string:
			stream << "<" << szLabel << ">";
			stream << m_value.m_string; // todo: escape the string as necessary
			stream << "</" << szLabel << ">";
			break;
		case type_null:
			stream << "<" << szLabel << ">";
			stream << "null";
			stream << "</" << szLabel << ">";
			break;
		default:
			ThrowError("Unrecognized node type");
	}
}

// -------------------------------------------------------------------------------

class Bogus1
{
public:
	int m_type;
	double m_double;
};

GDomNode* GDom::newObj()
{
	GDomNode* pNewObj = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + sizeof(GDomObjField*));
	pNewObj->m_type = GDomNode::type_obj;
	pNewObj->m_value.m_pLastField = NULL;
	return pNewObj;
}

GDomNode* GDom::newList()
{
	GDomNode* pNewList = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + sizeof(GDomListItem*));
	pNewList->m_type = GDomNode::type_list;
	pNewList->m_value.m_pLastItem = NULL;
	return pNewList;
}

GDomNode* GDom::newNull()
{
	GDomNode* pNewNull = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double));
	pNewNull->m_type = GDomNode::type_null;
	return pNewNull;
}

GDomNode* GDom::newBool(bool b)
{
	GDomNode* pNewBool = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + sizeof(bool));
	pNewBool->m_type = GDomNode::type_bool;
	pNewBool->m_value.m_bool = b;
	return pNewBool;
}

GDomNode* GDom::newInt(long long n)
{
	GDomNode* pNewInt = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + sizeof(long long));
	pNewInt->m_type = GDomNode::type_int;
	pNewInt->m_value.m_int = n;
	return pNewInt;
}

GDomNode* GDom::newDouble(double d)
{
	GDomNode* pNewDouble = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + sizeof(double));
	pNewDouble->m_type = GDomNode::type_double;
	pNewDouble->m_value.m_double = d;
	return pNewDouble;
}

GDomNode* GDom::newString(const char* pString, size_t len)
{
	GDomNode* pNewString = (GDomNode*)m_heap.allocAligned(offsetof(Bogus1, m_double) + len + 1);
	pNewString->m_type = GDomNode::type_string;
	memcpy(pNewString->m_value.m_string, pString, len);
	pNewString->m_value.m_string[len] = '\0';
	return pNewString;
}

GDomNode* GDom::newString(const char* szString)
{
	return newString(szString, strlen(szString));
}

GDomObjField* GDom::newField()
{
	return (GDomObjField*)m_heap.allocAligned(sizeof(GDomObjField));
}

GDomListItem* GDom::newItem()
{
	return (GDomListItem*)m_heap.allocAligned(sizeof(GDomListItem));
}

char* JSON_readString(GTokenizer& tok)
{
	tok.expect("\"");
	char* szTok = tok.nextUntilNotEscaped('\\', tok.charSet("\""));
	tok.advance(1);
	size_t eat = 0;
	char* szString = szTok;
	while(szString[eat] != '\0')
	{
		char c = szString[eat];
		if(c == '\\')
		{
			switch(szString[eat + 1])
			{
				case '"': c = '"'; break;
				case '\\': c = '\\'; break;
				case '/': c = '/'; break;
				case 'b': c = '\b'; break;
				case 'f': c = '\f'; break;
				case 'n': c = '\n'; break;
				case 'r': c = '\r'; break;
				case 't': c = '\t'; break;
				case 'u':
					GAssert(false); // Escaped unicode characters are not yet supported
					c = '_';
					eat += 3;
					break;
				default:
					ThrowError("Unrecognized escape sequence");
			}
			eat++;
		}
		*szString = c;
		szString++;
	}
	*szString = '\0';
	return szTok;
}

GDomNode* GDom::loadJsonObject(GTokenizer& tok)
{
	tok.expect("{");
	GDomNode* pNewObj = newObj();
	bool readyForField = true;
	GCharSet& whitespace = tok.charSet("\t\n\r ");
	while(tok.remaining() > 0)
	{
		tok.skip(whitespace);
		char c = tok.peek();
		if(c == '}')
		{
			tok.advance(1);
			break;
		}
		else if(c == ',')
		{
			if(readyForField)
				ThrowError("Unexpected ',' in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			tok.advance(1);
			readyForField = true;
		}
		else if(c == '\"')
		{
			if(!readyForField)
				ThrowError("Expected a ',' before the next field in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			GDomObjField* pNewField = newField();
			pNewField->m_pPrev = pNewObj->m_value.m_pLastField;
			pNewObj->m_value.m_pLastField = pNewField;
			pNewField->m_pName = m_heap.add(JSON_readString(tok));
			tok.skip(whitespace);
			tok.expect(":");
			tok.skip(whitespace);
			pNewField->m_pValue = loadJsonValue(tok);
			readyForField = false;
		}
		else if(c == '\0')
			ThrowError("Expected a matching '}' in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
		else
			ThrowError("Expected a '}' or a '\"' at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
	}
	return pNewObj;
}

GDomNode* GDom::loadJsonArray(GTokenizer& tok)
{
	tok.expect("[");
	GDomNode* pNewList = newList();
	bool readyForValue = true;
	GCharSet& whitespace = tok.charSet("\t\n\r ");
	while(tok.remaining() > 0)
	{
		tok.skip(whitespace);
		char c = tok.peek();
		if(c == ']')
		{
			tok.advance(1);
			break;
		}
		else if(c == ',')
		{
			if(readyForValue)
				ThrowError("Unexpected ',' in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			tok.advance(1);
			readyForValue = true;
		}
		else if(c == '\0')
			ThrowError("Expected a matching ']' in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
		else
		{
			if(!readyForValue)
				ThrowError("Expected a ',' or ']' in JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
			GDomListItem* pNewItem = newItem();
			pNewItem->m_pPrev = pNewList->m_value.m_pLastItem;
			pNewList->m_value.m_pLastItem = pNewItem;
			pNewItem->m_pValue = loadJsonValue(tok);
			readyForValue = false;
		}
	}
	return pNewList;
}

GDomNode* GDom::loadJsonNumber(GTokenizer& tok)
{
	char* szString = tok.nextWhile(tok.charSet("-.+0-9eE"));
	bool hasPeriod = false;
	for(char* szChar = szString; *szChar != '\0'; szChar++)
	{
		if(*szChar == '.')
			hasPeriod = true;
	}
	if(hasPeriod)
		return newDouble(atof(szString));
	else
	{
#ifdef WINDOWS
		return newInt(_atoi64(szString));
#else
		return newInt(strtoll(szString, (char**)NULL, 10));
#endif
	}
}

GDomNode* GDom::loadJsonValue(GTokenizer& tok)
{
	char c = tok.peek();
	if(c == '"')
		return newString(JSON_readString(tok));
	else if(c == '{')
		return loadJsonObject(tok);
	else if(c == '[')
		return loadJsonArray(tok);
	else if(c == 't')
	{
		tok.expect("true");
		return newBool(true);
	}
	else if(c == 'f')
	{
		tok.expect("false");
		return newBool(false);
	}
	else if(c == 'n')
	{
		tok.expect("null");
		return newNull();
	}
	else if((c >= '0' && c <= '9') || c == '-')
		return loadJsonNumber(tok);
	else if(c == '\0')
	{
		ThrowError("Unexpected end of file while parsing JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
		return NULL;
	}
	else
	{
		ThrowError("Unexpected token while parsing JSON file at line ", to_str(tok.line()), ", col ", to_str(tok.col()));
		return NULL;
	}
}

void GDom::parseJson(GTokenizer& tok)
{
	tok.skip(tok.charSet("\t\n\r "));
	setRoot(loadJsonValue(tok));
}

void GDom::loadJson(const char* szFilename)
{
	GTokenizer tok(szFilename);
	parseJson(tok);
}

void GDom::writeJson(std::ostream& stream)
{
	if(!m_pRoot)
		ThrowError("No root node has been set");
	stream.precision(14);
	m_pRoot->writeJson(stream);
}

void GDom::writeJsonCpp(std::ostream& stream)
{
	if(!m_pRoot)
		ThrowError("No root node has been set");
	stream.precision(14);
	stream << "const char* g_rename_me = \"";
	m_pRoot->writeJsonCpp(stream, 0);
	stream << "\";\n\n";
}

void GDom::saveJson(const char* szFilename)
{
	std::ofstream os;
	os.exceptions(std::ios::failbit|std::ios::badbit);
	try
	{
		os.open(szFilename, std::ios::binary);
	}
	catch(const std::exception&)
	{
		ThrowError("Error creating file: ", szFilename);
	}
	writeJson(os);
}

void GDom::writeXml(std::ostream& stream)
{
	if(!m_pRoot)
		ThrowError("No root node has been set");
	stream.precision(14);
	stream << "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>";
	m_pRoot->writeXml(stream, "root");
}

#ifndef NO_TEST_CODE
// static
void GDom::test()
{
	const char* szTestFile =
		"{\n"
		"	\"name\":\"Bob\\nis\\\\cool\",\n"
		"	\"pet\":{\n"
		"		\"hair\":\"thick\",\n"
		"		\"age\":12\n"
		"	},\n"
		"	\"acquantances\":[\n"
		"		{\n"
		"			\"name\":\"Bill\",\n"
		"			\"age\":31\n"
		"		},\n"
		"		{\n"
		"			\"name\":\"Sally\",\n"
		"			\"age\":24\n"
		"		},\n"
		"		{\n"
		"			\"name\":\"George\",\n"
		"			\"age\":18\n"
		"		}\n"
		"	],\n"
		"	\"names\":[\n"
		"		\"Bob\",\n"
		"		\"Doe\"\n"
		"	],\n"
		"	\"male\"  :  true , \n"
		"	\"height\": 5.8,\n"
		"	\"temp\":98.6\n"
		"}\n";
	GTokenizer tok(szTestFile, strlen(szTestFile));
	GDom doc;
	doc.parseJson(tok);
}
#endif // NO_TEST_CODE

} // namespace GClasses

