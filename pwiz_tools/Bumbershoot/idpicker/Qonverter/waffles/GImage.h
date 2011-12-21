/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GIMAGE_H__
#define __GIMAGE_H__

#include <stddef.h>
#include "GError.h"

namespace GClasses {

class GBezier;
class GRect;
class GRand;
class GDoubleRect;

#define gBlue(c) ((c) & 0xff)
#define gGreen(c) (((c) >> 8) & 0xff)
#define gRed(c) (((c) >> 16) & 0xff)
#define gAlpha(c) (((c) >> 24) & 0xff)
#define gGray(c) (77 * gRed(c) + 150 * gGreen(c) + 29 * gBlue(c))
#define MAX_GRAY_VALUE (255 * 256)

// hue, saturation, and value all range from 0 to 1
void rgbToHsv(unsigned int c, float* pHue, float* pSaturation, float* pValue);

// alpha ranges from 0 to 255. hue, saturation, and value range from 0 to 1
unsigned int gAHSV(int alpha, float hue, float saturation, float value);

inline int ClipChan(int n)
{
	return std::max(0, std::min(255, n));
}

inline unsigned int gRGB(int r, int g, int b)
{
	return ((b & 0xff) | ((g & 0xff) << 8) | ((r & 0xff) << 16) | 0xff000000);
}

inline unsigned int gARGB(int a, int r, int g, int b)
{
	return ((b & 0xff) | ((g & 0xff) << 8) | ((r & 0xff) << 16) | ((a & 0xff) << 24));
}

inline unsigned int gFromGray(int gray)
{
	gray = gray >> 8;
	return gRGB(gray, gray, gray);
}

// Converts Big-Endian ABGR to Big-Endian ARGB. (My code assumes pixels
// are represented as Big-Endian ARGB.) Note that this same function
// also converts the other way (argb to abgr).
inline unsigned int abgrToArgb(unsigned int rgba)
{
	return gARGB(gAlpha(rgba), gBlue(rgba), gGreen(rgba), gRed(rgba));
}

inline unsigned int MixColors(unsigned int a, unsigned int b, int nRatio)
{
	int n2 = 256 - nRatio;
	return gARGB(
		(gAlpha(a) * nRatio + gAlpha(b) * n2) >> 8,
		(gRed(a) * nRatio + gRed(b) * n2) >> 8,
		(gGreen(a) * nRatio + gGreen(b) * n2) >> 8,
		(gBlue(a) * nRatio + gBlue(b) * n2) >> 8
		);
}

inline unsigned int MultiplyBrightness(unsigned int c, float f)
{
	return gARGB(gAlpha(c),
			ClipChan((int)(f * gRed(c))),
			ClipChan((int)(f * gGreen(c))),
			ClipChan((int)(f * gBlue(c)))
		);
}

// pOutHex must be a buffer of at least 7 bytes
const char* rgbToHex(char* pOutHex, unsigned int c);

unsigned int hexToRgb(const char* szHex);

/// Represents an image
class GImage
{
protected:
	unsigned int* m_pPixels;
	unsigned int m_width;
	unsigned int m_height;

public:
	GImage();
	virtual ~GImage();

	/// Load a file (Determines the format from the extension. Doesn't
	/// handle incorrect extensions. Currently supports .png, .bmp, .ppm, and .pgm.)
	void loadByExtension(const char* szFilename);

	/// Load the image from a PNG as raw data
	void loadPng(const unsigned char* pRawData, size_t nBytes);

	/// Load the image from a PNG file
	void loadPng(const char* szFilename);

	/// Load the image from a hex'd PNG file
	void loadPngFromHex(const char* szHex);

	/// Load the image from a BMP file
	void loadBmp(const char* szFilename);

	/// Load the image from a BMP stream
	void loadBmp(FILE* pFile);

	/// Load the image from a BMP raw data
	void loadBmp(const unsigned char* pRawData, int nLen);

	/// Save the image from a PPM file
	void loadPpm(const char* szFilename);

	/// Load the image from a PGM file
	void loadPgm(const char* szFilename);

	/// Saves to a file. (Determines the file type from the extension.
	/// Currently supports .png, .bmp, .ppm, and .pgm.)
	void saveByExtension(const char* szFilename);

	/// Save the image as a PNG to a stream
	void savePng(FILE* pFile);

	/// Save the image as a PNG to a file
	void savePng(const char* szFilename);

	/// Save the image to a BMP file
	void saveBmp(const char* szFilename);

	/// Save the image to a PPM file
	void savePpm(const char* szFilename);

	/// Save the image to a PGM file
	void savePgm(const char* szFilename);

	/// Get a pixel
	inline unsigned int pixel(int nX, int nY) const
	{
		GAssert((unsigned int)nX < m_width && (unsigned int)nY < m_height); // out of range
		return m_pPixels[m_width * nY + nX];
	}

	/// Get a pixel reference
	inline unsigned int* pixelRef(int nX, int nY)
	{
		GAssert((unsigned int)nX < m_width && (unsigned int)nY < m_height); // out of range
		return &m_pPixels[m_width * nY + nX];
	}

	/// Returns the raw array of pixels that represent this image
	inline unsigned int* pixels() { return m_pPixels; }

	/// Returns the color of the pixel nearest to the specified coordinates
	unsigned int pixelNearest(int nX, int nY) const;

	/// Drawing Primitives
	inline void setPixel(int nX, int nY, unsigned int color)
	{
		GAssert((unsigned int)nX < m_width && (unsigned int)nY < m_height); // out of range
		m_pPixels[m_width * nY + nX] = color;
	}

	/// Set a pixel (may be out of range of the image)
	void setPixelIfInRange(int nX, int nY, unsigned int color);

	/// Draw a translucent pixel
	void setPixelTranslucent(int nX, int nY, unsigned int color, double dOpacity);

	/// Returns an interpolated pixel
	unsigned int interpolatePixel(float dX, float dY);

	/// Fill the entire image with a single color
	void clear(unsigned int color);

	/// Erase the image and resize it
	void setSize(unsigned int nWidth, unsigned int nHeight);

	/// Draw a line (may include parts outside the bounds of the image)
	void line(int nX1, int nY1, int nX2, int nY2, unsigned int color);

	/// Draw a line. Don't check to make sure the end-points are within range.
	void lineNoChecks(int nX1, int nY1, int nX2, int nY2, unsigned int color);

	/// Draw an anti-aliassed line
	void lineAntiAlias(int nX1, int nY1, int nX2, int nY2, unsigned int color);

	/// Draw a hollow box
	void box(int nX1, int nY1, int nX2, int nY2, unsigned int color);

	/// Draws a filled-in box
	void boxFill(int x, int y, int w, int h, unsigned int c);

	/// Draw a circle (uses SafeSetPixel, so it's okay to draw off the edge
	void circle(int nX, int nY, float dRadius, unsigned int color);

	/// Draws a filled-in circle (uses SafeDrawLine, so it's okay to draw off the edge
	void circleFill(int nX, int nY, float fRadius, unsigned int color);

	/// Draws a simple arrow from (x1, y1) to (x2, y2). headSize is the length
	/// (in pixels) of the arrow head. This method is intentionally
	/// simple, not exhaustive. If you want a special super-parameterized arrow,
	/// you can draw it with combinations of other methods.
	void arrow(int x1, int y1, int x2, int y2, unsigned int col, int headSize);

	/// Draw an ellipse
	void ellipse(int nX, int nY, double dRadius, double dHeightToWidthRatio, unsigned int color);

	/// Tolerant flood fill
	void floodFill(int nX, int nY, unsigned int color, int nTolerance);

	/// Tolerant boundary fill
	void boundaryFill(int nX, int nY, unsigned char nBoundaryR, unsigned char nBoundaryG, unsigned char nBoundaryB, unsigned char nFillR, unsigned char nFillG, unsigned char nFillB, int nTolerance);

	/// Draws a filled-in triangle
	void triangleFill(float x1, float y1, float x2, float y2, float x3, float y3, unsigned int c);

	/// Draws a fat line
	void fatLine(float x1, float y1, float x2, float y2, float fThickness, unsigned int c);

	/// Draws a single text character using a built-in font. (The pixel-height of the font is 12*size.)
	void textChar(char ch, int x, int y, int wid, int hgt, float size, unsigned int col);

	/// Prints a line of text using a built-in font. (The pixel-height of the font is 12*size.)
	/// col specifies the color.
	void text(const char* text, int x, int y, float size, unsigned int col, int wid = 50000, int hgt = 50000);

	/// Determines how many pixels of width are required to print a single character of text
	static int measureCharWidth(char c, float size);

	/// Determines how many pixels of width are required to print a line of text
	static int measureTextWidth(const char* text, float size);

	/// Counts the number of characters that can be printed in the given horizArea
	static int countTextChars(int horizArea, const char* text, float size);

	/// Draws a dot at a sub-pixel location
	void dot(float x, float y, float radius, unsigned int fore, unsigned int back);

	/// Flip the image horizontally
	void flipHorizontally();

	/// Flip the image vertically
	void flipVertically();

	/// Rotate the image around the specified point. dAngle is specified in radians. The results
	/// are placed in this image. (This image is resized to the same size as the source image,
	/// and anything that rotates out of bounds is clipped.)
	void rotate(GImage* pSourceImage, int nX, int nY, double dAngle);

	/// Makes this a copy of pSourceImage rotated counter-clockwise by 90 degrees
	void rotateCounterClockwise90(GImage* pSourceImage);

	/// Makes this a copy of pSourceImage rotated clockwise by 90 degrees
	void rotateClockwise90(GImage* pSourceImage);

	/// Scale the image
	void scale(unsigned int nNewWidth, unsigned int nNewHeight);

	/// Crops the image. (You can crop bigger by using values outside the picture)
	void crop(int left, int top, int width, int height);

	/// Converts the image to gray scale
	void convertToGrayScale();

	/// Equalizes the color histogram
	void equalizeColorSpread();

	/// Locally equalize the color histogram
	void locallyEqualizeColorSpread(int nLocalSize, float fExtent = 1);

	/// Blur the image by convolving with a Gaussian kernel
	void blur(double dRadius);

	/// Blurs by averaging uniformly over a square, plus some optimizations
	void blurQuick(int iters, int nRadius);

	/// Sharpen the image
	void sharpen(double dFactor);

	/// Inverts the pixels in the image
	void invert();

	/// Inverts the pixels in a particular rect
	void invertRect(GRect* pRect);

	/// Finds edges and makes them glow
	void makeEdgesGlow(float fThresh, int nThickness, int nOpacity, unsigned int color);

	/// Draws a border around anything that touches the background color
	void addBorder(const GImage* pSourceImage, unsigned int cBackground, unsigned int cBorder);

	void convolve(GImage* pKernel);

	void convolveKernel(GImage* pKernel);

	void horizDifferenceize();
	void horizSummize();

	void swapData(GImage* pSwapImage);

	void copy(GImage* pSourceImage);

	void copyRect(GImage* pSourceImage, int nLeft, int nTop, int nRight, int nBottom);

	/// Returns the width of the image in pixels
	unsigned int width() const { return m_width; }

	/// Returns the height of the image in pixels
	unsigned int height() const { return m_height; }

	/// Blit an image into this image.
	/// The dest area can be out of the dest image. The alpha channel is ignored.
	void blit(int x, int y, GImage* pSource, GRect* pSourceRect = NULL);

	/// Blit an image into this image. The source rect must be within the source image.
	/// The dest area can be out of the dest image. Also performs alpha blending.
	void blitAlpha(int x, int y, GImage* pSource, GRect* pSourceRect = NULL);

	/// Stretches the specified portion of the source rect to fit the destination
	/// rect and alpha-blits onto the this image
	void blitAlphaStretch(GRect* pDestRect, GImage* pImage, GRect* pSourceRect = NULL);

	/// Performs an interpolating stretch-blit, both to and from a sub-pixel-specified rect
	void blitStretchInterpolate(GDoubleRect* pDestRect, GImage* pImage, GDoubleRect* pSourceRect = NULL);

	/// Munges the image. nStyle should be 0, 1, 2, or 3. Each value munges a different way.
	/// fExtent should be between 0 and 1, where 0 doesn't change much and 1 totally munges it.
	/// You're responsible to delete the munged image this returns
	GImage* munge(int nStyle, float fExtent);

	/// dRadians ranges from 0 to 2PI
	/// fAmount ranges from 0 to about 4, but small values (like .1) seem to look best
	void moveLight(double dRadians, float fAmount);

	/// This uses a Gaussian to interpolate between the original image and a translated
	/// image. The result appears as though the start point were stretched to the end point.
	void stretch(int nXStart, int nYStart, int nXEnd, int nYEnd);

	/// Make a string of text that would be difficult for non-humans to read
	void captcha(const char* szText, GRand* pRand);

	/// Generates a discrete kernel to approximate a Gaussian. nWidth is both the
	/// width and height of the kernel. (Usually an odd number is desireable so there
	/// is a bright center pixel.) fDepth is the value of the center of the kernel.
	/// 1 <= fDepth <= 255. The values are computed such that the center of each edge
	/// has a value of 1.
	void gaussianKernel(int nWidth, float fDepth);

	/// Values for dExtent range from 0 to 1. In most cases, a small value (like .1)
	/// is desireable
	void highPassFilter(double dExtent);

	/// Values for dExtent range from 0 to 1. In most cases, a small value (like .1)
	/// is desireable
	void lowPassFilter(double dExtent);

	/// Thresholds the image (to black and white) at the specified grayscale value.
	/// (0 <= nGrayscaleValue < 65281).      (65281 = 255 * 256 + 1)
	void threshold(int nGrayscaleValue);

	/// Sets every pixel to the grayscale median of the neighborhood that fits within
	/// the specified radius. (A radius of 1 will include only 4 neighbors. A radius
	/// of 1.5 will include 8 neighbors.)
	void medianFilter(float fRadius);

	/// a basic morphological operator
	void dialate(GImage* pStructuringElement);

	/// A basic morphological operator
	void erode(GImage* pStructuringElement);

	/// if nPixels > 0, opens the image by the specified number of pixels
	/// if nPixels < 0, closes the image by the specified number of pixels
	void open(int nPixels);

	/// Sets the alpha channel value for all pixels in the image
	void setGlobalAlpha(int alpha);

	double moment(double centerX, double centerY, double i, double j);

	/// Uses moments to compute mean and orientation
	void meanAndOrientation(double* pMeanX, double* pMeanY, double* pRadians);

	void gradientMagnitudeImage(const GImage* pIn);

	/// Determines whether the point (x, y) is inside the triangle
	/// specified by (x0, y0), (x1, y1), (x2, y2). On any edge is considered
	/// to be within the triangle
	static bool isPointInsideTriangle(float x, float y,
					float x0, float y0,
					float x1, float y1,
					float x2, float y2);

	/// Given the three vertices of a triangle, this computes the weights for
	/// each vertex that linearly interpolates to the point (x, y)
	static void triangleWeights(float* pW0, float* pW1, float* pW2,
					float x, float y,
					float x0, float y0,
					float x1, float y1,
					float x2, float y2);

	/// Given three weights and three points, interpolate the point, and
	/// interpolate the color at that point
	unsigned int interpolateWithinTriangle(float w0, float w1, float w2,
					float x0, float y0,
					float x1, float y1,
					float x2, float y2);

	/// The value of each channel is centered around 128, multiplied by
	/// fContrastScale, added to nBrightnessDelta, and clipped to [0-255].
	void contrastAndBrightness(float fContrastScale, int nBrightnessDelta);

	/// Adjusts the hue, saturation, and value of the image by the specified amounts.
	/// The saturation and value are clipped with a min of 0 and a max of 1. The hue
	/// has a cycle of 1, so adjusting by any integer will leave the hue unchanged.
	void hueSaturationAndValue(GImage* pSource, float hue, float saturation, float value);

	/// Makes the darkest parts of the image darker. (Note that if you invert
	/// first, shade, and then invert again, you will have hilighted the lightest
	/// parts of the image.)
	/// fStart is ranges from 0 to 1, and specifies the brightness threshold at
	/// which the shading begins. fEnd also ranges from 0 to 1. Everything darker
	/// than fEnd will be completely black. fStart must be >= fEnd. fSteepness
	/// specifies the curvature of the gradient.
	void shade(float fStart, float fEnd, float fSteepness);

	/// Converts the whole image from Abgr (little-endian Rgba) to big-endian Argb
	void convertAbgrToArgb();

	/// Replaces every occurrence of the exact color "before" with "after"
	void replaceColor(unsigned int before, unsigned int after);

protected:
	void loadPixMap(FILE* pFile, bool bTextData, bool bGrayScale);
	void savePixMap(FILE* pFile, bool bTextData, bool bGrayScale);
	void floodFillRecurser(int nX, int nY, unsigned char nSrcR, unsigned char nSrcG, unsigned char nSrcB, unsigned char nDstR, unsigned char nDstG, unsigned char nDstB, int nTolerance);
	bool clipLineEndPoints(int& nX1, int& nY1, int& nX2, int& nY2);
	void textCharSmall(char ch, int x, int y, int wid, int hgt, float size, unsigned int col);
};

} // namespace GClasses

#endif // __GIMAGE_H__
