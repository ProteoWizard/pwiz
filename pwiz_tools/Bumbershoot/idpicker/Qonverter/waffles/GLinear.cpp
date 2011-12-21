/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GLinear.h"
#include "GDom.h"
#include "GTransform.h"
#include "GRand.h"
#include "GVec.h"
#include <cmath>
#include <math.h>

using namespace GClasses;

GLinearRegressor::GLinearRegressor(GRand& rand)
: GSupervisedLearner(rand), m_pBeta(NULL), m_pEpsilon(NULL)
{
}

GLinearRegressor::GLinearRegressor(GDomNode* pNode, GLearnerLoader& ll)
: GSupervisedLearner(pNode, ll)
{
	m_pBeta = new GMatrix(pNode->field("beta"));
	m_pEpsilon = new double[m_pBeta->rows()];
	GDomListIterator it(pNode->field("epsilon"));
	GVec::deserialize(m_pEpsilon, it);
}

// virtual
GLinearRegressor::~GLinearRegressor()
{
	clear();
}

// virtual
GDomNode* GLinearRegressor::serialize(GDom* pDoc)
{
	GDomNode* pNode = baseDomNode(pDoc, "GLinearRegressor");
	pNode->addField(pDoc, "beta", m_pBeta->serialize(pDoc));
	pNode->addField(pDoc, "epsilon", GVec::serialize(pDoc, m_pEpsilon, m_pBeta->rows()));
	return pNode;
}

void GLinearRegressor::refine(GMatrix& features, GMatrix& labels, double learningRate, size_t epochs, double learningRateDecayFactor)
{
	size_t fDims = features.cols();
	size_t lDims = labels.cols();
	size_t* pIndexes = new size_t[features.rows()];
	ArrayHolder<size_t> hIndexes(pIndexes);
	GIndexVec::makeIndexVec(pIndexes, features.rows());
	for(size_t i = 0; i < epochs; i++)
	{
		GIndexVec::shuffle(pIndexes, features.rows(), &m_rand);
		size_t* pIndex = pIndexes;
		for(size_t j = 0; j < features.rows(); j++)
		{
			double* pFeat = features[*pIndex];
			double* pLab = labels[*pIndex];
			double* pBias = m_pEpsilon;
			for(size_t k = 0; k < lDims; k++)
			{
				double err = *pLab - (GVec::dotProduct(pFeat, m_pBeta->row(k), fDims) + *pBias);
				double* pF = pFeat;
				double* pW = m_pBeta->row(k);
				for(size_t l = 0; l < fDims; l++)
				{
					*pW += *pF * learningRate * err;
					pF++;
					pW++;
				}
				*pBias += learningRate * err;
				pLab++;
				pBias++;
			}
			pIndex++;
		}
		learningRate *= learningRateDecayFactor;
	}
}

// virtual
void GLinearRegressor::trainInner(GMatrix& features, GMatrix& labels)
{
	// Use a fast, but not-very-numerically-stable technique to compute an initial approximation for beta and epsilon
	clear();
	GMatrix* pAll = GMatrix::mergeHoriz(&features, &labels);
	Holder<GMatrix> hAll(pAll);
	GPCA pca(features.cols(), &m_rand);
	pca.train(*pAll);
	size_t inputs = features.cols();
	size_t outputs = labels.cols();
	GMatrix f(inputs, inputs);
	GMatrix l(inputs, outputs);
	for(size_t i = 0; i < inputs; i++)
	{
		GVec::copy(f[i], pca.basis(i), inputs);
		double sqmag = GVec::squaredMagnitude(f[i], inputs);
		if(sqmag > 1e-10)
			GVec::multiply(f[i], 1.0 / sqmag, inputs);
		GVec::copy(l[i], pca.basis(i) + inputs, outputs);
	}
	m_pBeta = GMatrix::multiply(l, f, true, false);
	m_pEpsilon = new double[outputs];
	m_pBeta->multiply(pca.mean(), m_pEpsilon, false);
	GVec::multiply(m_pEpsilon, -1.0, outputs);
	GVec::add(m_pEpsilon, pca.mean() + inputs, outputs);

	// Refine the results using gradient descent
	refine(features, labels, 0.06, 10, 0.75);
}

// virtual
void GLinearRegressor::predictDistributionInner(const double* pIn, GPrediction* pOut)
{
	ThrowError("Sorry, this model cannot predict a distribution.");
}

// virtual
void GLinearRegressor::predictInner(const double* pIn, double* pOut)
{
	m_pBeta->multiply(pIn, pOut, false);
	GVec::add(pOut, m_pEpsilon, m_pBeta->rows());
}

// virtual
void GLinearRegressor::clear()
{
	delete(m_pBeta);
	m_pBeta = NULL;
	delete[] m_pEpsilon;
	m_pEpsilon = NULL;
}

void GLinearRegressor::autoTune(GMatrix& features, GMatrix& labels)
{
	// This model has no parameters to tune
}

#ifndef NO_TEST_CODE
void GLinearRegressor_linear_test(GRand& prng)
{
	// Train
	GMatrix features1(0, 3);
	GMatrix labels1(0, 1);
	for(size_t i = 0; i < 1000; i++)
	{
		double* pVec = features1.newRow();
		pVec[0] = prng.uniform();
		pVec[1] = prng.uniform(); // irrelevant attribute
		pVec[2] = prng.uniform();
		labels1.newRow()[0] = 0.3 * pVec[0] + 2.0 * pVec[2] + 5.0;
	}
	GLinearRegressor lr(prng);
	lr.train(features1, labels1);

	// Check some values
	if(lr.beta()->rows() != 1 || lr.beta()->cols() != 3)
		ThrowError("failed");
	if(std::abs(lr.beta()->row(0)[0] - 0.3) > 1e-6)
		ThrowError("failed");
	if(std::abs(lr.beta()->row(0)[1] - 0.0) > 1e-6)
		ThrowError("failed");
	if(std::abs(lr.beta()->row(0)[2] - 2.0) > 1e-6)
		ThrowError("failed");
	if(std::abs(lr.epsilon()[0] - 5.0) > 1e-6)
		ThrowError("failed");

	// Test
	GMatrix features2(0, 3);
	GMatrix labels2(0, 1);
	for(size_t i = 0; i < 1000; i++)
	{
		double* pVec = features2.newRow();
		pVec[0] = prng.uniform();
		pVec[1] = prng.uniform(); // irrelevant attribute
		pVec[2] = prng.uniform();
		labels2.newRow()[0] = 0.3 * pVec[0] + 2.0 * pVec[2] + 5.0;
	}
	double results;
	lr.accuracy(features2, labels2, &results);
	if(results > 0.0005)
		ThrowError("failed");
}

// static
void GLinearRegressor::test()
{
	GRand prng(0);
	GLinearRegressor_linear_test(prng);
	GLinearRegressor lr(prng);
	lr.basicTest(0.77, 0.79);
}
#endif







/*************************
* The following code was derived (with permission) from code by Jean-Pierre Moreau,
* which was posted at: http://jean-pierre.moreau.pagesperso-orange.fr/
* Here is the text of an email exchange with Dr. Moreau:

On 07/28/2010 06:58 AM, Jean-Pierre Moreau wrote:
> I suppose you can use freely the sources given in my website
> on a "as is" basis (with the given reference).
>
> Best regards.
>
>Jean-Pierre Moreau, Paris.
>
-----Message d'origine-----
De : Mike Gashler [mailto:mikegashler@gmail.com] 
Envoyé : mercredi 14 juillet 2010 20:42
À : jpmoreau@wanadoo.fr
Objet : numerical analysis code licensing

Dr. Moreau,

    Thank you for the useful code that you have shared on your web site. 
I would like to know if I may use some of your code in my open source
projects. Your site does not mention any particular license. Since you
posted it on the web, I assume you would like other people to use your code,
but if there is no license, I cannot safely use your code without fear of
legal repercussions. (I would recommend that you might choose the Creative
Commons CC0 license, which essentially puts the code into the public domain
for anyone to use, but any open source license would be welcome.)

I very much appreciate your excellent work,
-Mike Gashler
*/

void simp1(GMatrix& a, int mm, int* ll, int nll, int iabf, int* kp, double* bmax);
void simp2(GMatrix& a, int m, int n, int* l2, int nl2, int* ip, int kp, double* q1);
void simp3(GMatrix& a, int i1, int k1, int ip, int kp);

void simplx(GMatrix& a, int m, int n, int m1, int m2, int* icase, int* izrov, int* iposv)
{
	int m3 = m - (m1 + m2);
	int i, ip, ir, is, k, kh, kp, m12, nl1, nl2;
	GTEMPBUF(int, l1, n + 2);
	GTEMPBUF(int, l2, m + 2);
	GTEMPBUF(int, l3, m + 2);
	double bmax, q1, EPS = 1e-6; // EPS is the absolute precision, which should be adjusted to the scale of your variables.
	nl1 = n;
	for(k = 1; k<=n; k++)
	{
		l1[k] = k;     //Initialize index list of columns admissible for exchange.
		izrov[k] = k;  //Initially make all variables right-hand.
	}
	nl2 = m;
	for(i = 1; i <= m; i++)
	{
		if (a[i + 1][1] < 0.0)
		{
			printf(" Bad input tableau in simplx, Constants bi must be nonnegative.\n");
			return;
		}
		l2[i] = i;
		iposv[i] = n + i;
		// ------------------------------------------------------------------------------------------------
		// Initial left-hand variables. m1 type constraints are represented by having their slackv ariable
		// initially left-hand, with no artificial variable. m2 type constraints have their slack
		// variable initially left-hand, with a minus sign, and their artificial variable handled implicitly
		// during their first exchange. m3 type constraints have their artificial variable initially
		// left-hand.
		// ------------------------------------------------------------------------------------------------
	}
	for(i = 1; i <= m2; i++)
		l3[i] = 1;
	ir = 0;
	if(m2 + m3 == 0)
		goto e30; //The origin is a feasible starting solution. Go to phase two.
	ir = 1;
	for(k = 1; k <= n + 1; k++)
	{ //Compute the auxiliary objective function.
		q1 = 0.0;
		for(i = m1 + 1; i <= m; i++)
			q1 += a[i + 1][k];
		a[m + 2][k] = -q1;
	}
e10:
	simp1(a,m+1,l1,nl1,0,&kp,&bmax);    //Find max. coeff. of auxiliary objective fn
	if(bmax <= EPS && a[m + 2][1] < -EPS)
	{
		*icase=-1;    //Auxiliary objective function is still negative and cant be improved,
		return;       //hence no feasible solution exists.
	}
	else if (bmax <= EPS && a[m + 2][1] <= EPS)
	{
		//Auxiliary objective function is zero and cant be improved; we have a feasible starting vector.
		//Clean out the artificial variables corresponding to any remaining equality constraints by
		//goto 1s and then move on to phase two by goto 30.
		m12 = m1 + m2 + 1;
		if(m12 <= m)
			for (ip=m12; ip<=m; ip++)
				if(iposv[ip] == ip+n)
				{   //Found an artificial variable for an equalityconstraint.
					simp1(a,ip,l1,nl1,1,&kp,&bmax);
					if(bmax > EPS)
						goto e1; //Exchange with column corresponding to maximum
				} //pivot element in row.
		ir=0;
		m12=m12-1;
		if (m1+1 > m12) goto e30; 
		for (i=m1+1; i<=m1+m2; i++)     //Change sign of row for any m2 constraints
		if(l3[i-m1] == 1)             //still present from the initial basis.
		for (k=1; k<=n+1; k++) 
			a[i + 1][k] *= -1.0; 
		goto e30;                       //Go to phase two.
	}
	simp2(a,m,n,l2,nl2,&ip,kp,&q1); //Locate a pivot element (phase one).

	if(ip == 0)
	{                         //Maximum of auxiliary objective function is
		*icase=-1;                          //unbounded, so no feasible solution exists
		return; 
	} 
e1:
	simp3(a, m + 1, n, ip, kp);
	//Exchange a left- and a right-hand variable (phase one), then update lists. 
	if(iposv[ip] >= n + m1 + m2 + 1)
	{ //Exchanged out an artificial variable for an equality constraint. Make sure it stays out by removing it from the l1 list.
		for (k=1; k<=nl1; k++)
			if(l1[k] == kp)
				break;
		nl1 = nl1 - 1;
		for(is = k; is <= nl1; is++)
			l1[is] = l1[is+1];
	}
	else
	{
		if(iposv[ip] < n+m1+1) goto e20;
		kh=iposv[ip]-m1-n; 
		if(l3[kh] == 0) goto e20;  //Exchanged out an m2 type constraint. 
		l3[kh]=0;                  //If it's the first time, correct the pivot column 
					//or the minus sign and the implicit 
					//artificial variable. 
	}
	a[m + 2][kp + 1] += 1.0;
	for (i=1; i<=m+2; i++)
		a[i][kp + 1] *= -1.0;
e20:
	is=izrov[kp];             //Update lists of left- and right-hand variables. 
	izrov[kp]=iposv[ip];
	iposv[ip]=is;
	if (ir != 0) goto e10;       //if still in phase one, go back to 10. 

	//End of phase one code for finding an initial feasible solution. Now, in phase two, optimize it. 
e30:
	simp1(a,0,l1,nl1,0,&kp,&bmax); //Test the z-row for doneness.
	if(bmax <= EPS)
	{          //Done. Solution found. Return with the good news.
		*icase=0;
		return;
	}
	simp2(a,m,n,l2,nl2,&ip,kp,&q1);   //Locate a pivot element (phase two).
	if(ip == 0)
	{             //Objective function is unbounded. Report and return.
		*icase=1;
		return;
	} 
	simp3(a,m,n,ip,kp);       //Exchange a left- and a right-hand variable (phase two),
	goto e20;                 //update lists of left- and right-hand variables and
}                           //return for another iteration.


void simp1(GMatrix& a, int mm, int* ll, int nll, int iabf, int* kp, double* bmax)
{
//Determines the maximum of those elements whose index is contained in the supplied list
//ll, either with or without taking the absolute value, as flagged by iabf. 
	int k;
	double test;
	*kp=ll[1];
	*bmax=a[mm + 1][*kp + 1];
	if (nll < 2) return;
	for (k=2; k<=nll; k++)
	{
		if(iabf == 0)
			test=a[mm + 1][ll[k]+1]-(*bmax);
		else
			test=fabs(a[mm + 1][ll[k] + 1]) - fabs(*bmax);
		if(test > 0.0)
		{ 
			*bmax=a[mm + 1][ll[k]+1];
			*kp=ll[k];
		}
	}
	return;
}

void simp2(GMatrix& a, int m, int n, int* l2, int nl2, int* ip, int kp, double* q1)
{
	double EPS=1e-6;
	//Locate a pivot element, taking degeneracy into account.
	int i,ii,k;
	double q;
	double q0 = 0.0;
	double qp = 0.0;
	*ip=0;
	if(nl2 < 1)
		return;
	for (i=1; i<=nl2; i++)
		if (a[i+1][kp+1] < -EPS)
			goto e2;
	return;  //No possible pivots. Return with message.
e2:
	*q1 = -a[l2[i] + 1][1] / a[l2[i] + 1][kp + 1];
	*ip=l2[i];
	if (i+1 > nl2)
		return;
	for (i=i+1; i<=nl2; i++)
	{
		ii=l2[i];
		if(a[ii+1][kp+1] < -EPS)
		{
			q=-a[ii+1][1]/a[ii+1][kp+1];
			if (q <  *q1)
			{
				*ip=ii;
				*q1=q;
			}
			else if (q == *q1)
			{  //We have a degeneracy.
				for (k=1; k<=n; k++)
				{
					qp=-a[*ip+1][k+1]/a[*ip+1][kp+1];
					q0=-a[ii+1][k+1]/a[ii+1][kp+1];
					if (q0 != qp)
						goto e6;
				}
e6:
				if (q0 < qp)
					*ip=ii;
			}
		}
	}
	return;
}

void simp3(GMatrix& a, int i1, int k1, int ip, int kp)
{
	int ii,kk;
	double piv;
	piv=1.0/a[ip+1][kp+1];
	if (i1 >= 0)
		for(ii = 1; ii <= i1 + 1; ii++)
			if (ii-1 != ip)
			{
				a[ii][kp+1] *= piv;
				for (kk=1; kk<=k1+1; kk++)
					if (kk-1 != kp)
						a[ii][kk] -= a[ip+1][kk]*a[ii][kp+1];
			}
	for (kk=1; kk<=k1+1; kk++)
		if(kk-1 !=  kp) a[ip+1][kk] = -a[ip+1][kk]*piv;
			a[ip+1][kp+1]=piv;
	return;
}

/*
* End of code derived from Dr. Moreau's numerical analysis code
**************************/




// static
bool GLinearProgramming::simplexMethod(GMatrix* pA, const double* pB, int leConstraints, int geConstraints, const double* pC, double* pOutX)
{
	// Set up the matrix in the expected form
	if((size_t)leConstraints + (size_t)geConstraints > pA->rows())
		ThrowError("The number of constraints must be >= leConstraints + geConstraints");
	GMatrix aa(pA->rows() + 3, pA->cols() + 2);
	aa.setAll(0.0);
	aa[1][1] = 0.0;
	GVec::copy(aa.row(1) + 2, pC, pA->cols());
	for(size_t i = 1; i <= pA->rows(); i++)
	{
		GVec::copy(aa.row(i + 1) + 2, pA->row(i - 1), pA->cols());
		GVec::multiply(aa.row(i + 1) + 2, -1.0, pA->cols());
		aa[i + 1][1] = pB[i - 1];
	}

	// Solve it
	int icase;
	GTEMPBUF(int, iposv, aa.rows());
	GTEMPBUF(int, izrov, aa.cols());
	simplx(aa, (int)pA->rows(), (int)pA->cols(), leConstraints, geConstraints, &icase, izrov, iposv);

	// Extract the results
	if(icase)
		return false; // No solution. (icase gives an error code)
	GVec::setAll(pOutX, 0.0, pA->cols());
	for(size_t i = 1; i <= pA->rows(); i++)
	{
		int index = iposv[i];
		if(index >= 1 && index <= (int)pA->cols())
			pOutX[index - 1] = aa[i + 1][1];
	}
	return true;
}


#ifndef NO_TEST_CODE
// static
void GLinearProgramming::test()
{
	GMatrix a(4, 4);
	a[0][0] = 1.0; a[0][1] = 0.0; a[0][2] = 2.0; a[0][3] = 0.0;
	a[1][0] = 0.0; a[1][1] = 2.0; a[1][2] = 0.0; a[1][3] = -7.0;
	a[2][0] = 0.0; a[2][1] = 1.0; a[2][2] = -1.0; a[2][3] = 2.0;
	a[3][0] = 1.0; a[3][1] = 1.0; a[3][2] = 1.0; a[3][3] = 1.0;
	double b[4]; b[0] = 740.0; b[1] = 0.0; b[2] = 0.5; b[3] = 9.0;
	double c[4]; c[0] = 1.0; c[1] = 1.0; c[2] = 3.0; c[3] = -0.5;
	double x[4];
	if(!GLinearProgramming::simplexMethod(&a, b, 2, 1, c, x))
		ThrowError("failed to find a solution");
	if(std::abs(0.0 - x[0]) > 1e-6)
		ThrowError("failed");
	if(std::abs(3.325 - x[1]) > 1e-6)
		ThrowError("failed");
	if(std::abs(4.725 - x[2]) > 1e-6)
		ThrowError("failed");
	if(std::abs(0.95 - x[3]) > 1e-6)
		ThrowError("failed");
}
#endif
