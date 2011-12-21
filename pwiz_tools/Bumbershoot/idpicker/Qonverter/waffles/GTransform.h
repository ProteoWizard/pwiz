/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GTRANSFORM_H__
#define __GTRANSFORM_H__

#include "GLearner.h"

namespace GClasses {

class GActivationFunction;

/// This is the base class of algorithms that transform data without supervision
class GTransform
{
public:
	GTransform();
	GTransform(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GTransform();

	/// Applies the transformation to pIn and returns the results. For
	/// transformations with an internal model (including all transforms
	/// that inherit from GIncrementalTransform), this is equivalent to calling
	/// train, and then calling transformBatch.
	virtual GMatrix* doit(GMatrix& in) = 0;

protected:
	/// Child classes should use this in their implementation of serialize
	GDomNode* baseDomNode(GDom* pDoc, const char* szClassName);
};




/// This is the base class of algorithms that can transform data
/// one row at a time without supervision.
class GIncrementalTransform : public GTransform
{
protected:
	sp_relation m_pRelationBefore;
	sp_relation m_pRelationAfter;
	double* m_pInnerBuf;

public:
	GIncrementalTransform() : GTransform(), m_pInnerBuf(NULL) {}
	GIncrementalTransform(GDomNode* pNode, GLearnerLoader& ll) : GTransform(pNode, ll), m_pInnerBuf(NULL) {}
	virtual ~GIncrementalTransform();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc) = 0;

	/// Trains the transform on the data in pData.
	///
	/// For those implementing subclasses, must set
	/// m_pRelationBefore and m_pRelationAfter
	virtual void train(GMatrix& data) = 0;

	/// Returns a relation object describing the data before it is
	/// transformed
	///
	/// train must be called before this method is used
	sp_relation& before() { return m_pRelationBefore; }

	/// Returns a relation object describing the data after it is
	/// transformed
	///
	/// train must be called before this method is used
	sp_relation& after() { return m_pRelationAfter; }

	/// pIn is the source row. pOut is a buffer that will hold the
	/// transformed row.  train must be called before this method
	/// is used
	virtual void transform(const double* pIn, double* pOut) = 0;

	/// This calls Train with in, then transforms in and returns
	/// the results.  The caller is responsible for deleting the
	/// new matrix.
	virtual GMatrix* doit(GMatrix& in);

	/// This assumes that train has already been called, and
	/// transforms all the rows in in returning the resulting
	/// matrix.  The caller is responsible for deleting the new
	/// matrix.
	virtual GMatrix* transformBatch(GMatrix& in);

	/// Returns a buffer of sufficient size to store an inner
	/// (transformed) vector.  The caller does not have to delete
	/// the buffer, but the same buffer will be returned each
	/// time.
	double* innerBuf();
};




/// This is the base class of algorithms that can transform data
/// one row at a time without supervision, and can (un)transform
/// a row back to its original form if necessary.
class GTwoWayIncrementalTransform : public GIncrementalTransform
{
public:
	GTwoWayIncrementalTransform() : GIncrementalTransform() {}
	GTwoWayIncrementalTransform(GDomNode* pNode, GLearnerLoader& ll) : GIncrementalTransform(pNode, ll) {}
	virtual ~GTwoWayIncrementalTransform() {}

	/// pIn is a previously transformed row, and pOut is a buffer that will hold the untransformed row.
	/// train must be called before this method is used
	virtual void untransform(const double* pIn, double* pOut) = 0;

	/// Similar to untransform, except it produces a distribution instead of just a vector.
	/// This method may not be implemented in all classes, so it may throw an exception.
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut) = 0;

	/// This assumes train was previously called, and untransforms all the rows in pIn and returns the results.
	virtual GMatrix* untransformBatch(GMatrix& in);
};





/// This wraps two two-way-incremental-transoforms to form a single combination transform
class GTwoWayTransformChainer : public GTwoWayIncrementalTransform
{
protected:
	GTwoWayIncrementalTransform* m_pFirst;
	GTwoWayIncrementalTransform* m_pSecond;

public:
	/// General-purpose constructor
	GTwoWayTransformChainer(GTwoWayIncrementalTransform* pFirst, GTwoWayIncrementalTransform* pSecond);

	/// Deserializing constructor
	GTwoWayTransformChainer(GDomNode* pNode, GLearnerLoader& ll);
	virtual ~GTwoWayTransformChainer();

	/// See the comment for GTwoWayIncrementalTransform::serialize
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GTwoWayIncrementalTransform::train
	virtual void train(GMatrix& data);

	/// See the comment for GTwoWayIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransform
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);
};






/// Principal Component Analysis. (Computes the principal components about
/// the mean of the data when you call train. The transformed (reduced-dimensional)
/// data will have a mean about the origin.)
class GPCA : public GTwoWayIncrementalTransform
{
protected:
	size_t m_targetDims;
	GMatrix* m_pBasisVectors;
	double* m_pEigVals;
	bool m_aboutOrigin;
	GRand* m_pRand;

public:
	GPCA(size_t targetDims, GRand* pRand);

	/// Load from a DOM.
	GPCA(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GPCA();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// Specify to compute the eigenvalues during training. This
	/// method must be called before train is called.
	void computeEigVals();

	/// Specify to compute the principal components about the origin (instead
	/// of computing them about the mean).
	void aboutOrigin() { m_aboutOrigin = true; }

	/// Returns the eigenvalues. Returns NULL if computeEigVals was not called.
	double* eigVals() { return m_pEigVals; }

	/// Returns the number of principal components that it will find.
	size_t targetDims() { return m_targetDims; }

	/// Returns the mean of the data used to train this transform
	double* mean() { return m_pBasisVectors->row(0); }

	/// Returns the i'th principal component vector
	double* basis(size_t i) { return m_pBasisVectors->row(i + 1); }

	/// Returns a dataset where the first row is the centroid, and the remaining
	/// rows are the principal component vectors in order of decreasing eigenvalue.
	GMatrix* components() { return m_pBasisVectors; }

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);

	/// See the comment for GIncrementalTransform::transform.
	/// Projects the specified point into fewer dimensions.
	virtual void transform(const double* pIn, double* pOut);

	/// Computes a (lossy) high-dimensional point that corresponds with the
	/// specified low-dimensional coordinates.
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);
};


/// Principle Component Analysis without the projection. It only rotates
/// axes to align with the first few principal components.
class GPCARotateOnly
{
public:
	/// This rotates the data to align the first nComponents axes with the same
	/// number of principle components.
	static GMatrix* transform(size_t nDims, size_t nOutputs, GMatrix* pData, size_t nComponents, GRand* pRand);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE
};


/// Just generates Gaussian noise
class GNoiseGenerator : public GIncrementalTransform
{
protected:
	GRand* m_pRand;
	double m_mean, m_deviation;

public:
	GNoiseGenerator(GRand* pRand);

	/// Load from a DOM.
	GNoiseGenerator(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GNoiseGenerator();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);

	virtual sp_relation& relationAfter() { return m_pRelationBefore; }
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);

	void setMeanAndDeviation(double m, double d) { m_mean = m; m_deviation = d; }
};



/// Generates data by computing the product of each pair of
/// attributes. This is useful for augmenting data.
class GPairProduct : public GIncrementalTransform
{
protected:
	size_t m_maxDims;

public:
	GPairProduct(size_t nMaxDims);

	/// Load from a DOM.
	GPairProduct(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GPairProduct();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);
};



/// Generates subsets of data that contain only the most relevant features for predicting the labels.
/// The train method of this class produces a ranked ordering of the feature attributes by training
/// a single-layer neural network, and deselecting the weakest attribute until all attributes have been
/// deselected. The transform method uses only the highest-ranked attributes.
class GAttributeSelector : public GIncrementalTransform
{
protected:
	size_t m_labelDims;
	size_t m_targetFeatures;
	std::vector<size_t> m_ranks;
	GRand* m_pRand;

public:
	GAttributeSelector(size_t labelDims, size_t targetFeatures, GRand* pRand) : GIncrementalTransform(), m_labelDims(labelDims), m_targetFeatures(targetFeatures), m_pRand(pRand)
	{
	}

	GAttributeSelector(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GAttributeSelector()
	{
	}

#ifndef NO_TEST_CODE
	static void test();
#endif

	virtual GDomNode* serialize(GDom* pDoc);
	
	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);

	/// Specifies the number of features to select
	void setTargetFeatures(size_t n);

	/// Returns a list of attributes in ranked-order. Most important attributes are first. Weakest attributes are last.
	/// (The results are undefined until after train is called.)
	std::vector<size_t>& ranks() { return m_ranks; }
};



/// This is sort-of the opposite of discretize. It converts each nominal attribute to a categorical
/// distribution by representing each value using the corresponding row of the identity matrix. For
/// example, if a certain nominal attribute has 4 possible values, then a value of 3 would be encoded
/// as the vector 0 0 1 0. When predictions are converted back to nominal values, the mode of the
/// categorical distribution is used as the predicted value. (This is similar to Weka's
/// NominalToBinaryFilter.)
class GNominalToCat : public GTwoWayIncrementalTransform
{
protected:
	size_t m_valueCap;
	GRand* m_pRand;
	std::vector<size_t> m_ranks;
	bool m_preserveUnknowns;

public:
	GNominalToCat(size_t valueCap = 12);

	/// Load from a DOM.
	GNominalToCat(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GNominalToCat();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);

	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransform
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);

	/// Makes a mapping from the post-transform attribute indexes to the pre-transform attribute indexes
	void reverseAttrMap(std::vector<size_t>& rmap);

	/// Specify to preserve unknown values. That is, an unknown nominal value will be
	/// converted to a distribution of all unknown real values.
	void preserveUnknowns() { m_preserveUnknowns = true; }

protected:
	void init(sp_relation& pRelationBefore);
};



/// This transform scales and shifts continuous values
/// to make them fall within a specified range.
class GNormalize : public GTwoWayIncrementalTransform
{
protected:
	double m_min, m_max;
	double* m_pMins;
	double* m_pRanges;

public:
	/// min and max specify the target range. (The input domain is determined
	/// automatically when train is called.)
	GNormalize(double min = 0.0, double max = 1.0);

	/// Load from a DOM.
	GNormalize(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GNormalize();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);
	
	/// See the comment for GTwoWayIncrementalTransform::untransform
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);

	/// Specify the input min and range values for each attribute
	void setMinsAndRanges(sp_relation& pRel, const double* pMins, const double* pRanges);
};



/// This transform uses buckets to convert continuous data into discrete data.
/// It is common to use GFilter to combine this with your favorite modeler
/// (which only supports discrete values) to create a modeler that can also support
/// continuous values as well.
class GDiscretize : public GTwoWayIncrementalTransform
{
protected:
	size_t m_bucketsIn, m_bucketsOut;
	double* m_pMins;
	double* m_pRanges;

public:
	/// if buckets is less than 0, then it will use the floor of the square root of the number of rows in the data
	GDiscretize(size_t buckets = (size_t)-1);

	/// Load from a DOM.
	GDiscretize(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GDiscretize();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);
	
	/// See the comment for GTwoWayIncrementalTransform::untransform
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);
};




class GImputeMissingVals : public GTwoWayIncrementalTransform
{
protected:
	GCollaborativeFilter* m_pCF;
	GNominalToCat* m_pNTC;
	GRand& m_rand;
	GMatrix* m_pLabels;
	GMatrix* m_pBatch;

public:
	/// General-purpose constructor
	GImputeMissingVals(GRand& rand);

	/// Deserializing constructor
	GImputeMissingVals(GDomNode* pNode, GLearnerLoader& ll);

	virtual ~GImputeMissingVals();

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// See the comment for GIncrementalTransform::train
	virtual void train(GMatrix& data);
	
	/// See the comment for GIncrementalTransform::transform
	virtual void transform(const double* pIn, double* pOut);
	
	/// See the comment for GTwoWayIncrementalTransform::untransform
	virtual void untransform(const double* pIn, double* pOut);

	/// See the comment for GTwoWayIncrementalTransform::untransformToDistribution
	virtual void untransformToDistribution(const double* pIn, GPrediction* pOut);

	/// Sets the collaborative filter used to impute missing values. Takes
	/// ownership of pCF. If no collaborative filter is set, the default is to use
	/// matrix factorization with some typical parameters.
	void setCollaborativeFilter(GCollaborativeFilter* pCF);

	/// Specify a label matrix that should be appended with the training data.
	/// This object will not delete pLabels. It is expected that pLabels will remain
	/// valid at least until after train is next called.
	void setLabels(GMatrix* pLabels);

	/// Unlike most other transforms, this one assumes that the matrix passed
	/// to this method is the same that was used to train it. (This assumption
	/// is necessary in order to utilize the additional label information that
	/// may be available at training time, which can be important for imputation.)
	virtual GMatrix* transformBatch(GMatrix& in);
};

} // namespace GClasses

#endif // __GTRANSFORM_H__

