/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GHIDDENMARKOVMODEL_H__
#define __GHIDDENMARKOVMODEL_H__

#include "GHiddenMarkovModel.h"
#include <algorithm>
#include "GError.h"
#include "GHolders.h"
#include "GVec.h"
#include <math.h>
#include <cmath>

using namespace GClasses;
using std::vector;

GHiddenMarkovModel::GHiddenMarkovModel(int stateCount, int symbolCount)
: m_stateCount(stateCount), m_symbolCount(symbolCount), m_pTrainingBuffer(NULL)
{
	int modelSize = m_stateCount + m_stateCount * m_stateCount + m_stateCount * m_symbolCount;
	m_pInitialStateProbabilities = new double[modelSize];
	m_pTransitionProbabilities = m_pInitialStateProbabilities + m_stateCount;
	m_pSymbolProbabilities = m_pTransitionProbabilities + m_stateCount * m_stateCount;
}

GHiddenMarkovModel::~GHiddenMarkovModel()
{
	delete[] m_pInitialStateProbabilities;
	delete[] m_pTrainingBuffer;
}

double GHiddenMarkovModel::forwardAlgorithm(const int* pObservations, int len)
{
	// Compute probabilities of initial observation
	GTEMPBUF(double, alpha, 2 * m_stateCount);
	double* pCur = alpha;
	double* pPrev = alpha + m_stateCount;
	double logProb = 0;
	for(int j = 0; j < m_stateCount; j++)
		pCur[j] = m_pInitialStateProbabilities[j] * m_pSymbolProbabilities[m_symbolCount * j + pObservations[0]];

	// Do the rest
	for(int i = 1; i < len; i++)
	{
		// Compute probabilities for the next time step
		std::swap(pPrev, pCur);
		for(int j = 0; j < m_stateCount; j++)
		{
			pCur[j] = 0;
			for(int k = 0; k < m_stateCount; k++)
				pCur[j] += pPrev[k] * m_pTransitionProbabilities[m_stateCount * j + k];
			pCur[j] *= m_pSymbolProbabilities[m_symbolCount * j + pObservations[i]];
		}

		// Normalize to preserve numerical stability
		double sum = GVec::sumElements(pCur, m_stateCount);
		GVec::multiply(pCur, 1.0 / sum, m_stateCount);
		logProb += log(sum);
	}

	// Sum to get final probabilities
	logProb += log(GVec::sumElements(pCur, m_stateCount));
	return logProb;
}

double GHiddenMarkovModel::viterbi(int* pMostLikelyStates, const int* pObservations, int len)
{
	// Compute probabilities of initial observation
	GTEMPBUF(int, backpointers, m_stateCount * (len - 1));
	GTEMPBUF(double, delta, 2 * m_stateCount);
	double* pCur = delta;
	double* pPrev = delta + m_stateCount;
	double logProb = 0;
	for(int j = 0; j < m_stateCount; j++)
		pCur[j] = m_pInitialStateProbabilities[j] * m_pSymbolProbabilities[m_symbolCount * j + pObservations[0]];

	// Do the rest
	for(int i = 1; i < len; i++)
	{
		// Compute probabilities for the next time step
		std::swap(pPrev, pCur);
		for(int j = 0; j < m_stateCount; j++)
		{
			pCur[j] = 0;
			int index = 0;
			for(int k = 0; k < m_stateCount; k++)
			{
				double p = pPrev[k] * m_pTransitionProbabilities[m_stateCount * j + k];
				if(p >= pCur[j])
				{
					pCur[j] = p;
					index = k;
				}
			}
			pCur[j] *= m_pSymbolProbabilities[m_symbolCount * j + pObservations[i]];
			backpointers[m_stateCount * (i - 1) + j] = index;
		}

		// Normalize to preserve numerical stability
		double sum = GVec::sumElements(pCur, m_stateCount);
		GVec::multiply(pCur, 1.0 / sum, m_stateCount);
		logProb += log(sum);
	}

	// Find the best path
	int index = 0;
	for(int j = 1; j < m_stateCount; j++)
	{
		if(pCur[j] > pCur[index])
			index = j;
	}
	pMostLikelyStates[m_stateCount - 1] = index;
	for(int i = m_stateCount - 2; i >= 0; i--)
	{
		pMostLikelyStates[i] = backpointers[m_stateCount * i + index];
		index = pMostLikelyStates[i];
	}
	logProb += log(pCur[index]);
	return logProb;
}

void GHiddenMarkovModel::baumWelchBeginTraining(int maxLen)
{
	// ensure that the buffers are allocated for alpha and beta
	m_maxLen = maxLen;
	delete[] m_pTrainingBuffer;
	m_pTrainingBuffer = new double[
			m_stateCount + // initial state probabilities accumulator
			m_stateCount * m_stateCount + // transition probabilities accumulator
			m_stateCount * m_symbolCount + // symbol probabilities accumulator
			m_stateCount * maxLen + // beta
			m_stateCount + // alpha cur
			m_stateCount + // alpha prev
			m_stateCount + // gamma
			m_stateCount * m_stateCount // xi
		];
}

void GHiddenMarkovModel::baumWelchBeginPass()
{
	// Reset the accumulators
	double* pAccumInitProb = m_pTrainingBuffer;
	double* pAccumTransProb = pAccumInitProb + m_stateCount;
	double* pAccumSymbolProb = pAccumTransProb + m_stateCount * m_stateCount;
	GVec::setAll(pAccumInitProb, 0.0, m_stateCount);
	GVec::setAll(pAccumTransProb, 0.0, m_stateCount * m_stateCount);
	GVec::setAll(pAccumSymbolProb, 0.0, m_stateCount * m_symbolCount);
}

void GHiddenMarkovModel::backwardAlgorithm(const int* pObservations, int len)
{
	// Initialize probabilities of the last state
	double* pBeta = m_pTrainingBuffer + m_stateCount + m_stateCount * m_stateCount + m_stateCount * m_symbolCount;
	double* pCur = pBeta + m_stateCount * (len - 1);
	for(int j = 0; j < m_stateCount; j++)
	{
		*pCur = 1.0;
		pCur++;
	}

	// Induct backwards
	for(int i = len - 2; i >= 0; i--)
	{
		for(int j = 0; j < m_stateCount; j++)
		{
			double d = 0;
			for(int k = 0; k < m_stateCount; k++)
				d += 	m_pTransitionProbabilities[m_stateCount * j + k] *
					m_pSymbolProbabilities[m_symbolCount * k + pObservations[i + 1]] *
					pBeta[m_stateCount * (i + 1) + k];
			pBeta[m_stateCount * i + j] = d;
		}

		// Normalize to preserve numerical stability
		pCur = pBeta + m_stateCount * i;
		GVec::sumToOne(pCur, m_stateCount);
	}
}

void GHiddenMarkovModel::baumWelchAddSequence(const int* pObservations, int len)
{
	// Do backward algorithm to obtain beta values
	backwardAlgorithm(pObservations, len);

	// Do forward algorithm to update accumulators
	double* pAccumInitProb = m_pTrainingBuffer;
	double* pAccumTransProb = pAccumInitProb + m_stateCount;
	double* pAccumSymbolProb = pAccumTransProb + m_stateCount * m_stateCount;
	double* pBeta = pAccumSymbolProb + m_stateCount * m_symbolCount;
	double* pCur = pBeta + m_stateCount * m_maxLen;
	double* pPrev = pCur + m_stateCount;
	double* pGamma = pPrev + m_stateCount;
	double* pXi = pGamma + m_stateCount;
	for(int i = 0; i < len; i++)
	{
		// Compute alpha values
		if(i == 0)
		{
			// Compute initial alpha values
			for(int j = 0; j < m_stateCount; j++)
				pCur[j] = m_pInitialStateProbabilities[j] * m_pSymbolProbabilities[m_symbolCount * j + pObservations[0]];
		}
		else
		{
			// Compute probabilities for the next time step
			std::swap(pPrev, pCur);
			for(int j = 0; j < m_stateCount; j++)
			{
				pCur[j] = 0;
				for(int k = 0; k < m_stateCount; k++)
					pCur[j] += pPrev[k] * m_pTransitionProbabilities[m_stateCount * j + k];
				pCur[j] *= m_pSymbolProbabilities[m_symbolCount * j + pObservations[i]];
			}
		}

		// Normalize to preserve numerical stability
		GVec::sumToOne(pCur, m_stateCount);

		// Compute xi and gamma
		for(int j = 0; j < m_stateCount; j++)
		{
			pGamma[j] = pCur[j] * pBeta[m_stateCount * i + j];
			if(i < len - 1)
			{
				for(int k = 0; k < m_stateCount; k++)
				{
					pXi[m_stateCount * j + k] =
						pCur[j] *
						m_pTransitionProbabilities[m_stateCount * j + k] *
						m_pSymbolProbabilities[m_symbolCount * j + pObservations[i]] *
						pBeta[m_stateCount * (i + 1) + k];
				}
			}
		}
		GVec::sumToOne(pGamma, m_stateCount);
		if(i < len - 1)
			GVec::sumToOne(pXi, m_stateCount * m_stateCount);

		// Accumulate probabilities
		if(i ==  0)
			GVec::add (pAccumInitProb, pGamma, m_stateCount);
		if(i < len - 1)
			GVec::add(pAccumTransProb, pXi, m_stateCount * m_stateCount);
		for(int j = 0; j < m_stateCount; j++)
			pAccumSymbolProb[m_symbolCount * j + pObservations[i]] += pGamma[j];
	}
}

double GHiddenMarkovModel::baumWelchEndPass()
{
	// Normalize all of the probabilities
	double* pAccumInitProb = m_pTrainingBuffer;
	double* pAccumTransProb = pAccumInitProb + m_stateCount;
	double* pAccumSymbolProb = pAccumTransProb + m_stateCount * m_stateCount;
	GVec::sumToOne(pAccumInitProb, m_stateCount);
	for(int i = 0; i < m_stateCount; i++)
	{
		GVec::sumToOne(pAccumTransProb + m_stateCount * i, m_stateCount);
		GVec::sumToOne(pAccumSymbolProb + m_symbolCount * i, m_symbolCount);
	}

	// Measure the change
	double err = 0;
	err += GVec::squaredDistance(m_pInitialStateProbabilities, pAccumInitProb, m_stateCount);
	err += GVec::squaredDistance(m_pTransitionProbabilities, pAccumTransProb, m_stateCount * m_stateCount);
	err += GVec::squaredDistance(m_pSymbolProbabilities, pAccumSymbolProb, m_stateCount * m_symbolCount);

	// Copy over the old model
	GVec::copy(m_pInitialStateProbabilities, pAccumInitProb, m_stateCount);
	GVec::copy(m_pTransitionProbabilities, pAccumTransProb, m_stateCount * m_stateCount);
	GVec::copy(m_pSymbolProbabilities, pAccumSymbolProb, m_stateCount * m_symbolCount);

	return err;
}

void GHiddenMarkovModel::baumWelchEndTraining()
{
	delete[] m_pTrainingBuffer;
	m_pTrainingBuffer = NULL;
}

void GHiddenMarkovModel::baumWelch(vector<int*>& sequences, vector<int>& lengths, int maxPasses)
{
	if(sequences.size() != lengths.size())
		ThrowError("Expected both vectors to have the same size");
	int maxLen = 0;
	for(size_t i = 0; i < lengths.size(); i++)
		maxLen = std::max(maxLen, lengths[i]);
	baumWelchBeginTraining(maxLen);
	double prevErr = 1e200;
	while(maxPasses > 0)
	{
		baumWelchBeginPass();
		for(size_t i = 0; i < lengths.size(); i++)
			baumWelchAddSequence(sequences[i], lengths[i]);
		double err = baumWelchEndPass();
		if(err <= 0)
			break;
		GAssert(err <= prevErr); // The error got worse? This shouldn't happen
		if(1.0 - (err / prevErr) < 0.003)
			break;
		prevErr = err;
		maxPasses--;
	}
	baumWelchEndTraining();
}

#ifndef NO_TEST_CODE
// static
void GHiddenMarkovModel::test()
{
	GHiddenMarkovModel hmm(2, 2);

	// Set priors
	double* pInitial = hmm.initialStateProbabilities();
	pInitial[0] = 0.4;
	pInitial[1] = 0.6;
	double* pTrans = hmm.transitionProbabilities();
	pTrans[0] = 0.4; pTrans[1] = 0.6;
	pTrans[2] = 0.6; pTrans[3] = 0.4;
	double* pSym = hmm.symbolProbabilities();
	pSym[0] = 0.5; pSym[1] = 0.5;
	pSym[2] = 0.3; pSym[3] = 0.7;

	// Do Baum-Welch
	int seq[3];
	seq[0] = 1; seq[1] = 0; seq[2] = 1;
	vector<int*> sequences;
	sequences.push_back(seq);
	vector<int> lengths;
	lengths.push_back(3);
	hmm.baumWelch(sequences, lengths, 1);

	// Check the results
	if(std::abs(pInitial[0] - 0.29849966020178786) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pInitial[1] - 0.70150033979821202) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pTrans[0] - 0.40399443180411687) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pTrans[1] - 0.59600556819588313) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pTrans[2] - 0.61206063945288547) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pTrans[3] - 0.38793936054711436) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pSym[0] - 0.49547467745041401) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pSym[1] - 0.50452532254958593) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pSym[2] - 0.19935077334351725) > 1e-12)
		ThrowError("wrong");
	if(std::abs(pSym[3] - 0.80064922665648264) > 1e-12)
		ThrowError("wrong");
}
#endif

#endif // __GHIDDENMARKOVMODEL_H__
