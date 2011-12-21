/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GKEYPAIR_H__
#define __GKEYPAIR_H__

namespace GClasses {

class GBigInt;
class GRandCrypto;
class GDomNode;
class GDom;

// This is my home-made (so don't trust it) implementation of symmetric key cryptography.
class GKeyPair
{
private:
	GBigInt* m_pPublicKey;
	GBigInt* m_pPrivateKey;
	GBigInt* m_pN;

public:
	GKeyPair();
	GKeyPair(GDomNode* pNode);
	virtual ~GKeyPair();

#ifndef NO_TEST_CODE
	static void test();
#endif // !NO_TEST_CODE

	// Serialize the key pair
	GDomNode* serialize(GDom* pDoc, bool bIncludePrivateKey);

	// Generates a key-pair using the 3 buffers of cryptographic uint values you pass in.
	// (Typically, you will generate the values in these buffers by using a one-way hash
	// algorithm to digest a larger buffer of entropy values collected from mouse-wiggles,
	// key-strokes, and other such sources of unpredictable values.)
	// uintCount specifies the number of cryptographically random unsigned integers in each of the 3 buffers.
	void generateKeyPair(unsigned int uintCount, const unsigned int* pRawCryptographicBytes1, const unsigned int* pRawCryptographicBytes2, const unsigned int* pRawCryptographicBytes3);

	// Takes ownership of the GBigInt you pass in
	void setPublicKey(GBigInt* pPublicKey);

	// Takes ownership of the GBigInt you pass in
	void setPrivateKey(GBigInt* pPrivateKey);

	// Takes ownership of the GBigInt you pass in
	void setN(GBigInt* pN);

	// Copies the GBigInt you pass in
	void copyPublicKey(GBigInt* pPublicKey);

	// Copies the GBigInt you pass in
	void copyPrivateKey(GBigInt* pPrivateKey);

	// Copies the GBigInt you pass in
	void copyN(GBigInt* pN);

	// Get the public part of the key (not including N which is public too)
	GBigInt* publicKey();

	// Get the private part of the key
	GBigInt* privateKey();

	// Get the N part of the key
	GBigInt* n();

	// Returns the maximum number of bytes that you can encrypt using the PowerMod method
	int maxBlockSize();

	// This is the method that encrypts/decrypts your message.
	// If bPublicKey is true, it uses the public key.  If false it uses the private key.
	// Note: you must delete the buffer this returns
	unsigned char* powerMod(const unsigned char* pInput, int nInputSize, bool bPublicKey, int* pnOutputSize);
};

} // namespace GClasses

#endif // __GKEYPAIR_H__
