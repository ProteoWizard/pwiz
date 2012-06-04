/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GWAVE_H__
#define __GWAVE_H__

#include "GError.h"

namespace GClasses {

struct ComplexNumber;

/// Currently only supports PCM wave format
class GWave
{
protected:
	unsigned int m_size;
	unsigned int m_sampleCount;
	unsigned short m_channels;
	unsigned int m_sampleRate;
	unsigned short m_bitsPerSample;
	unsigned char* m_pData;

public:
	GWave();
	~GWave();

	/// Loads from a file in WAV format
	void load(const char* szFilename);

	/// Saves to a file in WAV format
	void save(const char* szFilename);

	/// Returns a pointer to the raw sample bytes
	unsigned char* data() { return m_pData; }

	/// pData is a pointer to a buffer of raw data.
	/// bitsPerSample should be 8, 16, or 32. If it is 8, sample values are
	/// unsigned (0-255). If it is 16 or 32, sample values are signed.
	/// channels is typically 1 or 2, but it can be larger.
	/// sampleRate is typically one of 8000, 16000, 22050, 44100, 48000, or 96000, but
	/// other sample rates could be used as well.
	void setData(unsigned char* pData, int bitsPerSample, int sampleCount, int channels, int sampleRate);

	/// Returns the number of samples
	int sampleCount() { return m_sampleCount; }

	/// Returns the number of bits-per-sample
	int bitsPerSample() { return m_bitsPerSample; }

	/// Returns the sample rate
	int sampleRate() { return m_sampleRate; }

	/// Returns the number of channels
	unsigned short channels() { return m_channels; }
};


/// This class iterates over the samples in a WAVE file.
/// Regardless of the bits-per-sample, this iterator
/// will convert all samples to doubles with a range from -1 to 1.
class GWaveIterator
{
protected:
	GWave& m_wave;
	size_t m_remaining;
	double* m_pSamples;
	unsigned char* m_pPos;

public:
	GWaveIterator(GWave& wave);
	~GWaveIterator();

	/// Returns the number of samples remaining
	size_t remaining();

	/// Advances to the next sample. Returns false if it
	/// reaches the end of the samples. Returns true otherwise.
	bool advance();

	/// Returns a pointer to a c-dimensional array of doubles,
	/// where c is the number of channels. Each element is
	/// one of the sample values for a channel from -1 to 1.
	double* current();

	/// pSamples should be an array of doubles, one for each channel,
	/// where each value ranges from -1 to 1. Sets the values in the
	/// format specified by the wave object.
	void set(double* pSamples);

	/// Copies the position of another wave iterator that is iterating
	/// on the same wave. (Behavior is undefined if the other iterator
	/// is iterating on a different wave.)
	void copy(GWaveIterator& other);
};


/// This is an abstract class that processes a wave file in blocks. Specifically, it
/// divides the wave file up into overlapping blocks, converts them into Fourier space,
/// calls the abstract "process" method with each block, converts back from Fourier space,
/// and then interpolates to create the wave output.
class GFourierWaveProcessor
{
protected:
	size_t m_blockSize;
	struct ComplexNumber* m_pBufA;
	struct ComplexNumber* m_pBufB;
	struct ComplexNumber* m_pBufC;
	struct ComplexNumber* m_pBufD;
	struct ComplexNumber* m_pBufE;
	struct ComplexNumber* m_pBufFinal;

public:
	/// blockSize must be a power of 2.
	GFourierWaveProcessor(size_t blockSize);
	virtual ~GFourierWaveProcessor();

	/// Transforms signal
	void doit(GWave& signal);

protected:
	/// pBuf represents a block of m_blockSize complex numbers in Fourier space.
	/// This method should transform the block in some way.
	virtual void process(struct ComplexNumber* pBuf) = 0;

	/// Convert the specified channel from an iterator to an array of complex numbers (in temporal space).
	void encodeBlock(GWaveIterator& it, struct ComplexNumber* pBuf, unsigned short chan);

	/// Convert an array of complex numbers (in temporal space) to the specified channel of a wave file.
	void decodeBlock(GWaveIterator& it, unsigned short chan);

	/// Given 3 overlapping blocks (in temporal space), interpolate to create a final block of values.
	void interpolate(struct ComplexNumber* pPre, struct ComplexNumber* pCur, struct ComplexNumber* pPost, size_t graftSamples);
};


} // namespace GClasses

#endif // __GWAVE_H__
