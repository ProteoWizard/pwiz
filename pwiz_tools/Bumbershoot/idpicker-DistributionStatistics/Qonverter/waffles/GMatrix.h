/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GMATRIX_H__
#define __GMATRIX_H__

#include "GError.h"
#include <vector>
#include <string>
#include <algorithm>
#include <iostream>
#include "GHolders.h"

namespace GClasses {

#define UNKNOWN_REAL_VALUE -1e308

// Why do we need a different value for unknown discrete values? Because it's
// common to store discrete values in an integer. Integers can't store -1e308,
// and we can't use -1 for unknown reals b/c it's not an insane value.
#define UNKNOWN_DISCRETE_VALUE -1


class GMatrix;
class GPrediction;
class GRand;
class GHeap;
class GDom;
class GDomNode;
class GTokenizer;
class GDistanceMetric;


/// Holds the metadata for a dataset, including which attributes
/// are continuous or nominal, and how many values each nominal
/// attribute supports.
class GRelation
{
public:
	enum RelationType
	{
		UNIFORM,
		MIXED,
		ARFF
	};

	GRelation() {}
	virtual ~GRelation() {}

	/// Returns the type of relation
	virtual RelationType type() = 0;

	/// Marshal this object into a DOM, which can then be converted to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc) = 0;

	/// Returns the number of attributes (columns)
	virtual size_t size() = 0;

	/// Returns the number of values in the specified attribute. (Returns 0 for
	/// continuous attributes.)
	virtual size_t valueCount(size_t nAttr) = 0;

	/// Returns true of all of the attributes in the specified range are continuous
	virtual bool areContinuous(size_t first, size_t count) = 0;

	/// Returns true of all of the attributes in the specified range are nominal
	virtual bool areNominal(size_t first, size_t count) = 0;

	/// Makes a deep copy of this relation
	virtual GRelation* clone() = 0;

	/// Makes a deep copy of the specified subset of this relation
	virtual GRelation* cloneSub(size_t start, size_t count) = 0;

	/// Deletes the specified attribute
	virtual void deleteAttribute(size_t index) = 0;

	/// Swaps two attributes
	virtual void swapAttributes(size_t nAttr1, size_t nAttr2) = 0;

	/// Prints as an ARFF file to the specified stream. (pData can be NULL if data is not available)
	void print(std::ostream& stream, GMatrix* pData, size_t precision);

	/// Prints the specified attribute name to a stream
	virtual void printAttrName(std::ostream& stream, size_t column);

	/// Prints the specified value to a stream
	virtual void printAttrValue(std::ostream& stream, size_t column, double value);

	/// Returns true iff this and that have the same number of values for each attribute
	virtual bool isCompatible(GRelation& that);

	/// Print a single row of data in ARFF format
	void printRow(std::ostream& stream, double* pRow, const char* separator);

	/// Counts the size of the corresponding real-space vector
	size_t countRealSpaceDims(size_t nFirstAttr, size_t nAttrCount);

	/// Converts a row (pIn) to a real-space vector (pOut)
	/// (pIn should point to the nFirstAttr'th element, not the first element)
	void toRealSpace(const double* pIn, double* pOut, size_t nFirstAttr, size_t nAttrCount);

	/// Converts a real-space vector (pIn) to a row (pOut)
	/// nFirstAttr and nAttrCount refer to the row indexes
	void fromRealSpace(const double* pIn, double* pOut, size_t nFirstAttr, size_t nAttrCount, GRand* pRand);

	/// Converts a real-space vector (pIn) to an array of predictions (pOut)
	/// nFirstAttr and nAttrCount refer to the prediction indexes
	void fromRealSpace(const double* pIn, GPrediction* pOut, size_t nFirstAttr, size_t nAttrCount);

	/// Load from a DOM.
	static smart_ptr<GRelation> deserialize(GDomNode* pNode);

	/// Saves to a file
	void save(GMatrix* pData, const char* szFilename, size_t precision);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE

 protected:
	/// Returns a copy of aString modified to escape internal instances
	/// of comma, apostrophe, space, percent, back-slash, and
	/// double-quote
	static std::string quote(const std::string aString);
};

typedef smart_ptr<GRelation> sp_relation;


/// A relation with a minimal memory footprint that assumes
/// all attributes are continuous, or all of them are nominal
/// and have the same number of possible values.
class GUniformRelation : public GRelation
{
protected:
	size_t m_attrCount;
	size_t m_valueCount;

public:
	GUniformRelation(size_t attrCount, size_t valueCount = 0)
	: m_attrCount(attrCount), m_valueCount(valueCount)
	{
	}

	GUniformRelation(GDomNode* pNode);

	virtual RelationType type() { return UNIFORM; }
	
	/// Serializes this object
	virtual GDomNode* serialize(GDom* pDoc);
	
	/// Returns the number of attributes (columns)
	virtual size_t size() { return m_attrCount; }
	
	/// Returns the number of values in each nominal attribute (or 0 if the attributes are continuous)
	virtual size_t valueCount(size_t nAttr) { return m_valueCount; }
	
	/// See the comment for GRelation::areContinuous
	virtual bool areContinuous(size_t first, size_t count) { return m_valueCount == 0; }
	
	/// See the comment for GRelation::areNominal
	virtual bool areNominal(size_t first, size_t count) { return m_valueCount != 0; }

	/// Returns a copy of this object
	virtual GRelation* clone() { return new GUniformRelation(m_attrCount, m_valueCount); }

	/// Returns a deep copy of the specified subset of this relation
	virtual GRelation* cloneSub(size_t start, size_t count) { return new GUniformRelation(count, m_valueCount); }

	/// Drop the specified attribute
	virtual void deleteAttribute(size_t index);
	
	/// Swap two attributes
	virtual void swapAttributes(size_t nAttr1, size_t nAttr2) {}

	/// Returns true iff this and that have the same number of values for each attribute
	virtual bool isCompatible(GRelation& that);
};



class GMixedRelation : public GRelation
{
protected:
	std::vector<size_t> m_valueCounts;

public:
	/// Makes an empty relation
	GMixedRelation();

	/// Construct a mixed relation. attrValues specifies the number of
	/// nominal values in each attribute (column), or 0 for continuous
	/// attributes.
	GMixedRelation(std::vector<size_t>& attrValues);

	/// Loads from a DOM.
	GMixedRelation(GDomNode* pNode);

	/// Makes a copy of pCopyMe
	GMixedRelation(GRelation* pCopyMe);

	/// Makes a copy of the specified range of pCopyMe
	GMixedRelation(GRelation* pCopyMe, size_t firstAttr, size_t attrCount);

	virtual ~GMixedRelation();

	virtual RelationType type() { return MIXED; }

	/// Marshalls this object to a DOM, which can be saved to a variety of serial formats.
	virtual GDomNode* serialize(GDom* pDoc);

	/// Makes a deep copy of this relation
	virtual GRelation* clone();

	/// Makes a deep copy of the specified subset of this relation
	virtual GRelation* cloneSub(size_t start, size_t count);

	/// Deletes all the attributes
	virtual void flush();

	/// If nValues is zero, adds a real attribute. If nValues is > 0, adds
	/// an attribute with "nValues" nominal values
	void addAttr(size_t nValues);

	/// Adds "attrCount" new attributes, each with "valueCount" values. (Use valueCount=0
	/// for continuous attributes.)
	void addAttrs(size_t attrCount, size_t valueCount);

	/// Copies the specified attributes and adds them to this relation.
	/// If attrCount < 0, then it will copy all attributes from firstAttr to the end.
	void addAttrs(GRelation* pCopyMe, size_t firstAttr = 0, size_t attrCount = (size_t)-1);

	/// Flushes this relation and then copies all of the attributes from pCopyMe
	void copy(GRelation* pCopyMe);

	/// Adds a copy of the specified attribute to this relation
	virtual void copyAttr(GRelation* pThat, size_t nAttr);

	/// Returns the total number of attributes in this relation
	virtual size_t size()
	{
		return m_valueCounts.size();
	}

	/// Returns the number of nominal values in the specified attribute
	virtual size_t valueCount(size_t nAttr)
	{
		return m_valueCounts[nAttr];
	}

	/// Sets the number of values for this attribute
	virtual void setAttrValueCount(size_t nAttr, size_t nValues);

	virtual bool areContinuous(size_t first, size_t count);

	virtual bool areNominal(size_t first, size_t count);

	/// Swaps two columns
	virtual void swapAttributes(size_t nAttr1, size_t nAttr2);

	/// Deletes an attribute.
	virtual void deleteAttribute(size_t nAttr);
};


class GArffAttribute
{
public:
	std::string m_name;
	std::vector<std::string> m_values;
};


/// ARFF = Attribute-Relation File Format. This stores richer information
/// than GRelation. This includes a name, a name for each attribute, and
/// names for each supported nominal value.
class GArffRelation : public GMixedRelation
{
friend class GMatrix;
protected:
	std::string m_name;
	std::vector<GArffAttribute> m_attrs;

public:
	GArffRelation();
	virtual ~GArffRelation();

	virtual RelationType type() { return ARFF; }

	/// Returns a deep copy of this object
	virtual GRelation* clone();

	/// Makes a deep copy of the specified subset of this relation
	virtual GRelation* cloneSub(size_t start, size_t count);

	/// Deletes all the attributes
	virtual void flush();

	/// Prints the specified attribute name to a stream
	virtual void printAttrName(std::ostream& stream, size_t column);

	/// Prints the specified value to a stream
	virtual void printAttrValue(std::ostream& stream, size_t column, double value);

	/// Returns true iff the attributes in both relations have the same names, the same number
	/// of values, and the names of those values all match. (Empty strings are considered to match
	/// everything.)
	virtual bool isCompatible(GRelation& that);

	/// Adds a new attribute (column) to the relation
	void addAttribute(const char* szName, size_t nValues, std::vector<const char*>* pValues);

	/// Adds a copy of the specified attribute to this relation
	virtual void copyAttr(GRelation* pThat, size_t nAttr);

	/// Returns the name of the relation
	const char* name() { return m_name.c_str(); }

	/// Sets the name of this relation
	void setName(const char* szName);

	/// Returns the name of the specified attribute
	const char* attrName(size_t nAttr);

	/// Adds a new possible value to a nominal attribute. Returns the numerical form of the new value.
	int addAttrValue(size_t nAttr, const char* szValue);

	/// Sets the number of values for the specified attribute
	virtual void setAttrValueCount(size_t nAttr, size_t nValues);

	/// Swaps two columns
	virtual void swapAttributes(size_t nAttr1, size_t nAttr2);

	/// Deletes an attribute
	virtual void deleteAttribute(size_t nAttr);

	/// Returns the nominal index for the specified attribute with the given value
	int findEnumeratedValue(size_t nAttr, const char* szValue);

	/// Parses a value
	double parseValue(size_t attr, const char* val);

	/// Parses the meta-data for an attribute
	void parseAttribute(GTokenizer& tok);

protected:
	/// takes ownership of ppValues
	void addAttributeInternal(const char* pName, size_t nameLen, size_t valueCount);
};


/// Represents a matrix or a database table. Elements can be discrete or continuous.
/// References a GRelation object, which stores the meta-information about each column.
class GMatrix
{
protected:
	sp_relation m_pRelation;
	GHeap* m_pHeap;
	std::vector<double*> m_rows;

public:
	/// Construct a rows x cols matrix. All elements of the matrix are assumed to
	/// be continuous. (It is okay to initially set rows to 0 and later call newRow
	/// to add each row. Adding columns later, however, is not very computationally
	/// efficient.)
	GMatrix(size_t rows, size_t cols, GHeap* pHeap = NULL);

	/// Construct a matrix with a mixed relation. That is, one with some continuous attributes
	/// (columns), and some nominal attributes (columns). attrValues specifies the number of
	/// nominal values suppored in each attribute (column), or 0 for a continuous attribute.
	/// Initially, this matrix will have 0 rows, but you can add more rows by calling newRow or newRows.
	GMatrix(std::vector<size_t>& attrValues, GHeap* pHeap = NULL);

	/// pRelation is a smart-pointer to a relation, which specifies
	/// the type of each attribute (column) in the data set.
	/// Initially, this matrix will have 0 rows, but you can add more rows by calling newRow or newRows.
	GMatrix(sp_relation& pRelation, GHeap* pHeap = NULL);

	/// Load from a DOM.
	GMatrix(GDomNode* pNode, GHeap* pHeap = NULL);

	~GMatrix();

	///
	/// Adds a new row to the dataset. (The values in the row are not initialized)
	///
	double* newRow();

	/// Adds "nRows" uninitialized rows to the data set
	void newRows(size_t nRows);

	/// Matrix add. Adds the values in pThat to this. (If transpose
	/// is true, adds the transpose of pThat to this.) Both datasets
	/// must have the same dimensions. Behavior is undefined for nominal columns.
	void add(GMatrix* pThat, bool transpose);

	/// Returns a new dataset that contains a subset of the attributes in this dataset
	GMatrix* attrSubset(size_t firstAttr, size_t attrCount);

	/// This computes the square root of this matrix. (If you take the matrix that this
	/// returns and multiply it by its transpose, you should get the original dataset
	/// again.) Behavior is undefined if there are nominal attributes. If this matrix
	/// is not positive definate, it will throw an exception.
	GMatrix* cholesky();

	/// Makes a deep copy of this dataset
	GMatrix* clone();

	/// Makes a deep copy of the specified rectangular region of this matrix
	GMatrix* cloneSub(size_t rowStart, size_t colStart, size_t rowCount, size_t colCount);

	/// Copies the specified column into pOutVector
	void col(size_t index, double* pOutVector);

	/// Returns the number of columns in the dataset
	size_t cols() const { return m_pRelation->size(); }

	/// Copies all the data from pThat. (Just references the same relation)
	void copy(GMatrix* pThat);

	/// Copies the specified block of columns from pSource to this dataset. pSource must have
	/// the same number of rows as this dataset.
	void copyColumns(size_t nDestStartColumn, GMatrix* pSource, size_t nSourceStartColumn, size_t nColumnCount);

	/// Adds a copy of the row to the data set
	void copyRow(const double* pRow);

	/// Computes the determinant of this matrix
	double determinant();

	/// Computes the eigenvalue that corresponds to the specified eigenvector
	/// of this matrix
	double eigenValue(const double* pEigenVector);

	/// Computes the eigenvector that corresponds to the specified eigenvalue of
	/// this matrix. Note that this method trashes this matrix, so
	/// make a copy first if you care.
	void eigenVector(double eigenvalue, double* pOutVector);

	/// Computes y in the equation M*y=x (or y=M^(-1)x), where M is this dataset, which
	/// must be a square matrix, and x is pVector as passed in, and y is pVector after
	/// the call. If there are multiple solutions, it finds the one for which all
	/// the variables in the null-space have a value of 1. If there are no solutions,
	/// it returns false. Note that this method trashes this dataset (so make a copy
	/// first if you care).
	bool gaussianElimination(double* pVector);

	/// Returns the heap used to allocate rows for this dataset
	GHeap* heap() { return m_pHeap; }

	/// Performs an in-place LU-decomposition, such that the lower triangle
	/// of this matrix (including the diagonal) specifies L, and the uppoer
	/// triangle of this matrix (not including the diagonal) specifies U,
	/// and all values of U along the diagonal are ones. (The upper triangle of L
	/// and the lower triangle of U are all zeros.)
	void LUDecomposition();

	/// This computes K=kabsch(A,B), such that K is an n-by-n matrix, where n is pA->cols().
	/// K is the optimal orthonormal rotation matrix to align A and B, such that A(K^T)
	/// minimizes sum-squared error with B, and BK minimizes sum-squared error with A.
	/// (This rotates around the origin, so typically you will want to subtract the
	/// centroid from both pA and pB before calling this.)
	static GMatrix* kabsch(GMatrix* pA, GMatrix* pB);

	/// This uses the Kabsch algorithm to rotate and translate pB in order to minimize
	/// RMS with pA. (pA and pB must have the same number of rows and columns.)
	static GMatrix* align(GMatrix* pA, GMatrix* pB);

	/// Loads an ARFF file and returns the data. This will throw an exception if
	/// there's an error.
	static GMatrix* loadArff(const char* szFilename);

	/// Loads a file in CSV format.
	static GMatrix* loadCsv(const char* szFilename, char separator, bool columnNamesInFirstRow, bool tolerant);

	/// Sets this dataset to an identity matrix. (It doesn't change the
	/// number of columns or rows. It just stomps over existing values.)
	void makeIdentity();

	/// If upperToLower is true, copies the upper triangle of this matrix over the lower triangle
	/// If upperToLower is false, copies the lower triangle of this matrix over the upper triangle
	void mirrorTriangle(bool upperToLower);

	/// Merges two datasets side-by-side. The resulting dataset
	/// will contain the attributes of both datasets. Both pSetA
	/// and pSetB (and the resulting dataset) must have the same
	/// number of rows
	static GMatrix* mergeHoriz(GMatrix* pSetA, GMatrix* pSetB);

	/// Steals all the rows from pData and adds them to this set.
	/// (You still have to delete pData.) Both datasets must have
	/// the same number of columns.
	void mergeVert(GMatrix* pData);

	/// Computes nCount eigenvectors and the corresponding eigenvalues using the power method.
	/// (This method is only accurate if a small number of eigenvalues/vectors are needed.)
	/// If mostSignificant is true, the largest eigenvalues are found. If mostSignificant
	/// is false, the smallest eigenvalues are found.
	GMatrix* eigs(size_t nCount, double* pEigenVals, GRand* pRand, bool mostSignificant);

	/// Multiplies every element in the dataset by scalar.
	/// Behavior is undefined for nominal columns.
	void multiply(double scalar);

	/// Multiplies this matrix by the column vector pVectorIn to get
	/// pVectorOut. (If transpose is true, then it multiplies the transpose
	/// of this matrix by pVectorIn to get pVectorOut.) pVectorIn should have
	/// the same number of elements as columns (or rows if transpose is true)
	/// and pVectorOut should have the same number of elements as rows (or
	/// cols, if transpose is true.) Note that if transpose is true, it is the
	/// same as if pVectorIn is a row vector and you multiply it by this matrix
	/// to get pVectorOut.
	void multiply(const double* pVectorIn, double* pVectorOut, bool transpose = false);

	/// Matrix multiply. For convenience, you can also specify that neither, one, or both
	/// of the inputs are virtually transposed prior to the multiplication. (If you want
	/// the results to come out transposed, you can use the equality AB=((B^T)(A^T))^T to
	/// figure out how to specify the parameters.)
	static GMatrix* multiply(GMatrix& a, GMatrix& b, bool transposeA, bool transposeB);

	/// Parses an ARFF file and returns the data. This will throw an exception if
	/// there's an error.
	static GMatrix* parseArff(const char* szFile, size_t nLen);

	/// Imports data from a text file. Determines the meta-data automatically.
	/// Note: This method does not support Mac line-endings. You should first replace all '\r' with '\n' if your data comes from a Mac.
	/// As a special case, if separator is '\0', then it assumes data elements are separated by any number of whitespace characters, that
	/// element values themselves contain no whitespace, and that there are no missing elements. (This is the case when you save a
	/// Matlab matrix to an ascii file.)
	static GMatrix* parseCsv(const char* pFile, size_t len, char separator, bool columnNamesInFirstRow, bool tolerant = false);

	/// Computes the Moore-Penrose pseudoinverse of this matrix (using the SVD method). You
	/// are responsible to delete the matrix this returns.
	GMatrix* pseudoInverse();

	/// Returns a relation object, which holds meta-data about the attributes (columns)
	sp_relation& relation() { return m_pRelation; }

	/// Allocates space for the specified number of patters (to avoid superfluous resizing)
	void reserve(size_t n) { m_rows.reserve(n); }

	/// Returns the number of rows in the dataset
	size_t rows() const { return m_rows.size(); }

	/// Saves the dataset to a file in ARFF format
	void saveArff(const char* szFilename);

	/// Sets the relation for this dataset
	void setRelation(sp_relation& pRelation) { m_pRelation = pRelation; }

	/// Performs SVD on A, where A is this m-by-n matrix.
	/// *ppU will be set to an m-by-m matrix where the columns are the eigenvectors of A(A^T).
	/// *ppDiag will be set to an array of n doubles holding the square roots of the corresponding eigenvalues.
	/// *ppV will be set to an n-by-n matrix where the rows are the eigenvectors of (A^T)A.
	/// You are responsible to delete(*ppU), delete(*ppV), and delete[] *ppDiag.
	void singularValueDecomposition(GMatrix** ppU, double** ppDiag, GMatrix** ppV, bool throwIfNoConverge = false, size_t maxIters = 80);

	/// Matrix subtract. Subtracts the values in pThat from this. (If transpose
	/// is true, subtracts the transpose of pThat from this.) Both datasets
	/// must have the same dimensions. Behavior is undefined for nominal columns.
	void subtract(GMatrix* pThat, bool transpose);

	/// Returns the sum squared difference between this matrix and an identity matrix
	double sumSquaredDiffWithIdentity();

	/// Adds an already-allocated row to this dataset. The row must
	/// be allocated in the same heap that this dataset uses. (There is no way
	/// for this method to verify that, so be careful.)
	void takeRow(double* pRow);

	/// Converts the matrix to reduced row echelon form
	size_t toReducedRowEchelonForm();

	/// Copies all the data from this dataset into pVector. pVector must be
	/// big enough to hold rows() x cols() doubles.
	void toVector(double* pVector);

	/// Marshalls this object to a DOM, which may be saved to a variety of serial formats.
	GDomNode* serialize(GDom* pDoc);

	/// Returns the sum of the diagonal elements
	double trace();

	/// Returns a dataset that is this dataset transposed. (All columns
	/// in the returned dataset will be continuous.)
	GMatrix* transpose();

	/// Copies the data from pVector over this dataset. nRows specifies the number
	/// of rows of data in pVector.
	void fromVector(const double* pVector, size_t nRows);

	/// Returns a pointer to the specified row
	inline double* row(size_t index) { return m_rows[index]; }

	/// Returns a pointer to the specified row
	inline double* operator [](size_t index) { return m_rows[index]; }

	/// Returns a const pointer to the specified row
	inline const double* row(size_t index) const { return m_rows[index]; }

	/// Returns a const pointer to the specified row
	inline const double* operator [](size_t index) const { 
	  return m_rows[index]; }

	/// Sets all elements in this dataset to the specified value
	void setAll(double val);

	/// Copies pVector over the specified column
	void setCol(size_t index, const double* pVector);

	/// Swaps the two specified rows
	void swapRows(size_t a, size_t b);

	/// Swaps two columns
	void swapColumns(size_t nAttr1, size_t nAttr2);

	/// Deletes a column
	void deleteColumn(size_t index);

	/// Swaps the specified row with the last row, and then releases it from the dataset.
	/// If this dataset does not have its own heap, then you must delete the row this returns
	double* releaseRow(size_t index);

	/// Swaps the specified row with the last row, and then deletes it.
	void deleteRow(size_t index);

	/// Releases the specified row from the dataset and shifts everything after it up one slot.
	/// If this dataset does not have its own heap, then you must delete the row this returns
	double* releaseRowPreserveOrder(size_t index);

	/// Deletes the specified row and shifts everything after it up one slot
	void deleteRowPreserveOrder(size_t index);

	/// Replaces any occurrences of NAN in the matrix with the corresponding values from
	/// an identity matrix.
	void fixNans();

	/// Deletes all the data
	void flush();

	/// Abandons (leaks) all the rows of data
	void releaseAllRows();

	/// Randomizes the order of the rows. If pExtension is non-NULL, then
	/// it will also be shuffled such that corresponding rows are preserved.
	void shuffle(GRand& rand, GMatrix* pExtension = NULL);

	/// Shuffles the order of the rows. Also shuffles the rows in "other" in
	/// the same way, such that corresponding rows are preserved.
	void shuffle2(GRand& rand, GMatrix& other);

	/// This is an inferior way to shuffle the data
	void shuffleLikeCards();

	/// Sorts the data from smallest to largest in the specified dimension
	void sort(size_t nDimension);

	/// This partially sorts the specified column, such that the specified row
	/// will contain the same row as if it were fully sorted, and previous
	/// rows will contain a value <= to it in that column, and later rows
	/// will contain a value >= to it in that column. Unlike sort, which
	/// has O(m*log(m)) complexity, this method has O(m) complexity. This might
	/// be useful, for example, for efficiently finding the row with a median
	/// value in some attribute, or for separating data by a threshold in
	/// some value.
	void sortPartial(size_t row, size_t col);

	/// Reverses the row order
	void reverseRows();

	/// Sorts rows according to the specified compare function. (Return true
	/// to indicate thate the first row comes before the second row.)
	template<typename CompareFunc>
	void sort(CompareFunc& pComparator)
	{
		std::sort(m_rows.begin(), m_rows.end(), pComparator);
	}

	/// Splits this set of data into two sets. Values greater-than-or-equal-to
	/// dPivot stay in this data set. Values less than dPivot go into pLessThanPivot
	/// If pExtensionA is non-NULL, then it will also split pExtensionA
	/// such that corresponding rows are preserved.
	void splitByPivot(GMatrix* pGreaterOrEqual, size_t nAttribute, double dPivot, GMatrix* pExtensionA = NULL, GMatrix* pExtensionB = NULL);

	/// Moves all rows with the specified value in the specified attribute
	/// into pSingleClass
	/// If pExtensionA is non-NULL, then it will also split pExtensionA
	/// such that corresponding rows are preserved.
	void splitByNominalValue(GMatrix* pSingleClass, size_t nAttr, int nValue, GMatrix* pExtensionA = NULL, GMatrix* pExtensionB = NULL);

	/// Removes the last nOtherRows rows from this data set and
	/// puts them in pOtherData
	void splitBySize(GMatrix* pOtherData, size_t nOtherRows);

	/// Measures the entropy of the specified attribute
	double entropy(size_t nColumn);

	/// Finds the min and the range of the values of the specified attribute
	void minAndRange(size_t nAttribute, double* pMin, double* pRange);

	/// Estimates the actual min and range based on a random sample
	void minAndRangeUnbiased(size_t nAttribute, double* pMin, double* pRange);

	/// Shifts the data such that the mean occurs at the origin.
	/// Only continuous values are affected.  Nominal values are
	/// left unchanged.
	void centerMeanAtOrigin();

	/// Computes the arithmetic mean of the values in the specified column
	double mean(size_t nAttribute);

	/// Computes the median of the values in the specified column
	double median(size_t nAttribute);

	/// Computes the arithmetic means of all attributes
	void centroid(double* pOutCentroid);

	/// Computes the average variance of a single attribute
	double variance(size_t nAttr, double mean);

	/// Normalizes the specified attribute values
	void normalize(size_t nAttribute, double dInputMin, double dInputRange, double dOutputMin, double dOutputRange);

	/// Normalize a value from the input min and range to the output min and range
	static double normalize(double dVal, double dInputMin, double dInputRange, double dOutputMin, double dOutputRange);

	/// Returns the mean if the specified attribute is continuous, otherwise returns the most common nominal value in the attribute.
	double baselineValue(size_t nAttribute);

	/// Returns true iff the specified attribute contains homogenous values. (Unknowns are counted as homogenous with anything)
	bool isAttrHomogenous(size_t col);

	/// Returns true iff each of the last labelDims columns in the data are homogenous
	bool isHomogenous();

	/// If the specified attribute is continuous, replaces all missing values in that attribute with the mean.
	/// If the specified attribute is nominal, replaces all missing values in that attribute with the most common value.
	void replaceMissingValuesWithBaseline(size_t nAttr);

	/// Replaces all missing values by copying a randomly selected non-missing value in the same attribute.
	void replaceMissingValuesRandomly(size_t nAttr, GRand* pRand);

	/// This is an efficient algorithm for iteratively computing the principal component vector
	/// (the eigenvector of the covariance matrix) of the data. See "EM Algorithms for PCA and SPCA"
	/// by Sam Roweis, 1998 NIPS.
	/// nIterations should be a small constant. 20 seems work well for most applications.
	/// (To compute the next principal component, call RemoveComponent, then call this again.)
	void principalComponent(double* pOutVector, size_t dims, const double* pMean, GRand* pRand);

	/// Computes the first principal component assuming the mean is already subtracted out of the data
	void principalComponentAboutOrigin(double* pOutVector, size_t dims, GRand* pRand);

	/// Computes principal components, while ignoring missing values
	void principalComponentIgnoreUnknowns(double* pOutVector, size_t dims, const double* pMean, GRand* pRand);

	/// Computes the first principal component of the data with each row weighted according to the
	/// vector pWeights. (pWeights must have an element for each row.)
	void weightedPrincipalComponent(double* pOutVector, size_t dims, const double* pMean, const double* pWeights, GRand* pRand);

	/// After you compute the principal component, you can call this to obtain the eigenvalue that
	/// corresponds to that principal component vector (eigenvector).
	double eigenValue(const double* pMean, const double* pEigenVector, size_t dims, GRand* pRand);

	/// Removes the component specified by pComponent from the data. (pComponent should already be normalized.)
	/// This might be useful, for example, to remove the first principal component from the data so you can
	/// then proceed to compute the second principal component, and so forth.
	void removeComponent(const double* pMean, const double* pComponent, size_t dims);

	/// Removes the specified component assuming the mean is zero.
	void removeComponentAboutOrigin(const double* pComponent, size_t dims);

	/// Computes the minimum number of principal components necessary so that
	/// less than the specified portion of the deviation in the data
	/// is unaccounted for. (For example, if the data projected onto
	/// the first 3 principal components contains 90 percent of the
	/// deviation that the original data contains, then if you pass
	/// the value 0.1 to this method, it will return 3.)
	size_t countPrincipalComponents(double d, GRand* pRand);

	/// Computes the sum-squared distance between pPoint and all of the points in the dataset.
	/// (If pPoint is NULL, it computes the sum-squared distance with the origin.)
	/// (Note that this is equal to the sum of all the eigenvalues times the number of dimensions,
	/// so you can efficiently compute eigenvalues as the difference in sumSquaredDistance with
	/// the mean after removing the corresponding component, and then dividing by the number of
	/// dimensions. This is more efficient than calling eigenValue.)
	double sumSquaredDistance(const double* pPoint);

	/// Computes the sum-squared distance between the specified column of this and that.
	/// If the column is a nominal attribute, then Hamming distance is used.
	double columnSumSquaredDifference(GMatrix& that, size_t col);

	/// Computes the squared distance between this and that. (If transpose is true, computes the
	/// difference between this and the transpose of that.)
	double sumSquaredDifference(GMatrix& that, bool transpose = false);

	/// Computes the linear coefficient between the two specified attributes.
	/// Usually you will want to pass the mean values for attr1Origin and attr2Origin.
	double linearCorrelationCoefficient(size_t attr1, double attr1Origin, size_t attr2, double attr2Origin);

	/// Computes the covariance between two attributes
	double covariance(size_t nAttr1, double dMean1, size_t nAttr2, double dMean2);

	/// Computes the covariance matrix of the data
	GMatrix* covarianceMatrix();

	/// Performs a paired T-Test with data from the two specified attributes.
	/// pOutV will hold the degrees of freedom. pOutT will hold the T-value.
	/// You can use GMath::tTestAlphaValue to convert these to a P-value.
	void pairedTTest(size_t* pOutV, double* pOutT, size_t attr1, size_t attr2, bool normalize);

	/// Performs the Wilcoxon signed ranks test from the two specified attributes.
	/// If two values are closer than tolerance, they are considered to be equal.
	void wilcoxonSignedRanksTest(size_t attr1, size_t attr2, double tolerance, int* pNum, double* pWMinus, double* pWPlus);

	/// Prints the data to the specified stream
	void print(std::ostream& stream);

	/// Returns the number of ocurrences of the specified value in the specified attribute
	size_t countValue(size_t attribute, double value);

	/// Returns true iff this matrix is missing any values.
	bool doesHaveAnyMissingValues();

	/// Throws an exception if this data contains any missing values in a continuous attribute
	void ensureDataHasNoMissingReals();

	/// Throws an exception if this data contains any missing values in a nominal attribute
	void ensureDataHasNoMissingNominals();

	/// Computes the sum entropy of the data (or the sum variance for continuous attributes)
	double measureInfo();

	/// Computes the vector in this subspace that has the greatest distance from its
	/// projection into pThat subspace. Returns true if the results are computed. Returns
	/// false if the subspaces are so nearly parallel that pOut cannot be computed with
	/// accuracy.
	bool leastCorrelatedVector(double* pOut, GMatrix* pThat, GRand* pRand);

	/// Computes the cosine of the dihedral angle between this subspace and pThat subspace
	double dihedralCorrelation(GMatrix* pThat, GRand* pRand);

	/// Projects pPoint onto this hyperplane (where each row defines
	/// one of the orthonormal basis vectors of this hyperplane)
	/// This computes (A^T)Ap, where A is this matrix, and p is pPoint.
	void project(double* pDest, const double* pPoint);

	/// Projects pPoint onto this hyperplane (where each row defines
	/// one of the orthonormal basis vectors of this hyperplane)
	void project(double* pDest, const double* pPoint, const double* pOrigin);

	/// Performs bipartite matching of the rows in the specified matrices.
	/// 'a' and 'b' must have the same number of columns. 'b' must have at
	/// least as many rows as 'a'. Returns an array of indexes, i[], where i[j] is
	/// the row in b that corresponds with row j of a.
	/// "metric" is the distance metric that will be minimized. For example, if metric
	/// computes the squared distance between two vectors, then this method will
	/// find the pairings that minimize sum squared distance.
	/// k specifies the number of nearest-neighbors of each row to consider as candidates
	/// for pairing. If k is equal to the number of rows in a, then optimal pairings
	/// are guaranteed. If k is smaller, then results will be obtained faster, but
	/// optimal results are not guaranteed. (An efficient neighbor-finder that assumes
	/// metric conforms to the triangle inequality is used to find neighbors.)
	/// If the number of columns is not too big, then small values for k will usually return
	/// optimal or near-optimal results anyway. sqrt(rows) might be a good general value to
	/// use for k. As a special value, if k is 0, then all pairs are considered, and optimal
	/// results are guaranteed.
	static size_t* bipartiteMatching(GMatrix& a, GMatrix& b, GDistanceMetric& metric, size_t k = 0);

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE
protected:
	double determinantHelper(size_t nEndRow, size_t* pColumnList);
	void inPlaceSquareTranspose();
	void singularValueDecompositionHelper(GMatrix** ppU, double** ppDiag, GMatrix** ppV, bool throwIfNoConverge, size_t maxIters);
};


/// This is a special holder that guarantees the data set
/// will release all of its data before it is deleted
class GReleaseDataHolder
{
protected:
	GMatrix* m_pData;

public:
	GReleaseDataHolder(GMatrix* pData)
	{
		m_pData = pData;
	}

	~GReleaseDataHolder()
	{
		m_pData->releaseAllRows();
	}
};

/// This class guarantees that the rows in b are merged vertically back into a when this object goes out of scope.
class GMergeDataHolder
{
protected:
	GMatrix& m_a;
	GMatrix& m_b;

public:
	GMergeDataHolder(GMatrix& a, GMatrix& b) : m_a(a), m_b(b) {}
	~GMergeDataHolder()
	{
		m_a.mergeVert(&m_b);
	}
};


/// Represents an array of matrices or datasets that all have the same number of columns.
class GMatrixArray
{
protected:
	sp_relation m_pRelation;
	std::vector<GMatrix*> m_sets;

public:
	GMatrixArray(sp_relation& pRelation);
	GMatrixArray(size_t cols);
	~GMatrixArray();
	std::vector<GMatrix*>& sets() { return m_sets; }

	/// Adds a new dataset to the array and preallocates
	/// the specified number of rows
	GMatrix* newSet(size_t rows);

	/// Adds count new datasets to the array, and
	/// preallocates the specified number of rows in
	/// each one
	void newSets(size_t count, size_t rows);

	/// Deletes all the datasets
	void flush();

	/// Returns the index of the largest data set
	size_t largestSet();
};

} // namespace GClasses

#endif // __GMATRIX_H__
