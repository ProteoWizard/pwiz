#include "GParticleSwarm.h"
#include <string.h>
#include "GVec.h"
#include "GRand.h"
#include <cmath>

namespace GClasses {

GParticleSwarm::GParticleSwarm(GTargetFunction* pCritic, size_t nPopulation, double dMin, double dRange, GRand* pRand)
: GOptimizer(pCritic), m_pRand(pRand)
{
	if(!pCritic->relation()->areContinuous(0, pCritic->relation()->size()))
		ThrowError("Discrete attributes are not supported");
	if(pCritic->isConstrained())
		ThrowError("Sorry, this optimizer doesn't support constrained problems");
	m_dLearningRate = .2;
	m_nDimensions = pCritic->relation()->size();
	m_nPopulation = nPopulation;
	m_pPositions = new double[m_nPopulation * m_nDimensions];
	m_pVelocities = new double[m_nPopulation * m_nDimensions];
	m_pBests = new double[m_nPopulation * m_nDimensions];
	m_pErrors = new double[m_nPopulation];
	m_dMin = dMin;
	m_dRange = dRange;
	reset();
}

/*virtual*/ GParticleSwarm::~GParticleSwarm()
{
	delete(m_pErrors);
	delete(m_pBests);
	delete(m_pVelocities);
	delete(m_pPositions);
}

void GParticleSwarm::reset()
{
	for(size_t i = 0; i < m_nPopulation; i++)
	{
		for(size_t n = 0; n < m_nDimensions; n++)
		{
			m_pPositions[m_nDimensions * i + n] = m_pRand->uniform() * m_dRange + m_dMin;
			m_pVelocities[m_nDimensions * i + n] = m_pRand->uniform() * m_dRange + m_dMin;
			m_pBests[m_nDimensions * i + n] = m_pPositions[m_nDimensions * i + n];
		}
		m_pErrors[i] = 1e100;
	}
	m_nGlobalBest = 0;
}

/*virtual*/ double GParticleSwarm::iterate()
{
	// Advance
	size_t n = m_nPopulation * m_nDimensions;
	for(size_t i = 0; i < n; i++)
		m_pPositions[i] += m_pVelocities[i];

	// Critique the current spots and find the global best
	double dError;
	double dGlobalBest = 1e100;
	for(size_t i = 0; i < m_nPopulation; i++)
	{
		size_t nPos = m_nDimensions * i;
		dError = m_pCritic->computeError(&m_pPositions[nPos]);
		if(dError < m_pErrors[i])
		{
			m_pErrors[i] = dError;
			memcpy(&m_pBests[nPos], &m_pPositions[nPos], sizeof(double) * m_nDimensions);
		}
		if(m_pErrors[i] < dGlobalBest)
		{
			dGlobalBest = m_pErrors[i];
			m_nGlobalBest = i;
		}
	}

	// Update velocities
	size_t nPos = 0;
	n = m_nDimensions * m_nGlobalBest;
	for(size_t i = 0; i < m_nPopulation; i++)
	{
		for(size_t j = 0; j < m_nDimensions; j++)
		{
			m_pVelocities[nPos + j] += m_dLearningRate * m_pRand->uniform() * (m_pBests[nPos + j] - m_pPositions[nPos + j]) + m_dLearningRate * m_pRand->uniform() * (m_pPositions[n + j] - m_pPositions[nPos + j]);
		}
		nPos += m_nDimensions;
	}

	return dGlobalBest;
}


// ----------------------------------------------------------------------------

struct GRubberBallStats
{
	double m_speed;
	double m_odometer;
};

#define NUMBER_OF_DOUBLES_TO_CONTAIN_STATS (sizeof(GRubberBallStats) + sizeof(double) - 1) / sizeof(double)

// Suppose there is a hyper-shell of unknown shape and unknown size that encloses the
// origin. The shell is defined by a function that returns true for all points within
// the shell and false for all points without the shell. Suppose you wish to sample
// the space within the shell. This class tries to produce pseudo-random points
// inside the shell by firing virtual accelerating rubber-balls in random directions
// from the origin. These balls decelerate and pick a new random direction when they
// hit the inner surface of the shell. (Unfortunately, this doesn't do a good job
// with narrow alleys)
GRubberBallSwarm::GRubberBallSwarm(int dims, int ballCount, GRand* pRand)
: m_balls(0, 3 * dims + NUMBER_OF_DOUBLES_TO_CONTAIN_STATS), m_pRand(pRand), m_dims(dims), m_currentBall(0), m_compareBall(0), m_condemnedBall(0)
{
	m_balls.reserve(ballCount);
	m_condemnedDistance = 1e200;
	m_wallPrecisionIters = 16;
	m_acceleration = 1.15;
	m_deceleration = 0.6;
	m_learningRate = 0.1;
	int i;
	for(i = 0; i < ballCount; i++)
		initBall(m_balls.newRow(), 1);
	m_pTemp = new double[2 * dims];
	m_pNormal = m_pTemp + dims;
}

// virtual
GRubberBallSwarm::~GRubberBallSwarm()
{
	delete[] m_pTemp;
}

void GRubberBallSwarm::initBall(double* pBall, double speed)
{
	GVec::setAll(pBall, 0.0, m_dims);
	double* pBallDir = pBall + m_dims;
	m_pRand->spherical(pBallDir, m_dims);
	double* pBallBias = pBallDir + m_dims;
	int i;
	for(i = 0; i < m_dims; i++)
		pBallBias[i] = 1;
	GRubberBallStats* pBallStats = (GRubberBallStats*)(pBallBias + m_dims);
	pBallStats->m_speed = speed;
	pBallStats->m_odometer = 0;
}

void GRubberBallSwarm::mutateBall(int dest, int source)
{
	double* pDest = m_balls.row(dest);
	double* pSource = m_balls.row(source);
	GVec::copy(pDest, pSource, 3 * m_dims + NUMBER_OF_DOUBLES_TO_CONTAIN_STATS);
	m_pRand->spherical(pDest + m_dims, m_dims);
}

void GRubberBallSwarm::advanceBall(double* pBallPos)
{
	// Get the ball
	double* pBallDir = pBallPos + m_dims;
	double* pBallBias = pBallDir + m_dims;
	GRubberBallStats* pBallStats = (GRubberBallStats*)(pBallBias + m_dims);

	// Advance
	GVec::addScaled(pBallPos, pBallStats->m_speed, pBallDir, m_dims);
	if(isInside(pBallPos))
	{
		// Accelerate
		pBallStats->m_speed *= m_acceleration;
		pBallStats->m_odometer += pBallStats->m_speed;
		return;
	}

	// Back up
	GVec::addScaled(pBallPos, -pBallStats->m_speed, pBallDir, m_dims);
	if(!isInside(pBallPos))
	{
		if(GVec::squaredMagnitude(pBallPos, m_dims) == 0)
			ThrowError("GRubberBallSwarm expects the origin to be inside");

		// The shell moved, and the ball is not longer inside, so it must die
		initBall(pBallPos, pBallStats->m_speed / 2);
		return;
	}

	// Move closer to the wall (using binary search)
	double d = pBallStats->m_speed;
	int i;
	for(i = 0; i < m_wallPrecisionIters; i++)
	{
		d *= 0.5;
		GVec::addScaled(pBallPos, d, pBallDir, m_dims);
		if(isInside(pBallPos))
			pBallStats->m_odometer += d;
		else
			GVec::addScaled(pBallPos, -d, pBallDir, m_dims);
	}

	// Approximate the wall's normal vector by sampling
	d *= 4;
	GVec::setAll(m_pNormal, 0.0, m_dims);
	int insideCount = 0;
	for(i = 0; i < m_wallPrecisionIters; i++)
	{
		m_pRand->spherical(m_pTemp, m_dims);
		GVec::addScaled(pBallPos, d, m_pTemp, m_dims);
		if(isInside(pBallPos))
		{
			GVec::add(m_pNormal, m_pTemp, m_dims);
			insideCount++;
		}
		else
			GVec::subtract(m_pNormal, m_pTemp, m_dims);
		GVec::addScaled(pBallPos, -d, m_pTemp, m_dims);
	}
	if(insideCount == 0)
	{
		// Somehow we got stuck in a bad spot, so let's just kill the ball
		initBall(pBallPos, 0.1);
		return;
	}

	// Update the ball's bias
	double sum = 0;
	for(i = 0; i < m_dims; i++)
	{
		m_pTemp[i] = 1.0 / std::max(1e-9, std::abs(m_pNormal[i]));
		sum += m_pTemp[i];
	}
	GVec::multiply(pBallBias, (1.0 - m_learningRate), m_dims);
	GVec::addScaled(pBallBias, m_learningRate * m_dims / sum, m_pTemp, m_dims);
	sum = 0;
	for(i = 0; i < m_dims; i++)
		sum += pBallBias[i];
	GVec::multiply(pBallBias, (double)m_dims / sum, m_dims);

	// Bounce in a biassed random direction
	onBounce(pBallPos);
	m_pRand->spherical(pBallDir, m_dims);
	for(i = 0; i < m_dims; i++)
		pBallDir[i] *= pBallBias[i];
	GVec::normalize(pBallDir, m_dims);
	if(GVec::dotProduct(pBallDir, m_pNormal, m_dims) < 0)
		GVec::multiply(pBallDir, -1, m_dims);
	pBallStats->m_speed *= m_deceleration;
}

void GRubberBallSwarm::iterate()
{
	double* pCurrentBall = m_balls.row(m_currentBall);
	advanceBall(pCurrentBall);
	if(m_currentBall != m_compareBall)
	{
		double* pCompareBall = m_balls.row(m_compareBall);
		double* pCompareDir = pCompareBall + m_dims;
		double* pCompareBias = pCompareDir + m_dims;
		double sum = 0;
		double d;
		int i;
		for(i = 0; i < m_dims; i++)
		{
			d = (pCurrentBall[i] -pCompareBall[i]) / pCompareBias[i];
			sum += (d * d);
		}
		if(sum < m_condemnedDistance)
		{
			m_condemnedBall = m_currentBall;
			m_condemnedDistance = sum;
		}
	}
	if(++m_currentBall >= (int)m_balls.rows())
	{
		if(m_currentBall > 5)
			mutateBall(m_condemnedBall, (int)m_pRand->next(m_balls.rows()));
		m_currentBall = 0;
		m_condemnedDistance = 1e200;
		m_compareBall = (int)m_pRand->next(m_balls.rows());
	}
}

double* GRubberBallSwarm::ball(int index)
{
	return m_balls.row(index);
}

} // namespace GClasses

