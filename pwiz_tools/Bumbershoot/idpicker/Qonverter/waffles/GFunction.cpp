/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GFunction.h"
#include <math.h>
#include "GError.h"
#include "GHolders.h"
#include "GMath.h"
#include <stdlib.h>
#include <cmath>

using namespace GClasses;
using std::vector;
using std::string;
using std::map;

typedef double (*MathFuncFunc)(vector<double>& params);

namespace GClasses {
class GFunctionNode
{
public:
	GFunctionNode()
	{
	}

	virtual ~GFunctionNode()
	{
	}

	virtual void Link(GFunctionParser* pMaster) = 0;
	virtual double Call(vector<double>& params) = 0;
	virtual bool IsStub() { return false; }
};
}


GFunction::GFunction(GFunctionNode* pRoot, int expectedParams) : m_pRoot(pRoot), m_expectedParams(expectedParams)
{
}

GFunction::~GFunction()
{
	delete(m_pRoot);
}

void GFunction::set(GFunctionNode* pRoot, int expectedParams)
{
	delete(m_pRoot);
	m_pRoot = pRoot;
	m_expectedParams = expectedParams;
}

double GFunction::call(std::vector<double>& params)
{
	return m_pRoot->Call(params);
}


namespace GClasses {
class GFunctionBuiltIn : public GFunctionNode
{
public:
	MathFuncFunc m_pFunc;

	GFunctionBuiltIn(MathFuncFunc pFunc) : GFunctionNode(), m_pFunc(pFunc)
	{
	}

	virtual ~GFunctionBuiltIn()
	{
	}

	virtual void Link(GFunctionParser* pMaster) {}

	double Call(vector<double>& params)
	{
		return m_pFunc(params);
	}

	// The operators
	static double plus(vector<double>& params) { return params[0] + params[1]; }
	static double minus(vector<double>& params) { return params[0] - params[1]; }
	static double times(vector<double>& params) { return params[0] * params[1]; }
	static double divide(vector<double>& params) { return params[0] / params[1]; }
	static double modulus(vector<double>& params) { return fmod(params[0], params[1]); }
	static double exponent(vector<double>& params) { return pow(params[0], params[1]); }
	static double negate(vector<double>& params) { return -params[0]; }

	// built-in functions
	static double _abs(vector<double>& params) { return std::abs(params[0]); }
	static double _acos(vector<double>& params) { return acos(params[0]); }
#ifndef WINDOWS
	static double _acosh(vector<double>& params) { return acosh(params[0]); }
#endif
	static double _asin(vector<double>& params) { return asin(params[0]); }
#ifndef WINDOWS
	static double _asinh(vector<double>& params) { return asinh(params[0]); }
#endif
	static double _atan(vector<double>& params) { return atan(params[0]); }
#ifndef WINDOWS
	static double _atanh(vector<double>& params) { return atanh(params[0]); }
#endif
	static double _ceil(vector<double>& params) { return ceil(params[0]); }
	static double _cos(vector<double>& params) { return cos(params[0]); }
	static double _cosh(vector<double>& params) { return cosh(params[0]); }
#ifndef WINDOWS
	static double _erf(vector<double>& params) { return erf(params[0]); }
#endif
	static double _floor(vector<double>& params) { return floor(params[0]); }
	static double _gamma(vector<double>& params) { return GMath::gamma(params[0]); }
	static double _lgamma(vector<double>& params) { return GMath::logGamma(params[0]); }
	static double _log(vector<double>& params) { return log(params[0]); }
	static double _max(vector<double>& params)
	{
		vector<double>::iterator it = params.begin();
		double d = *it;
		for(it++; it != params.end(); it++)
			d = std::max(d, *it);
		return d;
	}
	static double _min(vector<double>& params)
	{
		vector<double>::iterator it = params.begin();
		double d = *it;
		for(it++; it != params.end(); it++)
			d = std::min(d, *it);
		return d;
	}
	static double _sin(vector<double>& params) { return sin(params[0]); }
	static double _sinh(vector<double>& params) { return sinh(params[0]); }
	static double _sqrt(vector<double>& params) { return sqrt(params[0]); }
	static double _tan(vector<double>& params) { return tan(params[0]); }
	static double _tanh(vector<double>& params) { return tanh(params[0]); }
};

class GFunctionStub : public GFunctionNode
{
public:
	string m_name;

	GFunctionStub(const char* szName) : GFunctionNode(), m_name(szName)
	{
	}

	virtual ~GFunctionStub()
	{
	}

	virtual bool IsStub() { return true; }

	virtual void Link(GFunctionParser* pMaster)
	{
		GAssert(false); // This should never be called.
	}

	virtual double Call(vector<double>& params)
	{
		ThrowError("GFunctionStub::Eval should never be called. Was Link called?");
		return -1e308;
	}
};

class GFunctionCall : public GFunctionNode
{
public:
	GFunctionNode* m_pFunction;
	vector<GFunctionNode*> m_children;
	vector<double> m_params;

	GFunctionCall(GFunctionNode* pFunction) : GFunctionNode(), m_pFunction(pFunction)
	{
	}

	virtual ~GFunctionCall()
	{
		for(vector<GFunctionNode*>::iterator it = m_children.begin(); it != m_children.end(); it++)
			delete(*it);
	}

	void AddChild(GFunctionNode* pChild)
	{
		m_children.push_back(pChild);
		m_params.push_back(0.0);
	}

	virtual void Link(GFunctionParser* pMaster)
	{
		if(m_pFunction->IsStub())
		{
			GFunctionStub* pStub = (GFunctionStub*)m_pFunction;
			GFunction* pFunc = pMaster->getFunction(pStub->m_name.c_str());
			if(pFunc->m_expectedParams >= 0)
			{
				// An exact number of parameters is expected
				if((int)m_children.size() != pFunc->m_expectedParams)
					ThrowError("The function ", pStub->m_name.c_str(), " expects ", to_str(pFunc->m_expectedParams), " parameters. (Got ", to_str(m_children.size()), ").");
			}
			else
			{
				// A minimum number of parameters is expected
				if((int)m_children.size() < -pFunc->m_expectedParams - 1)
					ThrowError("The function ", pStub->m_name.c_str(), " expects at least ", to_str(-pFunc->m_expectedParams - 1), " parameters. (Got ", to_str(m_children.size()), ".)");
			}
			m_pFunction = pFunc->m_pRoot;
		}
		for(vector<GFunctionNode*>::iterator it = m_children.begin(); it != m_children.end(); it++)
			(*it)->Link(pMaster);
	}

	virtual double Call(vector<double>& params)
	{
		vector<GFunctionNode*>::iterator itChild = m_children.begin();
		vector<double>::iterator itParam = m_params.begin();
		while(itChild != m_children.end())
		{
			*itParam = (*itChild)->Call(params);
			itChild++;
			itParam++;
		}
		return m_pFunction->Call(m_params);
	}
};

class GFunctionVariable : public GFunctionNode
{
public:
	int m_index;

	GFunctionVariable(int index) : GFunctionNode(), m_index(index)
	{
	}

	virtual ~GFunctionVariable()
	{
	}

	virtual void Link(GFunctionParser* pMaster) {}

	virtual double Call(vector<double>& params)
	{
		return params[m_index];
	}
};

class GFunctionConstant : public GFunctionNode
{
public:
	double m_value;

	GFunctionConstant(double value) : GFunctionNode(), m_value(value)
	{
	}

	virtual ~GFunctionConstant()
	{
	}

	virtual void Link(GFunctionParser* pMaster) {}

	virtual double Call(vector<double>& params)
	{
		return m_value;
	}
};

// --------------------------------------------------------------

class GFunctionTokenizer
{
public:
	enum token_type
	{
		name,
		number,
		symbol,
	};

	const char* m_pEquation;
	const char* m_pNext;
	int m_len;
	token_type m_type;

	GFunctionTokenizer(const char* szEquation)
	: m_pEquation(szEquation), m_pNext(NULL), m_len(0), m_type(name)
	{
	}

	static bool IsNameChar(char c)
	{
		if(c >= 'a' && c <= 'z')
			return true;
		if(c == '_')
			return true;
		if(c >= 'A' && c <= 'Z')
			return true;
		return false;
	}

	static bool IsDigit(char c)
	{
		if(c >= '0' && c <= '9')
			return true;
		return false;
	}

	void MeasureTokenLength()
	{
		if(IsNameChar(*m_pNext))
		{
			for(m_len = 1; IsNameChar(m_pNext[m_len]) || IsDigit(m_pNext[m_len]); m_len++)
			{
			}
			m_type = name;
		}
		else if(IsDigit(*m_pNext) || *m_pNext == '.')
		{
			int exps = 0;
			int decimals = 0;
			if(*m_pNext == '.')
				decimals++;
			for(m_len = 1; m_pNext[m_len] != '\0'; m_len++)
			{
				if(IsDigit(m_pNext[m_len]))
					continue;
				if(m_pNext[m_len] == '.' && decimals == 0 && exps == 0)
				{
					decimals++;
					continue;
				}
				if(m_pNext[m_len] == 'e' && IsDigit(m_pNext[m_len + 1]) && exps == 0)
				{
					exps++;
					continue;
				}
				break;
			}
			m_type = number;
		}
		else
		{
			m_len = 1;
			m_type = symbol;
		}
	}

	bool Next()
	{
		if(m_pNext)
			m_pNext += m_len;
		else
			m_pNext = m_pEquation;
		while(*m_pNext <= ' ' && *m_pNext != '\0')
			m_pNext++;
		if(*m_pNext == '\0')
		{
			m_pNext = NULL;
			return false;
		}
		MeasureTokenLength();
		return true;
	}
};
}

// --------------------------------------------------------------

GFunctionParser::GFunctionParser(const char* szEquations)
{
	// Operators
	m_pNegate = new GFunctionBuiltIn(GFunctionBuiltIn::negate);
	m_pPlus = new GFunctionBuiltIn(GFunctionBuiltIn::plus);
	m_pMinus = new GFunctionBuiltIn(GFunctionBuiltIn::minus);
	m_pTimes = new GFunctionBuiltIn(GFunctionBuiltIn::times);
	m_pDivide = new GFunctionBuiltIn(GFunctionBuiltIn::divide);
	m_pModulus = new GFunctionBuiltIn(GFunctionBuiltIn::modulus);
	m_pExponent = new GFunctionBuiltIn(GFunctionBuiltIn::exponent);

	// Built-in constants (just a function with no parameters)
	addFunction("e", new GFunctionConstant(M_E), 0);
	addFunction("pi", new GFunctionConstant(M_PI), 0);

	// Built-in functions
	addFunction("abs", new GFunctionBuiltIn(GFunctionBuiltIn::_abs), 1);
	addFunction("acos", new GFunctionBuiltIn(GFunctionBuiltIn::_acos), 1);
#ifndef WINDOWS
	addFunction("acosh", new GFunctionBuiltIn(GFunctionBuiltIn::_acosh), 1);
#endif
	addFunction("asin", new GFunctionBuiltIn(GFunctionBuiltIn::_asin), 1);
#ifndef WINDOWS
	addFunction("asinh", new GFunctionBuiltIn(GFunctionBuiltIn::_asinh), 1);
#endif
	addFunction("atan", new GFunctionBuiltIn(GFunctionBuiltIn::_atan), 1);
#ifndef WINDOWS
	addFunction("atanh", new GFunctionBuiltIn(GFunctionBuiltIn::_atanh), 1);
#endif
	addFunction("ceil", new GFunctionBuiltIn(GFunctionBuiltIn::_ceil), 1);
	addFunction("cos", new GFunctionBuiltIn(GFunctionBuiltIn::_cos), 1);
	addFunction("cosh", new GFunctionBuiltIn(GFunctionBuiltIn::_cosh), 1);
#ifndef WINDOWS
	addFunction("erf", new GFunctionBuiltIn(GFunctionBuiltIn::_erf), 1);
#endif
	addFunction("floor", new GFunctionBuiltIn(GFunctionBuiltIn::_floor), 1);
	addFunction("gamma", new GFunctionBuiltIn(GFunctionBuiltIn::_gamma), 1);
	addFunction("lgamma", new GFunctionBuiltIn(GFunctionBuiltIn::_lgamma), 1);
	addFunction("log", new GFunctionBuiltIn(GFunctionBuiltIn::_log), 1);
	addFunction("max", new GFunctionBuiltIn(GFunctionBuiltIn::_max), -2/*at least 1 param*/);
	addFunction("min", new GFunctionBuiltIn(GFunctionBuiltIn::_min), -2/*at least 1 param*/);
	addFunction("sin", new GFunctionBuiltIn(GFunctionBuiltIn::_sin), 1);
	addFunction("sinh", new GFunctionBuiltIn(GFunctionBuiltIn::_sinh), 1);
	addFunction("sqrt", new GFunctionBuiltIn(GFunctionBuiltIn::_sqrt), 1);
	addFunction("tan", new GFunctionBuiltIn(GFunctionBuiltIn::_tan), 1);
	addFunction("tanh", new GFunctionBuiltIn(GFunctionBuiltIn::_tanh), 1);


	GFunctionTokenizer tokenizer(szEquations);
	while(tokenizer.Next())
	{
		size_t len = m_tokens.size();
		m_tokens.resize(len + 1);
		m_tokens[len] = string(tokenizer.m_pNext, tokenizer.m_len);
	}
	parseFunctionList(m_tokens);
}

GFunctionParser::~GFunctionParser()
{
	for(map<const char*, GFunction*, strCmp>::iterator it = m_functions.begin(); it != m_functions.end(); it++)
		delete(it->second);
	flushStubs();
	delete(m_pNegate);
	delete(m_pPlus);
	delete(m_pMinus);
	delete(m_pTimes);
	delete(m_pDivide);
	delete(m_pModulus);
	delete(m_pExponent);
}

void GFunctionParser::flushStubs()
{
	for(vector<GFunctionStub*>::iterator it = m_stubs.begin(); it != m_stubs.end(); it++)
		delete(*it);
	m_stubs.clear();
}

void GFunctionParser::addFunction(const char* name, GFunctionNode* pRoot, int expectedParams)
{
	GFunction* pFunc = m_functions[name];
	if(pFunc)
		pFunc->set(pRoot, expectedParams);
	else
	{
		pFunc = new GFunction(pRoot, expectedParams);
		m_functions[name] = pFunc;
	}
}

GFunction* GFunctionParser::getFunctionNoThrow(const char* name)
{
	map<const char*, GFunction*, strCmp>::iterator it = m_functions.find(name);
	if(it == m_functions.end())
		return NULL;
	return it->second;
}

GFunction* GFunctionParser::getFunction(const char* name)
{
	GFunction* pFunc = getFunctionNoThrow(name);
	if(!pFunc)
		ThrowError("No identifier named \"", name, "\" is currently defined");
	return pFunc;
}

int GFunctionParser::findOperatorWithLowestPrecidence(vector<string>& tokens, int start, int count)
{
	int priority = 100;
	int token = -1;
	int nests = 0;
	for(int i = 0; i < count; i++)
	{
		string& tok = tokens[start + i];
		if(tok.compare("(") == 0)
			nests++;
		else if(tok.compare(")") == 0)
		{
			nests--;
			if(i < 0)
				ThrowError("Unbalanced parentheses");
		}
		else if(nests == 0)
		{
			// negator		16
			// exponent		12
			// times/divide/modulus	8
			// plus/minus		4
			char c = tok[0];
			if(GFunctionTokenizer::IsNameChar(c))
			{
			}
			else if(GFunctionTokenizer::IsDigit(c) || c == '.')
			{
			}
			else
			{
				if(c == '-' && (i == 0 || (
							!GFunctionTokenizer::IsDigit(tokens[start + i - 1][0]) &&
							!GFunctionTokenizer::IsNameChar(tokens[start + i - 1][0]) &&
							tokens[start + i - 1][0] != ')' &&
							tokens[start + i - 1][0] != '.'
						)))
				{
					if(priority >= 16)
					{
						priority = 16;
						token = start + i;
					}
				}
				else if(c == '^')
				{
					if(priority >= 12)
					{
						priority = 12;
						token = start + i;
					}
				}
				else if(c == '*' || c == '/' || c == '%')
				{
					if(priority >= 8)
					{
						priority = 8;
						token = start + i;
					}
				}
				else if(c == '+' || c == '-')
				{
					if(priority >= 4)
					{
						priority = 4;
						token = start + i;
					}
				}
				else
					ThrowError("Unrecognized operator: ", to_str(c));
			}
		}
	}
	return token;
}

GFunctionNode* GFunctionParser::parseMathOperator(std::vector<std::string>& variables, vector<string>& tokens, int start, int count, int index, int depth)
{
	// We've got a math operator (^,*,/,+,-,%)
	GFunctionNode* pLeft = NULL;
	if(index > start)
		pLeft = parseFunctionBody(variables, tokens, start, index - start, depth);
	Holder<GFunctionNode> hLeft(pLeft);
	GFunctionNode* pRight = NULL;
	if(index < start + count - 1)
		pRight = parseFunctionBody(variables, tokens, index + 1, start + count - index - 1, depth);
	Holder<GFunctionNode> hRight(pRight);
	if(!pLeft)
	{
		if(pRight && tokens[index].compare("-") == 0)
		{
			GFunctionCall* pFunc = new GFunctionCall(m_pNegate);
			pFunc->AddChild(hRight.release());
			return pFunc;
		}
		else
			ThrowError("Expected something before the operator: ", tokens[index].c_str());
	}
	if(!pRight)
		ThrowError("Expected something after the operator: ", tokens[index].c_str());
	char c = tokens[index][0];
	GFunctionCall* pFunc = NULL;
	if(c == '^')
		pFunc = new GFunctionCall(m_pExponent);
	else if(c == '*')
		pFunc = new GFunctionCall(m_pTimes);
	else if(c == '/')
		pFunc = new GFunctionCall(m_pDivide);
	else if(c == '%')
		pFunc = new GFunctionCall(m_pModulus);
	else if(c == '+')
		pFunc = new GFunctionCall(m_pPlus);
	else if(c == '-')
		pFunc = new GFunctionCall(m_pMinus);
	else
		ThrowError("Unrecognized operator: ", to_str(c));
	pFunc->AddChild(hLeft.release());
	pFunc->AddChild(hRight.release());
	return pFunc;
}


void GFunctionParser::parseCommaSeparatedChildren(std::vector<std::string>& variables, GFunctionCall* pFunc, vector<string>& tokens, int start, int count, int depth)
{
	int childBegin = start;
	int nests = 0;
	for(int i = 0; i < count; i++)
	{
		string& tok = tokens[start + i];
		if(tok.compare("(") == 0)
			nests++;
		else if(tok.compare(")") == 0)
		{
			nests--;
			if(i < 0)
				ThrowError("Unbalanced parentheses");
		}
		else if(nests == 0)
		{
			if(tok.compare(",") == 0)
			{
				pFunc->AddChild(parseFunctionBody(variables, tokens, childBegin, start + i - childBegin, depth));
				childBegin = start + i + 1;
			}
		}
	}
	pFunc->AddChild(parseFunctionBody(variables, tokens, childBegin, start + count - childBegin, depth));
}

GFunctionCall* GFunctionParser::makeStubbedOperator(const char* szName)
{
	GFunctionStub* pStub = new GFunctionStub(szName);
	m_stubs.push_back(pStub);
	return new GFunctionCall(pStub);
}

GFunctionNode* GFunctionParser::parseFunctionBody(std::vector<std::string>& variables, vector<string>& tokens, int start, int count, int depth)
{
	// Protect against maliciously designed formulas
	if(depth > 10000)
		ThrowError("Pathologically deep nesting");
	if(count <= 0)
		ThrowError("Empty expression");

	// Handle enclosing parens
	if(tokens[start].compare("(") == 0 && tokens[start + count - 1].compare(")") == 0)
	{
		int depth = 1;
		int i;
		for(i = 1; i < count - 1; i++)
		{
			if(tokens[start + i].compare("(") == 0)
				depth++;
			else if(tokens[start + i].compare(")") == 0)
			{
				depth--;
				if(depth == 0)
					break;
			}
		}
		if(i >= count - 1)
			return parseFunctionBody(variables, tokens, start + 1, count - 2, depth + 1);
	}

	// Generate a node
	if(count == 1)
	{
		char c = tokens[start][0];
		if(GFunctionTokenizer::IsNameChar(c))
		{
			// See if it's a variable
			for(size_t i = 0; i < variables.size(); i++)
			{
				if(variables[i].compare(tokens[start]) == 0)
				{
					// We've got a variable
					return new GFunctionVariable((int)i);
				}
			}

			// It must be a constant (parameterless function), so make a stub
			return makeStubbedOperator(tokens[start].c_str());
		}
		else
		{
			// We've got a numeric constant
			if(!GFunctionTokenizer::IsDigit(c) && c != '.')
				ThrowError("Cannot parse symbol: ", tokens[start].c_str());
			return new GFunctionConstant(atof(tokens[start].c_str()));
		}
	}
	else
	{
		int index = findOperatorWithLowestPrecidence(tokens, start, count);
		if(index >= 0)
			return parseMathOperator(variables, tokens, start, count, index, depth + 1);
		else
		{
			// We've got a named function (like log(x), sin(y), or max(x,y))
			if(count < 3 || tokens[start + 1].compare("(") != 0 || tokens[start + count - 1].compare(")") != 0)
			{
				string s;
				for(int i = 0; i < count; i++)
					s += tokens[start + i];
				ThrowError("Cannot parse this portion of the expression: ", s.c_str());
			}
			GFunctionCall* pFunc = makeStubbedOperator(tokens[start].c_str());
			Holder<GFunctionCall> hFunc(pFunc);
			parseCommaSeparatedChildren(variables, pFunc, tokens, start + 2, count - 3, depth + 1);
			return hFunc.release();
		}
	}
}

void GFunctionParser::parseVariableNames(vector<string>& variables, vector<string>& tokens, int start, int count)
{
	for(int i = 0; i < count; i++)
	{
		char c = tokens[start + i][0];
		if(!GFunctionTokenizer::IsNameChar(c))
			ThrowError("Expected a variable name to start with a letter or '_'");
		variables.push_back(tokens[start + i].c_str());
		if(i + 1 < count && tokens[start + i + 1].compare(",") != 0)
			ThrowError("Expected a comma between variable declarations");
		i++;
	}
}

GFunctionNode* GFunctionParser::parseFunction(vector<string>& tokens, int start, int count)
{
	if(count == 0)
		return NULL;

	// Find the '='
	int equalPos;
	for(equalPos = 0; equalPos < count; equalPos++)
	{
		if(tokens[start + equalPos].compare("=") == 0)
			break;
	}
	if(equalPos >= count)
		ThrowError("All functions must contain an '='");
	if(equalPos == 0)
		ThrowError("All functions must have a name");

	// Parse the variable names
	const char* szFunctionName = tokens[start].c_str();
	vector<string> variables;
	if(equalPos > 1)
	{
		if(tokens[start + 1].compare("(") != 0)
			ThrowError("Expected a '(' after ", szFunctionName);
		if(tokens[start + equalPos - 1].compare(")") != 0)
			ThrowError("Expected a ')' before the '='");
		parseVariableNames(variables, tokens, start + 2, equalPos - 3);
	}

	// Parse the body
	GFunctionNode* pRoot = parseFunctionBody(variables, tokens, start + equalPos + 1, count - (equalPos + 1), 0);
	addFunction(szFunctionName, pRoot, (int)variables.size());
	return pRoot;
}

void GFunctionParser::parseFunctionList(vector<string>& tokens)
{
	// Parse all the functions
	vector<GFunctionNode*> functions;
	int start = 0;
	for(int i = 0; i < (int)tokens.size(); i++)
	{
		if(tokens[i].compare(";") == 0)
		{
			functions.push_back(parseFunction(tokens, start, i - start));
			start = i + 1;
		}
	}
	functions.push_back(parseFunction(tokens, start, (int)tokens.size() - start));

	// Link all the functions
	for(vector<GFunctionNode*>::iterator it = functions.begin(); it != functions.end(); it++)
	{
		if(*it != NULL)
			(*it)->Link(this);
	}
	flushStubs();
}

// static
void GFunctionParser::test()
{
	{
		GFunctionParser mfp("f(x)=1/(1+e^-x)");
		GFunction* pFunc = mfp.getFunction("f");
		if(pFunc->m_expectedParams != 1)
			ThrowError("Wrong number of expected parameters");
		double x = 1.23456789;
		vector<double> params;
		params.push_back(x);
		double y = pFunc->call(params);
		if(std::abs(y - GMath::logistic(x)) > 1e-12)
			ThrowError("Wrong answer");
	}

	{
		GFunctionParser mfp("h(bob)=bob^2;somefunc(x)=3+blah(x,5)*h(x)-(x/foo);blah(a,b)=a*b-b;foo=3.2");
		GFunction* pFunc = mfp.getFunction("somefunc");
		if(pFunc->m_expectedParams != 1)
			ThrowError("Wrong number of expected parameters");
		double x = 1.1;
		vector<double> params;
		params.push_back(x);
		double y = pFunc->call(params);
		if(std::abs(y - 3.26125) > 1e-12)
			ThrowError("Wrong answer");
	}
}
