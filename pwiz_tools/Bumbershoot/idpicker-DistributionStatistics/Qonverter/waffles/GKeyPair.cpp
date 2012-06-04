/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <stdio.h>
#include "GKeyPair.h"
#include "GBigInt.h"
#include "GError.h"
#include "GRand.h"
#include "GDom.h"
#include <time.h>

namespace GClasses {

GKeyPair::GKeyPair()
{
	m_pPrivateKey = NULL;
	m_pPublicKey = NULL;
	m_pN = NULL;
}

GKeyPair::GKeyPair(GDomNode* pNode)
{
	m_pN = new GBigInt(pNode->field("n"));
	m_pPublicKey = new GBigInt(pNode->field("public"));
	GDomNode* pPrivate = pNode->fieldIfExists("private");
	if(pPrivate)
		m_pPrivateKey = new GBigInt(pPrivate);
	else
		m_pPrivateKey = NULL;
}

GKeyPair::~GKeyPair()
{
	if(m_pPrivateKey)
		m_pPrivateKey->setToZero();
	if(m_pPublicKey)
		m_pPublicKey->setToZero();
	if(m_pN)
		m_pN->setToZero();
	delete(m_pPrivateKey);
	delete(m_pPublicKey);
	delete(m_pN);
}

void GKeyPair::setPublicKey(GBigInt* pPublicKey)
{
	delete(m_pPublicKey);
	m_pPublicKey = pPublicKey;
}

void GKeyPair::setPrivateKey(GBigInt* pPrivateKey)
{
	delete(m_pPrivateKey);
	m_pPrivateKey = pPrivateKey;
}

void GKeyPair::setN(GBigInt* pN)
{
	delete(m_pN);
	m_pN = pN;
}

void GKeyPair::copyPublicKey(GBigInt* pPublicKey)
{
	delete(m_pPublicKey);
	m_pPublicKey = new GBigInt();
	m_pPublicKey->copy(pPublicKey);
}

void GKeyPair::copyPrivateKey(GBigInt* pPrivateKey)
{
	delete(m_pPrivateKey);
	m_pPrivateKey = new GBigInt();
	m_pPrivateKey->copy(pPrivateKey);
}

void GKeyPair::copyN(GBigInt* pN)
{
	delete(m_pN);
	m_pN = new GBigInt();
	m_pN->copy(pN);
}

GBigInt* GKeyPair::publicKey()
{
	return m_pPublicKey;
}

GBigInt* GKeyPair::privateKey()
{
	return m_pPrivateKey;
}

GBigInt* GKeyPair::n()
{
	return m_pN;
}

void GKeyPair::generateKeyPair(unsigned int uintCount, const unsigned int* pRawCryptographicBytes1, const unsigned int* pRawCryptographicBytes2, const unsigned int* pRawCryptographicBytes3)
{
	// Make places to put the data
	GBigInt* pOutPublicKey = new GBigInt();
	GBigInt* pOutPrivateKey = new GBigInt();
	GBigInt* pOutN = new GBigInt();

	// Find two primes
	GBigInt p;
	GBigInt q;
	int n;
	for(n = (int)uintCount - 1; n >= 0; n--)
		p.setUInt(n, pRawCryptographicBytes1[n]);
	for(n = uintCount - 1; n >= 0; n--)
		q.setUInt(n, pRawCryptographicBytes2[n]);
	p.setBit(0, true);
	q.setBit(0, true);
	int nTries = 0;
	while(!p.isPrime())
	{
		p.increment();
		p.increment();
		nTries++;
	}
	nTries = 0;
	while(!q.isPrime())
	{
		q.increment();
		q.increment();
		nTries++;
	}

	// Calculate N (the product of the two primes)
	pOutN->multiply(&p, &q);

	// Calculate prod ((p - 1) * (q - 1))
	p.decrement();
	q.decrement();
	GBigInt prod;
	prod.multiply(&p, &q);

	// Calculate public and private keys
	pOutPublicKey->selectPublicKey(pRawCryptographicBytes3, uintCount, &prod);
	pOutPrivateKey->calculatePrivateKey(pOutPublicKey, &prod);

	// Fill in "this" GKeyPair object
	setPublicKey(pOutPublicKey);
	setPrivateKey(pOutPrivateKey);
	setN(pOutN);
}

GDomNode* GKeyPair::serialize(GDom* pDoc, bool bIncludePrivateKey)
{
	GDomNode* pNode = pDoc->newObj();
	if(!n() || !publicKey())
		ThrowError("No key has been made yet");
	if(bIncludePrivateKey && !privateKey())
		ThrowError("This key-pair doesn't include the private key");
	pNode->addField(pDoc, "n", n()->serialize(pDoc));
	pNode->addField(pDoc, "public", publicKey()->serialize(pDoc));
	if(bIncludePrivateKey)
		pNode->addField(pDoc, "private", privateKey()->serialize(pDoc));
	return pNode;
}

int GKeyPair::maxBlockSize()
{
	return (m_pN->getBitCount() - 1) / 8;
}

unsigned char* GKeyPair::powerMod(const unsigned char* pInput, int nInputSize, bool bPublicKey, int* pnOutputSize)
{
	GBigInt input;
	input.fromByteBuffer(pInput, nInputSize);
	GBigInt results;
	results.powerMod(&input, bPublicKey ? publicKey() : privateKey(), n());
	*pnOutputSize = results.getUIntCount() * sizeof(unsigned int);
	unsigned char* pOutput = (unsigned char*)results.toBufferGiveOwnership();
	while(pOutput[(*pnOutputSize) - 1] == 0)
		(*pnOutputSize)--;
	return pOutput;
}

#ifndef NO_TEST_CODE
/*static*/ void GKeyPair::test()
{
	GRand prng(0);
	unsigned int buf[6];
	for(int i = 0; i < 6; i++)
		buf[i] = (unsigned int)prng.next();
	GKeyPair kp;
	kp.generateKeyPair(2, buf, buf + 2, buf + 4);

	// Make up a message
	GBigInt message;
	message.setUInt(0, 0x6a54);

	// Encrypt it
	GBigInt cypher;
	cypher.powerMod(&message, kp.privateKey(), kp.n());

	// Decrypt it
	GBigInt final;
	final.powerMod(&cypher, kp.publicKey(), kp.n());

	// Check the final value
	if(final.compareTo(&message) != 0)
		ThrowError("failed");
}
#endif // !NO_TEST_CODE

} // namespace GClasses
