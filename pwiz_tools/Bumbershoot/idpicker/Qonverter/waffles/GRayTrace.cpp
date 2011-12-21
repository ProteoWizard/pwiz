/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GRayTrace.h"
#include "GError.h"
#include "GHashTable.h"
#include "GDom.h"
#include <algorithm>
#include "GImage.h"
#include "GMath.h"
#include <cmath>

namespace GClasses {

using std::vector;

GDomNode* GRayTraceColor::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "a", pDoc->newDouble(a));
	pNode->addField(pDoc, "r", pDoc->newDouble(r));
	pNode->addField(pDoc, "g", pDoc->newDouble(g));
	pNode->addField(pDoc, "b", pDoc->newDouble(b));
	return pNode;
}

void GRayTraceColor::deserialize(GDomNode* pNode)
{
	a = (G3DReal)pNode->field("a")->asDouble();
	r = (G3DReal)pNode->field("r")->asDouble();
	g = (G3DReal)pNode->field("g")->asDouble();
	b = (G3DReal)pNode->field("b")->asDouble();
}

GRayTraceColor::GRayTraceColor(unsigned int c)
{
	a = ((G3DReal)gAlpha(c)) / 255;
	r = ((G3DReal)gRed(c)) / 255;
	g = ((G3DReal)gGreen(c)) / 255;
	b = ((G3DReal)gBlue(c)) / 255;
}

void GRayTraceColor::set(unsigned int c)
{
	a = ((G3DReal)gAlpha(c)) / 255;
	r = ((G3DReal)gRed(c)) / 255;
	g = ((G3DReal)gGreen(c)) / 255;
	b = ((G3DReal)gBlue(c)) / 255;
}

unsigned int GRayTraceColor::color()
{
	return gARGB(	std::min(255, (int)(a * 255)),
			std::min(255, (int)(r * 255)),
			std::min(255, (int)(g * 255)),
			std::min(255, (int)(b * 255))
		);
}

void GRayTraceColor::makeSliderColor(float f, GRayTraceColor* pDiffuseColor)
{
	a = 1.0f;
	if(f < 0.5)
	{
		f += f;
		r = f * pDiffuseColor->r;
		g = f * pDiffuseColor->g;
		b = f * pDiffuseColor->b;
	}
	else
	{
		f += (f - 0.5f);
		r = (1.0f - f) * pDiffuseColor->r + f;
		g = (1.0f - f) * pDiffuseColor->g + f;
		b = (1.0f - f) * pDiffuseColor->b + f;
	}
}

// -----------------------------------------------------------------------------

GRayTraceCamera::GRayTraceCamera(GDomNode* pNode)
: GCamera(pNode)
{
	m_focalDistance = pNode->field("focal")->asDouble();
	m_lensDiameter = pNode->field("lens")->asDouble();
	m_maxDepth = (int)pNode->field("depth")->asInt();
}

// virtual
GDomNode* GRayTraceCamera::serialize(GDom* pDoc)
{
	GDomNode* pNode = GCamera::serialize(pDoc);
	pNode->addField(pDoc, "focal", pDoc->newDouble(m_focalDistance));
	pNode->addField(pDoc, "lens", pDoc->newDouble(m_lensDiameter));
	pNode->addField(pDoc, "depth", pDoc->newInt(m_maxDepth));
	return pNode;
}

// -----------------------------------------------------------------------------

#define MIN_RAY_DISTANCE ((G3DReal).001)

// Represents a ray for ray tracing
class GRayTraceRay
{
public:
	G3DVector m_collisionPoint;
	G3DVector m_normalVector;
	G3DVector m_reflectionVector;
	GRayTraceColor m_color;
	G3DReal m_indexOfRefraction;
	bool m_bHitTexture;
	G3DReal m_textureX;
	G3DReal m_textureY;
	GRand* m_pRand;

	GRayTraceRay(GRand* m_pRand);
	GRayTraceRay(GRayTraceRay* pThat);
	~GRayTraceRay();

	void Cast(GRayTraceScene* pScene, G3DVector* pRayOrigin, G3DVector* pDirectionVector, int nMaxDepth);
	void Trace(GRayTraceScene* pScene, G3DVector* pRayOrigin, G3DVector* pDirectionVector, int nMaxDepth, bool bEmissive);
	void SetTextureCoords(G3DReal x, G3DReal y);
	void GetTextureCoords(G3DReal* x, G3DReal* y);
	bool DidRayHitTexture() { return m_bHitTexture; }
	static void JitterRay(G3DVector* pVector, G3DReal jitter, GRand* pRand);

protected:
	bool ComputeTransmissionVector(G3DVector* pDirectionVector, G3DVector* pTransmissionVector, G3DReal oldIndexOfRefraction, G3DReal newIndexOfRefraction);
	void ComputeRandomDiffuseVector(G3DVector* pOutVector);
};


GRayTraceRay::GRayTraceRay(GRand* pRand)
{
	m_indexOfRefraction = 1;
	m_pRand = pRand;
}

GRayTraceRay::GRayTraceRay(GRayTraceRay* pThat)
{
	m_indexOfRefraction = pThat->m_indexOfRefraction;
	m_pRand = pThat->m_pRand;
}

GRayTraceRay::~GRayTraceRay()
{
}

bool GRayTraceRay::ComputeTransmissionVector(G3DVector* pDirectionVector, G3DVector* pTransmissionVector, G3DReal oldIndexOfRefraction, G3DReal newIndexOfRefraction)
{
	double ratio = (double)oldIndexOfRefraction / newIndexOfRefraction;
	double comp = (double)pDirectionVector->dotProduct(&m_normalVector);
	double tmp = (double)1 - (ratio * ratio * ((double)1 - (comp * comp)));
	if(tmp < 0)
		return false;
	pTransmissionVector->copy(&m_normalVector);
	pTransmissionVector->multiply((G3DReal)(-ratio * comp - sqrt(tmp)));
	G3DVector x(pDirectionVector);
	x.multiply((G3DReal)ratio);
	pTransmissionVector->add(&x);
	pTransmissionVector->normalize();
	return true;
}

void GRayTraceRay::ComputeRandomDiffuseVector(G3DVector* pOutVector)
{
	G3DVector u;
	G3DVector v;
	if(std::abs(m_normalVector.m_vals[0]) < .5) // if N is not co-linear with (1, 0, 0)
	{
		v.set(1, 0, 0);
		u.crossProduct(&m_normalVector, &v);
	}
	else
	{
		v.set(0, 1, 0);
		u.crossProduct(&m_normalVector, &v);
	}
	u.normalize();
	v.crossProduct(&u, &m_normalVector);
	double a, b, c;
	while(true)
	{
		a = m_pRand->uniform() * 2 - 1;
		b = m_pRand->uniform() * 2 - 1;
		if((a * a) + (b * b) < 1)
			break;
	}
	c = sqrt(1.0 - (a * a + b * b));
	u.multiply((G3DReal)a);
	v.multiply((G3DReal)b);
	pOutVector->copy(&m_normalVector);
	pOutVector->multiply((G3DReal)c);
	pOutVector->add(&u);
	pOutVector->add(&v);
}

void GRayTraceRay::JitterRay(G3DVector* pVector, G3DReal jitter, GRand* pRand)
{
	pVector->m_vals[0] += (jitter * (G3DReal)(pRand->uniform() * 2 - 1));
	pVector->m_vals[1] += (jitter * (G3DReal)(pRand->uniform() * 2 - 1));
	pVector->m_vals[2] += (jitter * (G3DReal)(pRand->uniform() * 2 - 1));
	pVector->normalize();
}

void GRayTraceRay::Cast(GRayTraceScene* pScene, G3DVector* pRayOrigin, G3DVector* pDirectionVector, int nMaxDepth)
{
	// Find the object
	G3DReal distance;
	GAssert(pScene->boundingBoxTree()); // You must to call RenderBegin first?
	GRayTraceObject* pClosestObject = pScene->boundingBoxTree()->closestIntersection(pRayOrigin, pDirectionVector, &distance);
	if(!pClosestObject)
	{
		m_color.copy(pScene->backgroundColor());
		return;
	}

	// Compute the collision point
	m_collisionPoint.copy(pDirectionVector);
	m_collisionPoint.multiply(distance);
	m_collisionPoint.add(pRayOrigin);

	// Compute the normal
	m_bHitTexture = false;
	pClosestObject->normalVector(this);
	if(!pClosestObject->isCulled())
	{
		if(pDirectionVector->dotProduct(&m_normalVector) > 0)
			m_normalVector.multiply(-1);
	}

	// Compute the reflection vector
	m_reflectionVector.reflectionVector(pDirectionVector, &m_normalVector);

	// Compute the color
	GRayTraceMaterial* pMaterial = pClosestObject->material();
	pMaterial->computeColor(pScene, this, true, true);

	// Cast child rays
	if(nMaxDepth > 0)
	{
		// Reflection
		GRayTraceColor* pReflectedColor = pMaterial->color(GRayTraceMaterial::Reflective, this);
		if(!pReflectedColor->isBlack())
		{
			G3DReal jitter = pMaterial->glossiness();
			if(jitter > 0)
				JitterRay(&m_reflectionVector, jitter, m_pRand);
			GRayTraceRay reflectionRay(this);
			reflectionRay.Cast(pScene, &m_collisionPoint, &m_reflectionVector, nMaxDepth - 1);
			reflectionRay.m_color.multiply(pReflectedColor);
			m_color.add(&reflectionRay.m_color);
		}

		// Transmission
		GRayTraceColor* pTransmissionColor = pMaterial->color(GRayTraceMaterial::Transmissive, this);
		if(!pTransmissionColor->isBlack())
		{
			// Compute transmission ray
			G3DReal newIndexOfRefraction;
			if(m_indexOfRefraction > .9999 && m_indexOfRefraction < 1.0001)
				newIndexOfRefraction = pMaterial->indexOfRefraction();
			else
				newIndexOfRefraction = 1;
			G3DVector transmissionVector;
			if(!ComputeTransmissionVector(pDirectionVector, &transmissionVector, m_indexOfRefraction, newIndexOfRefraction))
			{
				// total internal reflection occurs
				transmissionVector.copy(&m_reflectionVector);
				newIndexOfRefraction = m_indexOfRefraction;
			}

			// Cast transmission ray
			G3DReal jitter = pMaterial->cloudiness();
			if(jitter > 0)
				JitterRay(&transmissionVector, jitter, m_pRand);
			GRayTraceRay transmissionRay(this);
			transmissionRay.m_indexOfRefraction = newIndexOfRefraction;
			transmissionRay.Cast(pScene, &m_collisionPoint, &transmissionVector, nMaxDepth - 1);
			transmissionRay.m_color.multiply(pTransmissionColor);
			m_color.add(&transmissionRay.m_color);
		}
	}
}

#define PATH_TRACER_PROB_REFLECTIVE 0.5
#define PATH_TRACER_PROB_TRANSMISSIVE 0.3

void GRayTraceRay::Trace(GRayTraceScene* pScene, G3DVector* pRayOrigin, G3DVector* pDirectionVector, int nMaxDepth, bool bEmissive)
{
	// Find the object
	G3DReal distance;
	GRayTraceObject* pClosestObject = pScene->boundingBoxTree()->closestIntersection(pRayOrigin, pDirectionVector, &distance);
	if(!pClosestObject)
	{
		m_color.copy(pScene->backgroundColor());
		return;
	}

	// Compute the collision point
	m_collisionPoint.copy(pDirectionVector);
	m_collisionPoint.multiply(distance);
	m_collisionPoint.add(pRayOrigin);

	// Compute the normal
	m_bHitTexture = false;
	pClosestObject->normalVector(this);
	if(!pClosestObject->isCulled())
	{
		if(pDirectionVector->dotProduct(&m_normalVector) > 0)
			m_normalVector.multiply(-1);
	}

	// Compute the reflection vector
	m_reflectionVector.reflectionVector(pDirectionVector, &m_normalVector);

	// Compute the color
	GRayTraceMaterial* pMaterial = pClosestObject->material();
	pMaterial->computeColor(pScene, this, false, false);
	if(bEmissive)
		m_color.add(pMaterial->color(GRayTraceMaterial::Emissive, this));

	// Cast child rays
	if(nMaxDepth > 0)
	{
		GRayTraceColor* pReflectedColor = pMaterial->color(GRayTraceMaterial::Reflective, this);
		GRayTraceColor* pTransmissionColor = pMaterial->color(GRayTraceMaterial::Transmissive, this);
		GRayTraceColor* pDiffuseColor = pMaterial->color(GRayTraceMaterial::Diffuse, this);
		G3DReal lumReflective = pReflectedColor->r * (G3DReal)0.299 + pReflectedColor->g * (G3DReal)0.587 + pReflectedColor->b * (G3DReal)0.114;
		G3DReal lumTransmissive = pTransmissionColor->r * (G3DReal)0.299 + pTransmissionColor->g * (G3DReal)0.587 + pTransmissionColor->b * (G3DReal)0.114;
		G3DReal lumDiffuse = pDiffuseColor->r * (G3DReal)0.299 + pDiffuseColor->g * (G3DReal)0.587 + pDiffuseColor->b * (G3DReal)0.114;
		G3DReal lumSum = lumReflective + lumTransmissive + lumDiffuse;
		if(lumSum > .0001)
		{
			lumReflective /= lumSum;
			lumTransmissive /= lumSum;
			lumDiffuse /= lumSum;
			double d = m_pRand->uniform();
			if(d < lumReflective)
			{
				// Reflective path
				G3DReal jitter = pMaterial->glossiness();
				if(jitter > 0)
					JitterRay(&m_reflectionVector, jitter, m_pRand);
				GRayTraceRay reflectionRay(this);
				reflectionRay.Trace(pScene, &m_collisionPoint, &m_reflectionVector, nMaxDepth - 1, true);
				reflectionRay.m_color.multiply(pReflectedColor);
				reflectionRay.m_color.multiply((G3DReal)1.0 / lumReflective); // Compensate for less than one probability of selecting this path
				m_color.add(&reflectionRay.m_color);
			}
			else if(d < lumReflective + lumTransmissive)
			{
				// Compute transmission ray
				G3DReal newIndexOfRefraction;
				if(m_indexOfRefraction > .9999 && m_indexOfRefraction < 1.0001)
					newIndexOfRefraction = pMaterial->indexOfRefraction();
				else
					newIndexOfRefraction = 1;
				G3DVector transmissionVector;
				if(!ComputeTransmissionVector(pDirectionVector, &transmissionVector, m_indexOfRefraction, newIndexOfRefraction))
				{
					// total internal reflection occurs
					transmissionVector.copy(&m_reflectionVector);
					newIndexOfRefraction = m_indexOfRefraction;
				}

				// Cast transmission ray
				G3DReal jitter = pMaterial->cloudiness();
				if(jitter > 0)
					JitterRay(&transmissionVector, jitter, m_pRand);
				GRayTraceRay transmissionRay(this);
				transmissionRay.m_indexOfRefraction = newIndexOfRefraction;
				transmissionRay.Trace(pScene, &m_collisionPoint, &transmissionVector, nMaxDepth - 1, true);
				transmissionRay.m_color.multiply(pTransmissionColor);
				transmissionRay.m_color.multiply((G3DReal)1.0 / lumTransmissive); // Compensate for less than one probability of selecting this path
				m_color.add(&transmissionRay.m_color);
			}
			else
			{
				// Diffuse path
				G3DVector diffuseVector;
				ComputeRandomDiffuseVector(&diffuseVector);
				GRayTraceRay diffuseRay(this);
				diffuseRay.Trace(pScene, &m_collisionPoint, &diffuseVector, nMaxDepth - 1, false);
				diffuseRay.m_color.multiply(pDiffuseColor);
				diffuseRay.m_color.multiply((G3DReal)1.0 / lumDiffuse); // Compensate for less than one probability of selecting this path
				m_color.add(&diffuseRay.m_color);
			}
		}
	}
}

// I'm not sure it really makes sense to store the texture coordinates
// in the ray, but somehow this data needs to be passed from the tri-mesh
// to the material, and the ray is a convenient carrier
void GRayTraceRay::SetTextureCoords(G3DReal x, G3DReal y)
{
	m_bHitTexture = true;
	m_textureX = x;
	m_textureY = y;
}

void GRayTraceRay::GetTextureCoords(G3DReal* x, G3DReal* y)
{
	*x = m_textureX;
	*y = m_textureY;
}

// -----------------------------------------------------------------------------

GRayTraceScene::GRayTraceScene(GRand* pRand)
: m_backgroundColor(1, (G3DReal).6, (G3DReal).7, (G3DReal).5),
  m_ambientLight(1, (G3DReal).3, (G3DReal).3, (G3DReal).3), m_pRand(pRand)
{
	m_pCamera = new GRayTraceCamera(320, 240);
	m_pBoundingBoxTree = NULL;
	m_pImage = NULL;
	m_pDistanceMap = NULL;
	m_nY = -1;
	m_toneMappingConstant = .5;
	m_eMode = FAST_RAY_TRACE;
}

GRayTraceScene::GRayTraceScene(GDomNode* pNode, GRand* pRand)
: m_pRand(pRand)
{
	m_backgroundColor.deserialize(pNode->field("bgcol"));
	m_ambientLight.deserialize(pNode->field("ambient"));
	for(GDomListIterator it1(pNode->field("materials")); it1.current(); it1.advance())
		m_materials.push_back(GRayTraceMaterial::deserialize(it1.current()));
	for(GDomListIterator it2(pNode->field("meshes")); it2.current(); it2.advance())
		m_meshes.push_back(new GRayTraceTriMesh(it2.current(), this));
	for(GDomListIterator it3(pNode->field("objects")); it3.current(); it3.advance())
		m_objects.push_back(GRayTraceObject::deserialize(it3.current(), this));
	;
	for(GDomListIterator it4(pNode->field("lights")); it4.current(); it4.advance())
		m_lights.push_back(GRayTraceLight::deserialize(it4.current(), this));
	m_pCamera = new GRayTraceCamera(pNode->field("camera"));
	m_toneMappingConstant = pNode->field("tone")->asDouble();
	m_eMode = (RenderMode)pNode->field("mode")->asInt();
}

GRayTraceScene::~GRayTraceScene()
{
	flushObjects();
	for(vector<GRayTraceLight*>::iterator it = m_lights.begin(); it != m_lights.end(); it++)
		delete(*it);
	delete(m_pCamera);
	delete(m_pImage);
	delete(m_pDistanceMap);
}

GDomNode* GRayTraceScene::serialize(GDom* pDoc)
{
	GDomNode* pSceneNode = pDoc->newObj();
	pSceneNode->addField(pDoc, "bgcol", m_backgroundColor.serialize(pDoc));
	pSceneNode->addField(pDoc, "ambient", m_ambientLight.serialize(pDoc));
	GDomNode* pMaterialsNode = pSceneNode->addField(pDoc, "materials", pDoc->newList());
	for(size_t i = 0; i < m_materials.size(); i++)
		pMaterialsNode->addItem(pDoc, m_materials[i]->serialize(pDoc));
	GDomNode* pMeshesNode = pSceneNode->addField(pDoc, "meshes", pDoc->newList());
	for(size_t i = 0; i < m_meshes.size(); i++)
		pMeshesNode->addItem(pDoc, m_meshes[i]->serialize(pDoc, this));
	GDomNode* pObjectsNode = pSceneNode->addField(pDoc, "objects", pDoc->newList());
	for(size_t i = 0; i < m_objects.size(); i++)
		pObjectsNode->addItem(pDoc, m_objects[i]->serialize(pDoc, this));
	GDomNode* pLightsNode = pSceneNode->addField(pDoc, "lights", pDoc->newList());
	for(size_t i = 0; i < m_lights.size(); i++)
		pLightsNode->addItem(pDoc, m_lights[i]->serialize(pDoc, this));
	pSceneNode->addField(pDoc, "camera", m_pCamera->serialize(pDoc));
	pSceneNode->addField(pDoc, "tone", pDoc->newDouble(m_toneMappingConstant));
	pSceneNode->addField(pDoc, "mode", pDoc->newInt(m_eMode));
	return pSceneNode;
}

void GRayTraceScene::flushObjects()
{
	for(vector<GRayTraceMaterial*>::iterator it = m_materials.begin(); it != m_materials.end(); it++)
		delete(*it);
	m_materials.clear();
	for(vector<GRayTraceTriMesh*>::iterator it = m_meshes.begin(); it != m_meshes.end(); it++)
		delete(*it);
	m_meshes.clear();
	for(vector<GRayTraceObject*>::iterator it = m_objects.begin(); it != m_objects.end(); it++)
		delete(*it);
	m_objects.clear();
	delete(m_pBoundingBoxTree);
	m_pBoundingBoxTree = NULL;
}

void GRayTraceScene::swapObjects(GRayTraceScene* pOther)
{
	m_materials.swap(pOther->m_materials);
	m_meshes.swap(pOther->m_meshes);
	m_objects.swap(pOther->m_objects);
	std::swap(m_pBoundingBoxTree, pOther->m_pBoundingBoxTree);
}

size_t GRayTraceScene::materialCount()
{
	return m_materials.size();
}

size_t GRayTraceScene::meshCount()
{
	return m_meshes.size();
}

size_t GRayTraceScene::objectCount()
{
	return m_objects.size();
}

size_t GRayTraceScene::lightCount()
{
	return m_lights.size();
}

GRayTraceMaterial* GRayTraceScene::material(size_t n)
{
	if(n >= m_materials.size())
		ThrowError("out of range");
	return m_materials[n];
}

GRayTraceTriMesh* GRayTraceScene::mesh(size_t n)
{
	if(n >= m_meshes.size())
		ThrowError("out of range");
	return m_meshes[n];
}

GRayTraceObject* GRayTraceScene::object(size_t n)
{
	if(n >= m_objects.size())
		ThrowError("out of range");
	return m_objects[n];
}

GRayTraceLight* GRayTraceScene::light(size_t n)
{
	if(n >= m_lights.size())
		ThrowError("out of range");
	return m_lights[n];
}

void GRayTraceScene::addMaterial(GRayTraceMaterial* pMaterial)
{
	m_materials.push_back(pMaterial);
}

template<typename T>
size_t GetIndexOfPointerInVector(vector<T*>& vec, T* p)
{
	for(typename vector<T*>::iterator it = vec.begin(); it != vec.end(); it++)
		if(p == *it)
			return (it - vec.begin());
	ThrowError("The specified value could not be found");
	return -1;
}

size_t GRayTraceScene::materialIndex(GRayTraceMaterial* pMaterial)
{
	return GetIndexOfPointerInVector(m_materials, pMaterial);
}

size_t GRayTraceScene::meshIndex(GRayTraceTriMesh* pMesh)
{
	return GetIndexOfPointerInVector(m_meshes, pMesh);
}

size_t GRayTraceScene::objectIndex(GRayTraceObject* pObj)
{
	return GetIndexOfPointerInVector(m_objects, pObj);
}

void GRayTraceScene::addMesh(GRayTraceTriMesh* pMesh)
{
	m_meshes.push_back(pMesh);
	size_t nCount = pMesh->triangleCount();
	for(size_t i = 0; i < nCount; i++)
		addObject(new GRayTraceTriangle(pMesh, i));
}

void GRayTraceScene::addObject(GRayTraceObject* pObject)
{
	m_objects.push_back(pObject);
}

void GRayTraceScene::addLight(GRayTraceLight* pLight)
{
	m_lights.push_back(pLight);
}

void GRayTraceScene::activateDistanceMap()
{
	if(m_pDistanceMap)
		delete[] m_pDistanceMap;
	m_pDistanceMap = new G3DReal[m_pCamera->imageWidth() * m_pCamera->imageHeight()];
}

void GRayTraceScene::drawWireFrame()
{
	// Allocate an image
	if(!m_pImage)
		m_pImage = new GImage();
	int nWidth = m_pCamera->imageWidth();
	int nHeight = m_pCamera->imageHeight();
	m_pImage->setSize(nWidth, nHeight);
	m_pImage->clear(0xff000000);
	for(vector<GRayTraceObject*>::iterator it = m_objects.begin(); it != m_objects.end(); it++)
		(*it)->drawWireFrame(m_pCamera, m_pImage);
}

void GRayTraceScene::renderBegin()
{
	// Allocate an image
	if(!m_pImage)
		m_pImage = new GImage();
	int nWidth = m_pCamera->imageWidth();
	int nHeight = m_pCamera->imageHeight();
	m_pImage->setSize(nWidth, nHeight);

	// Rebuild the bounding box tree
	delete(m_pBoundingBoxTree);
	m_pBoundingBoxTree = GRayTraceBoundingBoxBase::makeBoundingBoxTree(this);

	// Precompute vectors
	G3DVector v(m_pCamera->viewUpVector());
	G3DVector u(m_pCamera->viewSideVector());
	G3DReal halfViewHeight = m_pCamera->halfViewHeight();
	G3DReal halfViewWidth = halfViewHeight * nWidth / nHeight;
	u.multiply(halfViewWidth);
	v.multiply(halfViewHeight);
	m_pixSide.copy(m_pCamera->lookFromPoint());
	m_pixSide.add(m_pCamera->lookDirection());
	m_pixSide.subtract(&u);
	m_pixSide.subtract(&v);
	m_pixDX.copy(&u);
	m_pixDX.multiply((G3DReal)2 / nWidth);
	m_pixDY.copy(&v);
	m_pixDY.multiply((G3DReal)2 / nHeight);
	m_nY = nHeight - 1;
}

unsigned int GRayTraceScene::renderPixel(GRayTraceRay* pRay, G3DVector* pScreenPoint, G3DReal* pDistance)
{
	G3DVector directionVector(pScreenPoint);
	directionVector.subtract(m_pCamera->lookFromPoint());
	directionVector.normalize();
	pRay->Cast(this, m_pCamera->lookFromPoint(), &directionVector, m_pCamera->maxDepth());
	if(pDistance)
		m_pBoundingBoxTree->closestIntersection(m_pCamera->lookFromPoint(), &directionVector, pDistance);
	return pRay->m_color.color();
}

#define SQRT_RAYS_PER_PIXEL 6

unsigned int GRayTraceScene::renderPixelAntiAliassed(GRayTraceRay* pRay, G3DVector* pScreenPoint, G3DReal* pDistance)
{
	G3DVector jitter;
	int x, y;
	GRayTraceColor col;
	G3DReal focalDistance = m_pCamera->focalDistance();
	G3DReal r1, r2;
	for(y = 0; y < SQRT_RAYS_PER_PIXEL; y++)
	{
		for(x = 0; x < SQRT_RAYS_PER_PIXEL; x++)
		{
			G3DVector directionVector(pScreenPoint);

			// Jitter in X direction
			jitter.copy(&m_pixDX);
			jitter.multiply((G3DReal)(((double)x + m_pRand->uniform()) / SQRT_RAYS_PER_PIXEL - .5));
			directionVector.add(&jitter);

			// Jitter in Y direction
			jitter.copy(&m_pixDY);
			jitter.multiply((G3DReal)(((double)y + m_pRand->uniform()) / SQRT_RAYS_PER_PIXEL - .5));
			directionVector.add(&jitter);

			// Cast the ray
			directionVector.subtract(m_pCamera->lookFromPoint());
			if(focalDistance > 0)
			{
				// Use focus lens -- Start from a random point on the lens and fire at the focal point
				while(true)
				{
					r1 = (G3DReal)(m_pRand->uniform() - .5);
					r2 = (G3DReal)(m_pRand->uniform() - .5);
					if((r1 * r1) + (r2 * r2) <= .25)
						break;
				}
				G3DVector dx(m_pCamera->viewSideVector());
				dx.multiply(r1 * m_pCamera->lensDiameter());
				G3DVector dy(m_pCamera->viewUpVector());
				dy.multiply(r2 * m_pCamera->lensDiameter());
				directionVector.multiply(focalDistance);
				directionVector.subtract(&dx);
				directionVector.subtract(&dy);
				directionVector.normalize();
				G3DVector lensPoint(m_pCamera->lookFromPoint());
				lensPoint.add(&dx);
				lensPoint.add(&dy);
				pRay->Cast(this, &lensPoint, &directionVector, m_pCamera->maxDepth());
			}
			else
			{
				// Perfect focus -- just fire straight
				directionVector.normalize();
				pRay->Cast(this, m_pCamera->lookFromPoint(), &directionVector, m_pCamera->maxDepth());
			}

			// Make sure the color doesn't exceed pure white since it will be added with other measurements
			pRay->m_color.clip();
			col.add(&pRay->m_color);
		}
	}
	col.multiply((G3DReal)1 / (SQRT_RAYS_PER_PIXEL * SQRT_RAYS_PER_PIXEL));

	if(pDistance)
	{
		G3DVector directionVector(pScreenPoint);
		directionVector.subtract(m_pCamera->lookFromPoint());
		directionVector.normalize();
		m_pBoundingBoxTree->closestIntersection(m_pCamera->lookFromPoint(), &directionVector, pDistance);
	}

	return col.color();
}

unsigned int GRayTraceScene::renderPixelPathTrace(GRayTraceRay* pRay, G3DVector* pScreenPoint)
{
	G3DVector jitter;
	int x, y;
	GRayTraceColor col;
	G3DReal focalDistance = m_pCamera->focalDistance();
	G3DReal r1, r2;
	for(y = 0; y < SQRT_RAYS_PER_PIXEL; y++)
	{
		for(x = 0; x < SQRT_RAYS_PER_PIXEL; x++)
		{
			G3DVector directionVector(pScreenPoint);

			// Jitter in X direction
			jitter.copy(&m_pixDX);
			jitter.multiply((G3DReal)(((double)x + m_pRand->uniform()) / SQRT_RAYS_PER_PIXEL - .5));
			directionVector.add(&jitter);

			// Jitter in Y direction
			jitter.copy(&m_pixDY);
			jitter.multiply((G3DReal)(((double)y + m_pRand->uniform()) / SQRT_RAYS_PER_PIXEL - .5));
			directionVector.add(&jitter);

			// Cast the ray
			directionVector.subtract(m_pCamera->lookFromPoint());
			if(focalDistance > 0)
			{
				// Use focus lens -- Start from a random point on the lens and fire at the focal point
				while(true)
				{
					r1 = (G3DReal)(m_pRand->uniform() - .5);
					r2 = (G3DReal)(m_pRand->uniform() - .5);
					if((r1 * r1) + (r2 * r2) <= .25)
						break;
				}
				G3DVector dx(m_pCamera->viewSideVector());
				dx.multiply(r1 * m_pCamera->lensDiameter());
				G3DVector dy(m_pCamera->viewUpVector());
				dy.multiply(r2 * m_pCamera->lensDiameter());
				directionVector.multiply(focalDistance);
				directionVector.subtract(&dx);
				directionVector.subtract(&dy);
				directionVector.normalize();
				G3DVector lensPoint(m_pCamera->lookFromPoint());
				lensPoint.add(&dx);
				lensPoint.add(&dy);
				pRay->Trace(this, &lensPoint, &directionVector, m_pCamera->maxDepth(), true);
			}
			else
			{
				// Perfect focus -- just fire straight
				directionVector.normalize();
				pRay->Trace(this, m_pCamera->lookFromPoint(), &directionVector, m_pCamera->maxDepth(), true);
			}
			col.add(&pRay->m_color);
		}
	}
	col.multiply((G3DReal)1 / (SQRT_RAYS_PER_PIXEL * SQRT_RAYS_PER_PIXEL));

	// Apply tone mapping constant
	G3DReal luminance = col.r * (G3DReal)0.299 + col.g * (G3DReal)0.587 + col.b * (G3DReal)0.114;
	G3DReal desiredLuminance = (m_toneMappingConstant * luminance) / ((G3DReal)1.0 + m_toneMappingConstant * luminance);
	col.multiply(desiredLuminance / luminance);

	return col.color();
}

bool GRayTraceScene::renderLine()
{
	if(m_nY < 0)
		return false;
	GRayTraceRay ray(m_pRand);
	int x;
	G3DVector screenPoint(&m_pixSide);
	int nWidth = m_pImage->width();
	G3DReal distance;
	G3DReal* pDistance = m_pDistanceMap ? &distance : NULL;
	unsigned int col = 0;
	for(x = 0; x < nWidth; x++)
	{
//GAssert(x != 100 || m_nY != 143); // break
		switch(m_eMode)
		{
			case FAST_RAY_TRACE:
				col = renderPixel(&ray, &screenPoint, pDistance);
				break;
			case QUALITY_RAY_TRACE:
				col = renderPixelAntiAliassed(&ray, &screenPoint, pDistance);
				break;
			case PATH_TRACE:
				col = renderPixelPathTrace(&ray, &screenPoint);
				break;
			default:
				GAssert(false); // unrecognized case
		}
		m_pImage->setPixel(x, m_nY, col);
		if(pDistance)
			m_pDistanceMap[nWidth * m_nY + x] = distance;
		screenPoint.add(&m_pixDX);
	}
	m_pixSide.add(&m_pixDY);
	if(--m_nY >= 0)
		return true;
	else
		return false;
}

void GRayTraceScene::render()
{
	renderBegin();
	while(renderLine())
	{
	}
}

unsigned int GRayTraceScene::renderSinglePixel(int x, int y)
{
	// Init the rendering
	renderBegin();

	// Compute the screen point
	G3DVector screenPoint(&m_pixSide);
	m_pixDY.multiply((G3DReal)(m_pImage->height() - 1 - y));
	screenPoint.add(&m_pixDY);
	m_pixDX.multiply((G3DReal)(m_pImage->width() - 1 - x));
	screenPoint.add(&m_pixDX);

	// Cast the ray
	GRayTraceRay ray(m_pRand);
	unsigned int c = renderPixel(&ray, &screenPoint, NULL);
	return c;
}

// -----------------------------------------------------------------------------

GRayTraceLight::GRayTraceLight(G3DReal r, G3DReal g, G3DReal b)
: m_color(1, r, g, b)
{
}

GRayTraceLight::GRayTraceLight(GDomNode* pNode)
{
	m_color.deserialize(pNode->field("color"));
}

/*virtual*/ GRayTraceLight::~GRayTraceLight()
{
}

GDomNode* GRayTraceLight::baseDomNode(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "color", m_color.serialize(pDoc));
	pNode->addField(pDoc, "type", pDoc->newInt(lightType()));
	return pNode;
}

// static
GRayTraceLight* GRayTraceLight::deserialize(GDomNode* pNode, GRayTraceScene* pScene)
{
	switch((LightType)pNode->field("type")->asInt())
	{
		case Directional:
			return new GRayTraceDirectionalLight(pNode);
		case Point:
			return new GRayTracePointLight(pNode);
		case Area:
			return new GRayTraceAreaLight(pNode, pScene);
	}
	ThrowError("Unrecognized light type");
	return NULL;
}

// -----------------------------------------------------------------------------

GRayTraceDirectionalLight::GRayTraceDirectionalLight(G3DReal dx, G3DReal dy, G3DReal dz, G3DReal r, G3DReal g, G3DReal b, G3DReal jitter)
: GRayTraceLight(r, g, b), m_direction(dx, dy, dz), m_jitter(jitter)
{
}

GRayTraceDirectionalLight::GRayTraceDirectionalLight(GDomNode* pNode)
: GRayTraceLight(pNode)
{
	m_direction.deserialize(pNode->field("dir"));
	m_jitter = pNode->field("jit")->asDouble();
}

/*virtual*/ GRayTraceDirectionalLight::~GRayTraceDirectionalLight()
{
}

// virtual
GDomNode* GRayTraceDirectionalLight::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = baseDomNode(pDoc);
	pNode->addField(pDoc, "dir", m_direction.serialize(pDoc));
	pNode->addField(pDoc, "jit", pDoc->newDouble(m_jitter));
	return pNode;
}

/*virtual*/ void GRayTraceDirectionalLight::colorContribution(GRayTraceScene* pScene, GRayTraceRay* pRay, GRayTraceMaterial* pMaterial, bool bSpecular)
{
	G3DVector direction(&m_direction);
	if(m_jitter > 0)
		GRayTraceRay::JitterRay(&direction, m_jitter, pScene->rand());

	// Check if the point is in a shadow
	G3DReal distance;
	if(pScene->boundingBoxTree()->closestIntersection(&pRay->m_collisionPoint, &direction, &distance))
		return;

	// Compute diffuse component of the color
	GRayTraceColor diffuse(pMaterial->color(GRayTraceMaterial::Diffuse, pRay));
	diffuse.multiply(std::max((G3DReal)0, direction.dotProduct(&pRay->m_normalVector)));

	// Compute specular component of the color
	if(bSpecular)
	{
		G3DReal mag = (G3DReal)pow(std::max((G3DReal)0, pRay->m_reflectionVector.dotProduct(&direction)), pMaterial->specularExponent());
		GRayTraceColor specular(pMaterial->color(GRayTraceMaterial::Specular, pRay));
		specular.multiply(mag);
		diffuse.add(&specular);
	}

	// Multiply by light intensity
	diffuse.multiply(&m_color);
	pRay->m_color.add(&diffuse);
}

// -----------------------------------------------------------------------------


GRayTracePointLight::GRayTracePointLight(G3DReal x, G3DReal y, G3DReal z, G3DReal r, G3DReal g, G3DReal b, G3DReal jitter)
: GRayTraceLight(r, g, b), m_position(x, y, z), m_jitter(jitter)
{
}

GRayTracePointLight::GRayTracePointLight(GDomNode* pNode)
: GRayTraceLight(pNode)
{
	m_position.deserialize(pNode->field("pos"));
	m_jitter = pNode->field("jit")->asDouble();
}

/*virtual*/ GRayTracePointLight::~GRayTracePointLight()
{
}

// virtual
GDomNode* GRayTracePointLight::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = baseDomNode(pDoc);
	pNode->addField(pDoc, "pos", m_position.serialize(pDoc));
	pNode->addField(pDoc, "jit", pDoc->newDouble(m_jitter));
	return pNode;
}

/*virtual*/ void GRayTracePointLight::colorContribution(GRayTraceScene* pScene, GRayTraceRay* pRay, GRayTraceMaterial* pMaterial, bool bSpecular)
{
	G3DVector lightDirection(&m_position);
	if(m_jitter > 0)
	{
		// Jitter light position (to create soft shadows)
		lightDirection.m_vals[0] += (G3DReal)(m_jitter * 2 * pScene->rand()->uniform() - m_jitter);
		lightDirection.m_vals[1] += (G3DReal)(m_jitter * 2 * pScene->rand()->uniform() - m_jitter);
		lightDirection.m_vals[2] += (G3DReal)(m_jitter * 2 * pScene->rand()->uniform() - m_jitter);
	}

	// Check if the point is in a shadow
	lightDirection.subtract(&pRay->m_collisionPoint);
	double distsqared = lightDirection.squaredMag();
	lightDirection.normalize();
	G3DReal distance;
	if(pScene->boundingBoxTree()->closestIntersection(&pRay->m_collisionPoint, &lightDirection, &distance))
	{
		if((distance * distance) < distsqared)
			return;
	}

	// Compute diffuse component of the color
	GRayTraceColor diffuse(pMaterial->color(GRayTraceMaterial::Diffuse, pRay));
	diffuse.multiply(std::max((G3DReal)0, lightDirection.dotProduct(&pRay->m_normalVector)));

	// Compute specular component of the color
	if(bSpecular)
	{
		G3DReal mag = (G3DReal)pow(std::max((G3DReal)0, pRay->m_reflectionVector.dotProduct(&lightDirection)), pMaterial->specularExponent());
		GRayTraceColor specular(pMaterial->color(GRayTraceMaterial::Specular, pRay));
		specular.multiply(mag);
		diffuse.add(&specular);
	}

	// Multiply by light intensity
	diffuse.multiply(&m_color);
	diffuse.multiply((G3DReal)(1.0 / distsqared));
	pRay->m_color.add(&diffuse);
}

// -----------------------------------------------------------------------------

GRayTraceAreaLight::GRayTraceAreaLight(GRayTraceObject* pObject, G3DReal r, G3DReal g, G3DReal b)
: GRayTraceLight(r, g, b), m_pObject(pObject)
{
}

GRayTraceAreaLight::GRayTraceAreaLight(GDomNode* pNode, GRayTraceScene* pScene)
: GRayTraceLight(pNode)
{
	m_pObject = pScene->object((size_t)pNode->field("obj")->asInt());
}

/*virtual*/ GRayTraceAreaLight::~GRayTraceAreaLight()
{
}

// virtual
GDomNode* GRayTraceAreaLight::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = baseDomNode(pDoc);
	pNode->addField(pDoc, "obj", pDoc->newInt(pScene->objectIndex(m_pObject)));
	return pNode;
}

/*virtual*/ void GRayTraceAreaLight::colorContribution(GRayTraceScene* pScene, GRayTraceRay* pRay, GRayTraceMaterial* pMaterial, bool bSpecular)
{
	G3DVector lightDirection;
	if(m_pObject->type() == GRayTraceObject::Triangle)
	{
		// Pick a random point on the triangle
		GRayTraceTriangle* pTri = (GRayTraceTriangle*)m_pObject;
		G3DVector u(pTri->vertex(1));
		u.subtract(pTri->vertex(0));
		G3DVector v(pTri->vertex(2));
		v.subtract(pTri->vertex(0));
		double a = pScene->rand()->uniform() * 2 - 1;
		double b = pScene->rand()->uniform() * 2 - 1;
		if(a + b > 1)
		{
			a = 1 - a;
			b = 1 - b;
		}
		u.multiply((G3DReal)a);
		v.multiply((G3DReal)b);
		lightDirection.copy(pTri->vertex(0));
		lightDirection.add(&u);
		lightDirection.add(&v);
	}
	else if(m_pObject->type() == GRayTraceObject::Sphere)
	{
		// Pick a random point on a disc facing the point of collision
		GRayTraceSphere* pSphere = (GRayTraceSphere*)m_pObject;
		G3DVector u, v;
		G3DVector N(pSphere->center());
		N.subtract(&pRay->m_collisionPoint);
		N.normalize();
		if(std::abs(N.m_vals[0]) < 0.5)
		{
			v.set(1, 0, 0);
			u.crossProduct(&N, &v);
		}
		else 
		{
			v.set(0, 1, 0);
			u.crossProduct(&N, &v);
		}
		u.normalize();
		v.crossProduct(&u, &N);
		double a, b;
		while(true)
		{
			a = pScene->rand()->uniform() * 2 - 1;
			b = pScene->rand()->uniform() * 2 - 1;
			if((a * a) + (b * b) < 1)
				break;
		}
		u.multiply((G3DReal)a);
		v.multiply((G3DReal)b);
		lightDirection.copy(pSphere->center());
		lightDirection.add(&u);
		lightDirection.add(&v);
	}
	else
	{
		GAssert(false); // Unrecognized object type
	}

	// Check if the point is in a shadow
	lightDirection.subtract(&pRay->m_collisionPoint);
	double distsqared = lightDirection.squaredMag();
	lightDirection.normalize();
	G3DReal distance;
	GRayTraceObject* pObj = pScene->boundingBoxTree()->closestIntersection(&pRay->m_collisionPoint, &lightDirection, &distance);
	if(pObj != m_pObject && pObj && distance * distance < distsqared)
		return;

	GRayTraceColor diffuse(&m_color);
	diffuse.multiply(pMaterial->color(GRayTraceMaterial::Diffuse, pRay));
	if(m_pObject->type() == GRayTraceObject::Triangle)
	{
		GRayTraceTriangle* pTri = (GRayTraceTriangle*)m_pObject;
		G3DVector u(pTri->vertex(1));
		u.subtract(pTri->vertex(0));
		G3DVector v(pTri->vertex(2));
		v.subtract(pTri->vertex(0));
		G3DVector t;
		t.crossProduct(&u, &v);
		G3DReal triangleArea = (G3DReal)sqrt(t.squaredMag()) / 2;
		G3DVector triangleNormal;
		triangleNormal.triangleNormal(pTri->vertex(0), pTri->vertex(1), pTri->vertex(2));
		diffuse.multiply(std::abs(pRay->m_normalVector.dotProduct(&lightDirection) * triangleNormal.dotProduct(&lightDirection) * triangleArea / (G3DReal)(M_PI * distsqared)));
	}
	else
	{
		GRayTraceSphere* pSphere = (GRayTraceSphere*)m_pObject;
		diffuse.multiply(pRay->m_normalVector.dotProduct(&lightDirection)  * pSphere->radius() * pSphere->radius() / (G3DReal)distsqared);
	}

	pRay->m_color.add(&diffuse);
}

// -----------------------------------------------------------------------------

GRayTraceMaterial::GRayTraceMaterial()
{
}

// virtual
GRayTraceMaterial::~GRayTraceMaterial()
{
}

// static
GRayTraceMaterial* GRayTraceMaterial::deserialize(GDomNode* pNode)
{
	switch((MaterialType)pNode->field("type")->asInt())
	{
		case Physical:
			return new GRayTracePhysicalMaterial(pNode);
		case Image:
			return new GRayTraceImageTexture(pNode);
		case Etherial:
			ThrowError("Sorry, not implemented yet");
	}
	return NULL;
}

void GRayTraceMaterial::computeColor(GRayTraceScene* pScene, GRayTraceRay* pRay, bool bAmbient, bool bSpecular)
{
	if(bAmbient)
	{
		pRay->m_color.copy(pScene->ambientLight());
		pRay->m_color.multiply(color(Ambient, pRay));
	}
	else
		pRay->m_color.set(0, 0, 0, 0);

	// Real lights
	for(size_t n = 0; n < pScene->lightCount(); n++)
		pScene->light(n)->colorContribution(pScene, pRay, this, bSpecular);
}




GRayTracePhysicalMaterial::GRayTracePhysicalMaterial()
{
	setColor(Diffuse, (G3DReal).5, (G3DReal).5, (G3DReal).5);
	setColor(Specular, 1, 1, 1);
	setColor(Reflective, 0, 0, 0);
	setColor(Transmissive, 0, 0, 0);
	setColor(Ambient, (G3DReal).1, (G3DReal).1, (G3DReal).1);
	setColor(Emissive, 0, 0, 0);
	m_indexOfRefraction = 1;
	m_specularExponent = 1;
	m_glossiness = 0;
	m_cloudiness = 0;
}

GRayTracePhysicalMaterial::GRayTracePhysicalMaterial(GDomNode* pNode)
{
	GDomListIterator it(pNode->field("colors"));
	for(int i = 0; i < Color_Type_Count && it.current(); i++)
	{
		m_colors[i].deserialize(it.current());
		it.advance();
	}
	m_indexOfRefraction = pNode->field("iof")->asDouble();
	m_specularExponent = pNode->field("se")->asDouble();
	m_glossiness = pNode->field("glossiness")->asDouble();
	m_cloudiness = pNode->field("cloudiness")->asDouble();
}

// virtual
GRayTracePhysicalMaterial::~GRayTracePhysicalMaterial()
{
}

// virtual
GDomNode* GRayTracePhysicalMaterial::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "type", pDoc->newInt(materialType()));
	GDomNode* pColors = pNode->addField(pDoc, "colors", pDoc->newList());
	for(int i = 0; i < Color_Type_Count; i++)
		pColors->addItem(pDoc, m_colors[i].serialize(pDoc));
	pNode->addField(pDoc, "iof", pDoc->newDouble(m_indexOfRefraction));
	pNode->addField(pDoc, "se", pDoc->newDouble(m_specularExponent));
	pNode->addField(pDoc, "glossiness", pDoc->newDouble(m_glossiness));
	pNode->addField(pDoc, "cloudiness", pDoc->newDouble(m_cloudiness));
	return pNode;
}

// virtual
bool GRayTracePhysicalMaterial::isSame(GRayTraceMaterial* pOther)
{
	GRayTracePhysicalMaterial* pThat = (GRayTracePhysicalMaterial*)pOther;
	for(int i = 0; i < Color_Type_Count; i++)
	{
		if(std::abs(pThat->m_colors[i].r - m_colors[i].r) > 1e-6)
			return false;
		if(std::abs(pThat->m_colors[i].g - m_colors[i].g) > 1e-6)
			return false;
		if(std::abs(pThat->m_colors[i].b - m_colors[i].b) > 1e-6)
			return false;
	}
	if(std::abs(pThat->m_indexOfRefraction - m_indexOfRefraction) > 1e-6)
		return false;
	if(std::abs(pThat->m_specularExponent - m_specularExponent) > 1e-6)
		return false;
	if(std::abs(pThat->m_glossiness - m_glossiness) > 1e-6)
		return false;
	if(std::abs(pThat->m_cloudiness - m_cloudiness) > 1e-6)
		return false;
	return true;
}

// virtual
GRayTraceMaterial* GRayTracePhysicalMaterial::copy()
{
	GRayTracePhysicalMaterial* pCopy = new GRayTracePhysicalMaterial();
	for(int i = 0; i < Color_Type_Count; i++)
		pCopy->m_colors[i] = m_colors[i];
	pCopy->m_indexOfRefraction = m_indexOfRefraction;
	pCopy->m_specularExponent = m_specularExponent;
	pCopy->m_glossiness = m_glossiness;
	pCopy->m_cloudiness = m_cloudiness;
	return pCopy;
}

// virtual
GRayTraceColor* GRayTracePhysicalMaterial::color(ColorType eType, GRayTraceRay* pRay)
{
	return &m_colors[eType];
}

void GRayTracePhysicalMaterial::setColor(ColorType eType, GRayTraceColor* pCol)
{
	m_colors[eType].copy(pCol);
}

void GRayTracePhysicalMaterial::setColor(ColorType eType, G3DReal r, G3DReal g, G3DReal b)
{
	if(eType == Emissive)
		m_colors[eType].set(1, r, g, b);
	else
		m_colors[eType].set(1,
				std::max((G3DReal)0, std::min((G3DReal)1, r)),
				std::max((G3DReal)0, std::min((G3DReal)1, g)),
				std::max((G3DReal)0, std::min((G3DReal)1, b))
			);
}






GRayTraceImageTexture::GRayTraceImageTexture()
{
	m_pTextureImage = NULL;
	m_bDeleteTextureImage = false;
}

GRayTraceImageTexture::GRayTraceImageTexture(GDomNode* pNode)
{
	ThrowError("Sorry, deserializing images not yet supported");
}

// virtual
GRayTraceImageTexture::~GRayTraceImageTexture()
{
	if(m_bDeleteTextureImage)
		delete(m_pTextureImage);
}

// virtual
GDomNode* GRayTraceImageTexture::serialize(GDom* pDoc)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "type", pDoc->newInt(materialType()));
	ThrowError("Sorry, serializing images not yet supported");
	return pNode;
}

// virtual
bool GRayTraceImageTexture::isSame(GRayTraceMaterial* pThat)
{
	ThrowError("Sorry, not implemented yet");
	return false;
}

// virtual
GRayTraceMaterial* GRayTraceImageTexture::copy()
{
	ThrowError("Sorry, not implemented yet");
	return NULL;
}

void GRayTraceImageTexture::setTextureImage(GImage* pImage, bool bDeleteImage)
{
	if(m_bDeleteTextureImage)
		delete(m_pTextureImage);
	m_pTextureImage = pImage;
	m_bDeleteTextureImage = bDeleteImage;
}

GRayTraceColor* GRayTraceImageTexture::color(ColorType eType, GRayTraceRay* pRay)
{
	if(eType == Diffuse)
	{
		if(m_pTextureImage)
		{
			GAssert(pRay->DidRayHitTexture()); // ray doesn't know about this
			G3DReal x, y;
			pRay->GetTextureCoords(&x, &y);
			y = m_pTextureImage->height() - y;
			m_col.set(m_pTextureImage->interpolatePixel((float)x, (float)y));
		}
		else
			m_col.set(1, 0, 1, 1); // if you see cyan, you forgot to set the texture image
		return &m_col;
	}
	else
	{
		m_col.set(1, 0, 0, 0);
		return &m_col;
	}
}


// -----------------------------------------------------------------------------

class Ray_Trace_Object_Comparing_Functor
{
protected:
	int m_dim;

public:
	Ray_Trace_Object_Comparing_Functor(int dim) : m_dim(dim)
	{
	}

	bool operator() (GRayTraceObject* pA, GRayTraceObject* pB) const
	{
		G3DVector a, b;
		pA->center(&a);
		pB->center(&b);
		return (a.m_vals[m_dim] < b.m_vals[m_dim]);
	}
};

//static
GRayTraceBoundingBoxBase* GRayTraceBoundingBoxBase::BuildTree(vector<GRayTraceObject*>& objects)
{
	// Make a leaf node if we can
	if(objects.size() <= MAX_OBJECTS_PER_BOUNDING_BOX)
		return new GRayTraceBoundingBoxLeaf(objects);

	// Find the biggest dimension
	double dBiggestRange = 0;
	int nBiggestDim = -1;
	double dRange;
	int i;
	GRayTraceObject* pFirst;
	GRayTraceObject* pLast;
	G3DVector vFirst, vLast;
	for(i = 0; i < 3; i++)
	{
		Ray_Trace_Object_Comparing_Functor comparer(i);
		std::sort(objects.begin(), objects.end(), comparer);
		pFirst = objects[0];
		pLast = objects[objects.size() - 1];
		pFirst->center(&vFirst);
		pLast->center(&vLast);
		dRange = vLast.m_vals[i] - vFirst.m_vals[i];
		if(dRange > dBiggestRange)
		{
			nBiggestDim = i;
			dBiggestRange = dRange;
		}
	}

	// Split into two arrays
	if(nBiggestDim != 2)
	{
		Ray_Trace_Object_Comparing_Functor comparer(nBiggestDim);
		std::sort(objects.begin(), objects.end(), comparer);
	}
	int nSize = (int)objects.size();
	int nLesserSize = nSize / 2;
	vector<GRayTraceObject*> other;
	other.reserve(nSize - nLesserSize);
	for(i = nSize - 1; i >= nLesserSize; i--)
	{
		other.push_back(objects[objects.size() - 1]);
		objects.pop_back();
	}

	// Make an interior node
	GRayTraceBoundingBoxBase* pLesser = BuildTree(objects);
	GRayTraceBoundingBoxBase* pGreater = BuildTree(other);
	return new GRayTraceBoundingBoxInterior(pLesser, pGreater);
}

//static
GRayTraceBoundingBoxBase* GRayTraceBoundingBoxBase::makeBoundingBoxTree(GRayTraceScene* pScene)
{
	// Make a copy of the object list
	vector<GRayTraceObject*> objects;
	objects.reserve(pScene->objectCount());
	for(size_t i = 0; i < pScene->objectCount(); i++)
		objects.push_back(pScene->object(i));

	// Build the tree
	return BuildTree(objects);
}

bool GRayTraceBoundingBoxBase::DoesRayHitBox(G3DVector* pRayOrigin, G3DVector* pDirectionVector)
{
	G3DVector point;
	if(pDirectionVector->m_vals[0] != 0)
	{
		point.copy(pDirectionVector);
		point.multiply((m_min.m_vals[0] - pRayOrigin->m_vals[0]) / pDirectionVector->m_vals[0]);
		point.add(pRayOrigin);
		if(point.m_vals[1] >= m_min.m_vals[1] && point.m_vals[2] >= m_min.m_vals[2] &&
			point.m_vals[1] <= m_max.m_vals[1] && point.m_vals[2] <= m_max.m_vals[2])
			return true;
		point.copy(pDirectionVector);
		point.multiply((m_max.m_vals[0] - pRayOrigin->m_vals[0]) / pDirectionVector->m_vals[0]);
		point.add(pRayOrigin);
		if(point.m_vals[1] >= m_min.m_vals[1] && point.m_vals[2] >= m_min.m_vals[2] &&
			point.m_vals[1] <= m_max.m_vals[1] && point.m_vals[2] <= m_max.m_vals[2])
			return true;
	}
	if(pDirectionVector->m_vals[1] != 0)
	{
		point.copy(pDirectionVector);
		point.multiply((m_min.m_vals[1] - pRayOrigin->m_vals[1]) / pDirectionVector->m_vals[1]);
		point.add(pRayOrigin);
		if(point.m_vals[0] >= m_min.m_vals[0] && point.m_vals[2] >= m_min.m_vals[2] &&
			point.m_vals[0] <= m_max.m_vals[0] && point.m_vals[2] <= m_max.m_vals[2])
			return true;
		point.copy(pDirectionVector);
		point.multiply((m_max.m_vals[1] - pRayOrigin->m_vals[1]) / pDirectionVector->m_vals[1]);
		point.add(pRayOrigin);
		if(point.m_vals[0] >= m_min.m_vals[0] && point.m_vals[2] >= m_min.m_vals[2] &&
			point.m_vals[0] <= m_max.m_vals[0] && point.m_vals[2] <= m_max.m_vals[2])
			return true;
	}
	if(pDirectionVector->m_vals[2] != 0)
	{
		point.copy(pDirectionVector);
		point.multiply((m_min.m_vals[2] - pRayOrigin->m_vals[2]) / pDirectionVector->m_vals[2]);
		point.add(pRayOrigin);
		if(point.m_vals[0] >= m_min.m_vals[0] && point.m_vals[1] >= m_min.m_vals[1] &&
			point.m_vals[0] <= m_max.m_vals[0] && point.m_vals[1] <= m_max.m_vals[1])
			return true;
		point.copy(pDirectionVector);
		point.multiply((m_max.m_vals[2] - pRayOrigin->m_vals[2]) / pDirectionVector->m_vals[2]);
		point.add(pRayOrigin);
		if(point.m_vals[0] >= m_min.m_vals[0] && point.m_vals[1] >= m_min.m_vals[1] &&
			point.m_vals[0] <= m_max.m_vals[0] && point.m_vals[1] <= m_max.m_vals[1])
			return true;
	}
	return false;
}

// -----------------------------------------------------------------------------

// virtual
GRayTraceObject* GRayTraceBoundingBoxInterior::closestIntersection(G3DVector* pRayOrigin, G3DVector* pDirectionVector, G3DReal* pOutDistance)
{
	if(!DoesRayHitBox(pRayOrigin, pDirectionVector))
		return NULL;
	G3DReal lesserDist = 0;
	G3DReal greaterDist = 0;
	GRayTraceObject* pLesser = m_pLesser->closestIntersection(pRayOrigin, pDirectionVector, &lesserDist);
	GRayTraceObject* pGreater = m_pGreater->closestIntersection(pRayOrigin, pDirectionVector, &greaterDist);
	if(!pGreater)
	{
		*pOutDistance = lesserDist;
		return pLesser;
	}
	if(!pLesser)
	{
		*pOutDistance = greaterDist;
		return pGreater;
	}
	if(lesserDist < greaterDist)
	{
		*pOutDistance = lesserDist;
		return pLesser;
	}
	else
	{
		*pOutDistance = greaterDist;
		return pGreater;
	}
}

// -----------------------------------------------------------------------------

GRayTraceBoundingBoxLeaf::GRayTraceBoundingBoxLeaf(vector<GRayTraceObject*>& objects)
{
	m_nObjectCount = (int)objects.size();
	m_pObjects = new GRayTraceObject*[m_nObjectCount];
	m_min.set((G3DReal)1e30, (G3DReal)1e30, (G3DReal)1e30);
	m_max.set((G3DReal)-1e30, (G3DReal)-1e30, (G3DReal)-1e30);
	int i;
	for(i = 0; i < m_nObjectCount; i++)
	{
		m_pObjects[i] = objects[i];
		m_pObjects[i]->adjustBoundingBox(&m_min, &m_max);
	}
}

//virtual
GRayTraceBoundingBoxLeaf::~GRayTraceBoundingBoxLeaf()
{
	delete[] m_pObjects;
}

// virtual
GRayTraceObject* GRayTraceBoundingBoxLeaf::closestIntersection(G3DVector* pRayOrigin, G3DVector* pDirectionVector, G3DReal* pOutDistance)
{
	if(!DoesRayHitBox(pRayOrigin, pDirectionVector))
		return NULL;

	// Find the closest intersection
	G3DReal distance;
	G3DReal closestDistance = (G3DReal)1e30;
	GRayTraceObject* pClosestObject = NULL;
	int n;
	for(n = 0; n < m_nObjectCount; n++)
	{
		distance = m_pObjects[n]->rayDistance(pRayOrigin, pDirectionVector);
		if(distance < closestDistance && distance > MIN_RAY_DISTANCE)
		{
			closestDistance = distance;
			pClosestObject = m_pObjects[n];
		}
	}
	*pOutDistance = closestDistance;
	return pClosestObject;
}

// -----------------------------------------------------------------------------

GRayTraceTriMesh::GRayTraceTriMesh(GRayTraceMaterial* pMaterial, size_t nPoints, size_t nTriangles, size_t nNormals, size_t nTextureCoords)
{
	m_pMaterial = pMaterial;
	m_nPoints = nPoints;
	m_pPoints = new G3DVector[nPoints];
	m_nTriangles = nTriangles;
	m_pTriangles = new size_t[3 * nTriangles];
	if(nNormals > 0)
	{
		if(nNormals != nPoints)
			throw "The number of normals is not equal to the number of vertices";
		m_pNormals = new G3DVector[nPoints];
	}
	else
		m_pNormals = NULL;
	if(nTextureCoords > 0)
	{
		if(nTextureCoords != nPoints)
			throw "The number of texture coords is not equal to the number of vertices";
		m_pTextureCoords = new G3DReal[2 * nPoints];
	}
	else
		m_pTextureCoords = NULL;
	m_bCulling = false;
}

GRayTraceTriMesh::GRayTraceTriMesh(GDomNode* pNode, GRayTraceScene* pScene)
{
	m_pMaterial = pScene->material((size_t)pNode->field("material")->asInt());
	GDomNode* pPointsNode = pNode->field("points");
	GDomNode* pTrianglesNode = pNode->field("triangles");
	GDomNode* pNormalsNode = pNode->fieldIfExists("normals");
	GDomNode* pCoordsNode = pNode->fieldIfExists("coords");
	GDomListIterator it1(pPointsNode);
	m_nPoints = it1.remaining();
	m_pPoints = new G3DVector[m_nPoints];
	for(size_t i = 0; it1.current(); i++)
	{
		m_pPoints[i].deserialize(it1.current());
		it1.advance();
	}
	GDomListIterator it2(pTrianglesNode);
	if(it2.remaining() % 3 != 0)
		ThrowError("triangle points are not a multiple of 3");
	m_nTriangles = it2.remaining() / 3;
	m_pTriangles = new size_t[it2.remaining()];
	for(size_t i = 0; it2.current(); i++)
	{
		m_pTriangles[i] = (size_t)it2.current()->asInt();
		it2.advance();
	}
	if(pNormalsNode)
	{
		GDomListIterator it3(pNormalsNode);
		if(it3.remaining() != (size_t)m_nPoints)
			ThrowError("The number of normals must match the number of vertexes");
		m_pNormals = new G3DVector[m_nPoints];
		for(size_t i = 0; it3.current(); i++)
		{
			m_pNormals[i].deserialize(it3.current());
			it3.advance();
		}
	}
	else
		m_pNormals = NULL;
	if(pCoordsNode)
	{
		GDomListIterator it4(pCoordsNode);
		if(it4.remaining() != 2 * (size_t)m_nPoints)
			ThrowError("The number of texture coords must be double the number of vertexes");
		m_pTextureCoords = new G3DReal[2 * m_nPoints];
		for(size_t i = 0; it4.current(); i++)
		{
			m_pTextureCoords[i] = it4.current()->asDouble();
			it4.advance();
		}
	}
	else
		m_pTextureCoords = NULL;
	m_bCulling = pNode->field("culling")->asBool();
}

/*virtual*/ GRayTraceTriMesh::~GRayTraceTriMesh()
{
	delete[] m_pPoints;
	delete[] m_pTriangles;
	delete[] m_pNormals;
	delete[] m_pTextureCoords;
}

GDomNode* GRayTraceTriMesh::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "material", pDoc->newInt(pScene->materialIndex(m_pMaterial)));
	GDomNode* pPointsNode = pNode->addField(pDoc, "points", pDoc->newList());
	for(size_t i = 0; i < m_nPoints; i++)
		pPointsNode->addItem(pDoc, m_pPoints[i].serialize(pDoc));
	GDomNode* pTrianglesNode = pNode->addField(pDoc, "triangles", pDoc->newList());
	int j = 0;
	for(size_t i = 0; i < m_nTriangles; i++)
	{
		pTrianglesNode->addItem(pDoc, pDoc->newInt(m_pTriangles[j]));
		j++;
		pTrianglesNode->addItem(pDoc, pDoc->newInt(m_pTriangles[j]));
		j++;
		pTrianglesNode->addItem(pDoc, pDoc->newInt(m_pTriangles[j]));
		j++;
	}
	if(m_pNormals)
	{
		GDomNode* pNormalsNode = pNode->addField(pDoc, "normals", pDoc->newList());
		for(size_t i = 0; i < m_nPoints; i++)
			pNormalsNode->addItem(pDoc, m_pNormals[i].serialize(pDoc));
	}
	if(m_pTextureCoords)
	{
		GDomNode* pCoordsNode = pNode->addField(pDoc, "coords", pDoc->newList());
		for(size_t i = 0; i < 2 * m_nPoints; i++)
			pCoordsNode->addItem(pDoc, pDoc->newDouble(m_pTextureCoords[i]));
	}
	pNode->addField(pDoc, "culling", pDoc->newBool(m_bCulling));
	return pNode;
}

void GRayTraceTriMesh::setPoint(size_t nIndex, const G3DVector* pPoint)
{
	GAssert(nIndex < m_nPoints); // out of range
	m_pPoints[nIndex] = *pPoint;
}

void GRayTraceTriMesh::setTriangle(size_t nIndex, size_t v1, size_t v2, size_t v3)
{
	GAssert(nIndex < m_nTriangles); // out of range
	size_t* pTri = &m_pTriangles[3 * nIndex];
	pTri[0] = v1;
	pTri[1] = v2;
	pTri[2] = v3;
}

void GRayTraceTriMesh::setNormal(size_t nIndex, G3DVector* pNormal)
{
	GAssert(nIndex < m_nPoints); // out of range
	m_pNormals[nIndex] = *pNormal;
}

class EdgeRecord : public HashTableNode
{
protected:
	int m_v1;
	int m_v2;
	vector<int> m_triangles;

public:
	EdgeRecord(int v1, int v2) : HashTableNode()
	{
		SetEdge(v1, v2);
	}

	virtual ~EdgeRecord()
	{
	}

	vector<int>& GetTriangles() { return m_triangles; }

	void SetEdge(int v1, int v2)
	{
		if(v1 < v2)
		{
			m_v1 = v1;
			m_v2 = v2;
		}
		else
		{
			m_v1 = v2;
			m_v2 = v1;
		}
	}

	virtual unsigned int Hash(int nBucketCount)
	{
		return (m_v1 * 7387 + m_v2) % nBucketCount;
	}

	virtual bool Equals(HashTableNode* pThat)
	{
		EdgeRecord* pOther = (EdgeRecord*)pThat;
		return ((m_v1 == pOther->m_v1) && (m_v2 == pOther->m_v2));
	}
	
	void AddTriangle(int i)
	{
		m_triangles.push_back(i);
	}
};
/*
void GRayTraceTriMesh::ComputeTriangleNeighborSides(GIntQueue* pQ, GNodeHashTable* pEdges, int* pFaces, int i)
{
	GAssert(pFaces[i] != 0); // Expected to already know the facing direction of triangle i
	EdgeRecord tmp(-1, -1);
	int j, l, n;
	for(j = 0; j < 3; j++) // j = which side of the main triangle
	{
		tmp.SetEdge(m_pTriangles[3 * i + j], m_pTriangles[3 * i + ((j + 1) % 3)]);
		EdgeRecord* pEdge = (EdgeRecord*)pEdges->Get(&tmp);
		GAssert(pEdge); // No record of this edge
		vector<int>& triangles = pEdge->GetTriangles();
		for(size_t k = 0; k < triangles.size(); k++) // k = which neighbor index
		{
			n = triangles[k];
			if(n == i)
				continue; // It's the same triangle
			if(pFaces[n] != 0)
				continue; // That triangle has already been done

			// Find the first matching vertex
			for(l = 0; l < 3; l++) // l = which vertex on the neighbor
			{
				if(m_pTriangles[3 * n + l] == m_pTriangles[3 * i + j])
					break;
			}
			GAssert(l < 3); // Expected neighbor to have matching vertex

			// Determine whether the triangles face the same way
			if(m_pTriangles[3 * n + ((l + 1) % 3)] == m_pTriangles[3 * i + ((j + 1) % 3)])
				pFaces[n] = -pFaces[i];
			else
			{
				GAssert(m_pTriangles[3 * n + ((l + 2) % 3)] == m_pTriangles[3 * i + ((j + 1) % 3)]); // Expected a matching side
				pFaces[n] = pFaces[i];
			}

			// Add the neighbor to the queue
			pQ->Push(n);
		}
	}

}
*/
class GTriangleIndexArray
{
public:
	vector<size_t> m_triangles;
};

void GRayTraceTriMesh::computePhongNormals()
{
	// Make lists of all the triangles that touch each vertex
	vector<GTriangleIndexArray> arrVertexTriangles;
	arrVertexTriangles.resize(m_nPoints);
	for(size_t i = 0; i < m_nTriangles; i++)
	{
		for(size_t j = 0; j < 3; j++)
			arrVertexTriangles[m_pTriangles[3 * i + j]].m_triangles.push_back(i);
	}

	// Make the vertex normals
	size_t nTri;
	delete[] m_pNormals;
	m_pNormals = new G3DVector[m_nPoints];
	G3DVector triNorm;
	for(size_t i = 0; i < m_nPoints; i++)
	{
		m_pNormals[i].set(0, 0, 0);
		for(size_t j = 0; j < arrVertexTriangles[i].m_triangles.size(); j++)
		{
			nTri = arrVertexTriangles[i].m_triangles[j];
			GAssert(nTri >= 0 && nTri < m_nTriangles); // out of range
			triNorm.triangleNormal(
					&m_pPoints[m_pTriangles[3 * nTri]],
					&m_pPoints[m_pTriangles[3 * nTri + 1]],
					&m_pPoints[m_pTriangles[3 * nTri + 2]]
				);
			// todo: scale each component vector by the area of the triangle that contributes it?
			m_pNormals[i].add(&triNorm);
		}
		m_pNormals[i].normalize();
	}
}

void GRayTraceTriMesh::setTextureCoord(size_t nIndex, G3DReal x, G3DReal y)
{
	GAssert(nIndex < m_nPoints); // out of range
	nIndex *= 2;
	m_pTextureCoords[nIndex] = x;
	m_pTextureCoords[nIndex + 1] = y;
}

bool GRayTraceTriMesh::isPointWithinPlanarPolygon(G3DVector* pPoint, G3DVector** ppVertices, size_t nVertices)
{
	// Find the two dimensions with the most significant component (which
	// are the dimensions with the smallest component in the normal vector)
	GAssert(nVertices >= 3); // at least three points are needed to define a planar polygon
	G3DVector plane;
	plane.triangleNormal(ppVertices[0], ppVertices[1], ppVertices[2]);
	plane.m_vals[0] = std::abs(plane.m_vals[0]);
	plane.m_vals[1] = std::abs(plane.m_vals[1]);
	plane.m_vals[2] = std::abs(plane.m_vals[2]);
	int d1, d2;
	if(plane.m_vals[0] >= plane.m_vals[1] && plane.m_vals[0] >= plane.m_vals[2])
	{
		d1 = 1;
		d2 = 2;
	}
	else if(plane.m_vals[1] >= plane.m_vals[0] && plane.m_vals[1] >= plane.m_vals[2])
	{
		d1 = 0;
		d2 = 2;
	}
	else
	{
		d1 = 0;
		d2 = 1;
	}

	// Count the number of times a ray shot out from the point crosses
	// a side of the polygon
	int numCrossings = 0;
	int signHolder, nextSignHolder;
	if(ppVertices[0]->m_vals[d2] - pPoint->m_vals[d2] < 0)
		signHolder = -1;
	else
		signHolder = 1;
	size_t nNextVertex;
	G3DReal u0, u1, v0, v1;
	for(size_t nVertex = 0; nVertex < nVertices; nVertex++)
	{
		nNextVertex = (nVertex + 1) % nVertices;
		v1 = ppVertices[nNextVertex]->m_vals[d2] - pPoint->m_vals[d2];
		if(v1 < 0)
			nextSignHolder = -1;
		else
			nextSignHolder = 1;
		if(signHolder != nextSignHolder)
		{
			u0 = ppVertices[nVertex]->m_vals[d1] - pPoint->m_vals[d1];
			u1 = ppVertices[nNextVertex]->m_vals[d1] - pPoint->m_vals[d1];
			v0 = ppVertices[nVertex]->m_vals[d2] - pPoint->m_vals[d2];
			if(u0 - v0 * (u1 - u0) / (v1 - v0) > 0)
				numCrossings++;
		}
		signHolder = nextSignHolder;
	}
	if(numCrossings & 1)
		return true;
	else
		return false;
}

G3DReal GRayTraceTriMesh::rayDistanceToTriangle(size_t nTriangle, G3DVector* pRayOrigin, G3DVector* pRayDirection)
{
	// Compute the plane equasion Ax + By + Cz + D = 0
	size_t* pTriangle = &m_pTriangles[3 * nTriangle];
	G3DVector plane; // The plane normal is the vector (A, B, C)
	G3DReal d;
	plane.planeEquation(&m_pPoints[pTriangle[0]], &m_pPoints[pTriangle[1]], &m_pPoints[pTriangle[2]], &d);

	// Compute distance and point of intersection
	G3DReal tmp = plane.dotProduct(pRayDirection);
	if(tmp >= 0)
	{
		if(tmp == 0)
			return 0; // the ray is paralell to the plane
		if(m_bCulling)
			return 0; // the ray hits the back side of the plane
		else
		{
			// Reverse the plane normal
			plane.multiply(-1);
			d = -d;
			tmp = -tmp;
		}
	}
	G3DReal distance = -(plane.dotProduct(pRayOrigin) + d) / tmp;
	if(distance <= 0)
		return 0; // the intersection point is behind the ray origin
	G3DVector point(pRayDirection);
	point.multiply(distance);
	point.add(pRayOrigin);

	// Determine if the intersection point is within the triangle
	G3DVector* pVertices[3];
	pVertices[0] = &m_pPoints[pTriangle[0]];
	pVertices[1] = &m_pPoints[pTriangle[1]];
	pVertices[2] = &m_pPoints[pTriangle[2]];
	if(!isPointWithinPlanarPolygon(&point, pVertices, 3))
		return 0; // the ray misses the triangle
	return distance;
}

void GRayTraceTriMesh::normalVector(GRayTraceRay* pRay, size_t nIndex)
{
	size_t* pTriangle = &m_pTriangles[3 * nIndex];
	pRay->m_normalVector.triangleNormal(&m_pPoints[pTriangle[0]], &m_pPoints[pTriangle[1]], &m_pPoints[pTriangle[2]]);
	if(!m_pNormals && !m_pTextureCoords)
		return;

	// Find the two most significant dimensions
	int i1,i2;
	G3DReal xx, yy, zz;
	xx = pRay->m_normalVector.m_vals[0] * pRay->m_normalVector.m_vals[0];
	yy = pRay->m_normalVector.m_vals[1] * pRay->m_normalVector.m_vals[1];
	zz = pRay->m_normalVector.m_vals[2] * pRay->m_normalVector.m_vals[2];
	if(xx >= yy && xx >= zz)
	{
		i1 = 1;
		i2 = 2;
	}
	else if(yy >= zz)
	{
		i1 = 0;
		i2 = 2;
	}
	else
	{
		i1 = 0;
		i2 = 1;
	}

	// Find the weights alpha, beta, and gamma
	G3DReal u1 = m_pPoints[pTriangle[1]].m_vals[i1] - m_pPoints[pTriangle[0]].m_vals[i1];
	G3DReal u2 = m_pPoints[pTriangle[2]].m_vals[i1] - m_pPoints[pTriangle[0]].m_vals[i1];
	G3DReal v1 = m_pPoints[pTriangle[1]].m_vals[i2] - m_pPoints[pTriangle[0]].m_vals[i2];
	G3DReal v2 = m_pPoints[pTriangle[2]].m_vals[i2] - m_pPoints[pTriangle[0]].m_vals[i2];
	G3DReal u0 = pRay->m_collisionPoint.m_vals[i1] - m_pPoints[pTriangle[0]].m_vals[i1];
	G3DReal v0 = pRay->m_collisionPoint.m_vals[i2] - m_pPoints[pTriangle[0]].m_vals[i2];
	G3DReal beta  = (u0 * v2 - u2 * v0) / (u1 * v2 - v1 * u2);
	G3DReal gamma = (v0 * u1 - u0 * v1) / (u1 * v2 - v1 * u2);
	G3DReal alpha = (G3DReal)1.0 - (gamma + beta);
/*
	GAssert(ABS(alpha * m_pPoints[pTriangle[0]].m_vals[0] + beta * m_pPoints[pTriangle[1]].m_vals[0] + gamma * m_pPoints[pTriangle[2]].m_vals[0] - pRay->m_collisionPoint.m_vals[0]) < .001, "value is wrong");
	GAssert(ABS(alpha * m_pPoints[pTriangle[0]].m_vals[1] + beta * m_pPoints[pTriangle[1]].m_vals[1] + gamma * m_pPoints[pTriangle[2]].m_vals[1] - pRay->m_collisionPoint.m_vals[1]) < .001, "value is wrong");
	GAssert(ABS(alpha * m_pPoints[pTriangle[0]].m_vals[2] + beta * m_pPoints[pTriangle[1]].m_vals[2] + gamma * m_pPoints[pTriangle[2]].m_vals[2] - pRay->m_collisionPoint.m_vals[2]) < .001, "value is wrong");
*/	
	// todo: If beta and gamma > 0 and beta + gamma <= 1,
	// then the point of intersection is inside the triangle.
	// This could be used as a more efficient inside-outside test for triangles.
 
	// Compute the Phong normal
	if(m_pNormals)
	{
		G3DVector tmpNormal(&m_pNormals[pTriangle[0]]);
		tmpNormal.multiply(alpha);
		pRay->m_normalVector.copy(&tmpNormal);
		tmpNormal.copy(&m_pNormals[pTriangle[1]]);
		tmpNormal.multiply(beta);
		pRay->m_normalVector.add(&tmpNormal);
		tmpNormal.copy(&m_pNormals[pTriangle[2]]);
		tmpNormal.multiply(gamma);
		pRay->m_normalVector.add(&tmpNormal);
		pRay->m_normalVector.normalize();
	}

	// Compute texture coordinate
	if(m_pTextureCoords)
	{
		G3DReal x = alpha * m_pTextureCoords[2 * pTriangle[0]] +
				beta * m_pTextureCoords[2 * pTriangle[1]] +
				gamma * m_pTextureCoords[2 * pTriangle[2]];
		G3DReal y = alpha * m_pTextureCoords[2 * pTriangle[0] + 1] +
				beta * m_pTextureCoords[2 * pTriangle[1] + 1] +
				gamma * m_pTextureCoords[2 * pTriangle[2] + 1];
		pRay->SetTextureCoords(x, y);
	}
}

void GRayTraceTriMesh::center(G3DVector* pOutPoint, size_t nIndex)
{
	size_t* pTriangle = &m_pTriangles[3 * nIndex];
	pOutPoint->copy(&m_pPoints[pTriangle[0]]);
	pOutPoint->add(&m_pPoints[pTriangle[1]]);
	pOutPoint->add(&m_pPoints[pTriangle[2]]);
	pOutPoint->multiply((G3DReal)1.0 / (G3DReal)3.0);
}

G3DVector* GRayTraceTriMesh::vertex(size_t nIndex, size_t nVertex)
{
	size_t* pTriangle = &m_pTriangles[3 * nIndex];
	return &m_pPoints[pTriangle[nVertex]];
}

void GRayTraceTriMesh::adjustBoundingBox(size_t nIndex, G3DVector* pMin, G3DVector* pMax)
{
	size_t* pTriangle = &m_pTriangles[3 * nIndex];

	// Vertex 0
	pMin->m_vals[0] = std::min(pMin->m_vals[0], m_pPoints[pTriangle[0]].m_vals[0]);
	pMin->m_vals[1] = std::min(pMin->m_vals[1], m_pPoints[pTriangle[0]].m_vals[1]);
	pMin->m_vals[2] = std::min(pMin->m_vals[2], m_pPoints[pTriangle[0]].m_vals[2]);
	pMax->m_vals[0] = std::max(pMax->m_vals[0], m_pPoints[pTriangle[0]].m_vals[0]);
	pMax->m_vals[1] = std::max(pMax->m_vals[1], m_pPoints[pTriangle[0]].m_vals[1]);
	pMax->m_vals[2] = std::max(pMax->m_vals[2], m_pPoints[pTriangle[0]].m_vals[2]);

	// Vertex 1
	pMin->m_vals[0] = std::min(pMin->m_vals[0], m_pPoints[pTriangle[1]].m_vals[0]);
	pMin->m_vals[1] = std::min(pMin->m_vals[1], m_pPoints[pTriangle[1]].m_vals[1]);
	pMin->m_vals[2] = std::min(pMin->m_vals[2], m_pPoints[pTriangle[1]].m_vals[2]);
	pMax->m_vals[0] = std::max(pMax->m_vals[0], m_pPoints[pTriangle[1]].m_vals[0]);
	pMax->m_vals[1] = std::max(pMax->m_vals[1], m_pPoints[pTriangle[1]].m_vals[1]);
	pMax->m_vals[2] = std::max(pMax->m_vals[2], m_pPoints[pTriangle[1]].m_vals[2]);

	// Vertex 2
	pMin->m_vals[0] = std::min(pMin->m_vals[0], m_pPoints[pTriangle[2]].m_vals[0]);
	pMin->m_vals[1] = std::min(pMin->m_vals[1], m_pPoints[pTriangle[2]].m_vals[1]);
	pMin->m_vals[2] = std::min(pMin->m_vals[2], m_pPoints[pTriangle[2]].m_vals[2]);
	pMax->m_vals[0] = std::max(pMax->m_vals[0], m_pPoints[pTriangle[2]].m_vals[0]);
	pMax->m_vals[1] = std::max(pMax->m_vals[1], m_pPoints[pTriangle[2]].m_vals[1]);
	pMax->m_vals[2] = std::max(pMax->m_vals[2], m_pPoints[pTriangle[2]].m_vals[2]);
}

// static
GRayTraceTriMesh* GRayTraceTriMesh::makeQuadSurface(GRayTraceMaterial* pMaterial, G3DVector* p1, G3DVector* p2, G3DVector* p3, G3DVector* p4)
{
	bool imageTexture = (pMaterial->materialType() == GRayTraceMaterial::Image);
	GRayTraceTriMesh* pMesh = new GRayTraceTriMesh(pMaterial, 4, 2, 0, (imageTexture ? 4 : 0));
	pMesh->setPoint(0, p1);
	pMesh->setPoint(1, p2);
	pMesh->setPoint(2, p3);
	pMesh->setPoint(3, p4);
	pMesh->setTriangle(0, 0, 1, 2);
	pMesh->setTriangle(1, 2, 3, 0);
	if(imageTexture)
	{
		GImage* pImage = ((GRayTraceImageTexture*)pMaterial)->textureImage();
		pMesh->setTextureCoord(0, 0, 0);
		pMesh->setTextureCoord(1, (G3DReal)(pImage->width() - 1), 0);
		pMesh->setTextureCoord(2, (G3DReal)(pImage->width() - 1), (G3DReal)(pImage->height() - 1));
		pMesh->setTextureCoord(3, 0, (G3DReal)(pImage->height() - 1));
	}
	return pMesh;
}

// static
GRayTraceTriMesh* GRayTraceTriMesh::makeSingleTriangle(GRayTraceMaterial* pMaterial, G3DVector* p1, G3DVector* p2, G3DVector* p3)
{
	bool imageTexture = (pMaterial->materialType() == GRayTraceMaterial::Image);
	GRayTraceTriMesh* pMesh = new GRayTraceTriMesh(pMaterial, 3, 1, 0, (imageTexture ? 4 : 0));
	pMesh->setPoint(0, p1);
	pMesh->setPoint(1, p2);
	pMesh->setPoint(2, p3);
	pMesh->setTriangle(0, 0, 1, 2);
	if(imageTexture)
	{
		GImage* pImage = ((GRayTraceImageTexture*)pMaterial)->textureImage();
		pMesh->setTextureCoord(0, 0, 0);
		pMesh->setTextureCoord(1, (G3DReal)(pImage->width() - 1), 0);
		pMesh->setTextureCoord(2, 0, (G3DReal)(pImage->height() - 1));
	}
	return pMesh;
}

// static
GRayTraceTriMesh* GRayTraceTriMesh::makeCylinder(GRayTraceMaterial* pMaterial, G3DVector* pCenter1, G3DVector* pCenter2, G3DReal radius, size_t nSides, bool bEndCaps)
{
	// Compute t, u, and v as the three component vectors of the MakeCylinder. w is just any
	// vector that is not parallel to t
	G3DVector t(pCenter1);
	t.subtract(pCenter2);
	G3DVector w;
	if(t.m_vals[0] > t.m_vals[1] && t.m_vals[0] > t.m_vals[2])
		w.set(0, 1, 0);
	else
		w.set(1, 0, 0);
	G3DVector u, v;
	u.crossProduct(&t, &w);
	u.normalize();
	u.multiply(radius);
	v.crossProduct(&t, &u);
	v.normalize();
	v.multiply(radius);

	// Make the mesh
	size_t nVertices = nSides * 2 + (bEndCaps ? 2 : 0);
	size_t nTriangles = nSides * (bEndCaps ? 4 : 2);
	bool imageTexture = (pMaterial->materialType() == GRayTraceMaterial::Image);
	GImage* pImage = imageTexture ? ((GRayTraceImageTexture*)pMaterial)->textureImage() : NULL;
	GRayTraceTriMesh* pMesh = new GRayTraceTriMesh(pMaterial, nVertices, nTriangles, 0, (pImage ? nVertices : 0));
	G3DVector p1(pCenter1);
	p1.add(&u);
	G3DVector p2(pCenter2);
	p2.add(&u);
	G3DVector p3, p4;
	double dRads;
	double dStep = 2 * M_PI / nSides;
	size_t nVertex = 0;
	size_t nTriangle = 0;
	if(bEndCaps)
	{
		pMesh->setPoint(nVertex++, pCenter1);
		pMesh->setPoint(nVertex++, pCenter2);
		if(pImage)
		{
			pMesh->setTextureCoord(nVertex - 2, (G3DReal)pImage->width() / 2, (G3DReal)pImage->height() / 2);
			pMesh->setTextureCoord(nVertex - 1, (G3DReal)pImage->width() / 2, (G3DReal)pImage->height() / 2);
		}
	}
	size_t nPrevVertexPos = nVertices;
	for(dRads = 2 * M_PI - .000000001; dRads > 0; ) // the small constant is to ensure that rounding error doesn't cause an extra side to be added
	{
		// Move forward
		dRads -= dStep;
		p3.copy(&p1);
		p4.copy(&p2);

		// Compute the two vertices of the new edge
		w.copy(&u);
		w.multiply((G3DReal)cos(dRads));
		p1.copy(&v);
		p1.multiply((G3DReal)sin(dRads));
		p1.add(&w);
		p2.copy(&p1);
		p1.add(pCenter1);
		p2.add(pCenter2);
		pMesh->setPoint(nVertex++, &p1);
		pMesh->setPoint(nVertex++, &p2);

		// Set the texture coordinates
		if(pImage)
		{
			G3DReal y = (G3DReal)(dRads * (pImage->height() - 1) / (2 * M_PI));
			pMesh->setTextureCoord(nVertex - 2, 0, y);
			pMesh->setTextureCoord(nVertex - 1, (G3DReal)(pImage->width() - 1), y);
		}

		// Make the triangles
		pMesh->setTriangle(nTriangle++, nVertex - 1, nVertex - 2, nPrevVertexPos - 1);
		pMesh->setTriangle(nTriangle++, nPrevVertexPos - 2, nPrevVertexPos - 1, nVertex - 2);
		if(bEndCaps)
		{
			pMesh->setTriangle(nTriangle++, nVertex - 2, nPrevVertexPos - 2, 0);
			pMesh->setTriangle(nTriangle++, nVertex - 1, nPrevVertexPos - 1, 1);
		}

		nPrevVertexPos = nVertex;
	}
	GAssert(nVertex == nVertices); // wrong number of vertices
	GAssert(nTriangle == nTriangles); // wrong number of triangles
	//pMesh->ComputePhongNormals();
	return pMesh;
}

// -----------------------------------------------------------------------------

// static
GRayTraceObject* GRayTraceObject::deserialize(GDomNode* pNode, GRayTraceScene* pScene)
{
	switch((ObjectType)pNode->field("type")->asInt())
	{
		case Sphere:
			return new GRayTraceSphere(pNode, pScene);
		case Triangle:
			return new GRayTraceTriangle(pNode, pScene);
	}
	ThrowError("Unexpected type");
	return NULL;
}

// -----------------------------------------------------------------------------

GRayTraceSphere::GRayTraceSphere(GDomNode* pNode, GRayTraceScene* pScene)
: GRayTraceObject()
{
	m_pMaterial = pScene->material((size_t)pNode->field("material")->asInt());
	m_center.deserialize(pNode->field("center"));
	m_radius = pNode->field("radius")->asDouble();
}

// virtual
GDomNode* GRayTraceSphere::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "type", pDoc->newInt(type()));
	pNode->addField(pDoc, "material", pDoc->newInt(pScene->materialIndex(m_pMaterial)));
	pNode->addField(pDoc, "center", m_center.serialize(pDoc));
	pNode->addField(pDoc, "radius", pDoc->newDouble(m_radius));
	return pNode;
}

// virtual
G3DReal GRayTraceSphere::rayDistance(G3DVector* pRayOrigin, G3DVector* pRayDirection)
{
	G3DReal b = (G3DReal)2 * (
				pRayDirection->m_vals[0] * (pRayOrigin->m_vals[0] - m_center.m_vals[0]) +
				pRayDirection->m_vals[1] * (pRayOrigin->m_vals[1] - m_center.m_vals[1]) +
				pRayDirection->m_vals[2] * (pRayOrigin->m_vals[2] - m_center.m_vals[2])
			);
	G3DReal c = pRayOrigin->m_vals[0] * pRayOrigin->m_vals[0] -
				(G3DReal)2 * pRayOrigin->m_vals[0] * m_center.m_vals[0] +
				m_center.m_vals[0] * m_center.m_vals[0] +
				pRayOrigin->m_vals[1] * pRayOrigin->m_vals[1] -
				(G3DReal)2 * pRayOrigin->m_vals[1] * m_center.m_vals[1] +
				m_center.m_vals[1] * m_center.m_vals[1] +
				pRayOrigin->m_vals[2] * pRayOrigin->m_vals[2] -
				(G3DReal)2 * pRayOrigin->m_vals[2] * m_center.m_vals[2] +
				m_center.m_vals[2] * m_center.m_vals[2] -
				m_radius * m_radius;
	G3DReal discriminant = b * b - (G3DReal)4 * c;
	if(discriminant < 0)
		return 0;
	G3DReal dist = (b + (G3DReal)sqrt(discriminant)) / (-2);
	if(dist > MIN_RAY_DISTANCE)
		return dist;
	dist = (b - (G3DReal)sqrt(discriminant)) / (-2);
	return dist;
}

//virtual
void GRayTraceSphere::normalVector(GRayTraceRay* pRay)
{
	pRay->m_normalVector.copy(&pRay->m_collisionPoint);
	pRay->m_normalVector.subtract(&m_center);
	pRay->m_normalVector.normalize();
}

//virtual
void GRayTraceSphere::adjustBoundingBox(G3DVector* pMin, G3DVector* pMax)
{
	pMin->m_vals[0] = std::min(pMin->m_vals[0], m_center.m_vals[0] - m_radius);
	pMin->m_vals[1] = std::min(pMin->m_vals[1], m_center.m_vals[1] - m_radius);
	pMin->m_vals[2] = std::min(pMin->m_vals[2], m_center.m_vals[2] - m_radius);
	pMax->m_vals[0] = std::max(pMax->m_vals[0], m_center.m_vals[0] + m_radius);
	pMax->m_vals[1] = std::max(pMax->m_vals[1], m_center.m_vals[1] + m_radius);
	pMax->m_vals[2] = std::max(pMax->m_vals[2], m_center.m_vals[2] + m_radius);
}

void GRayTraceSphere::drawWireFrame(GCamera* pCamera, GImage* pImage)
{
	G3DVector center, perim, tmp;
	tmp.copy(pCamera->viewSideVector());
	tmp.multiply(m_radius);
	tmp.add(&m_center);
	pCamera->project(&m_center, &center);
	if(center.m_vals[2] < 0)
		return; // behind the camera
	pCamera->project(&tmp, &perim);
	double radius = sqrt((perim.m_vals[0] - center.m_vals[0]) * (perim.m_vals[0] - center.m_vals[0]) + (perim.m_vals[1] - center.m_vals[1]) * (perim.m_vals[1] - center.m_vals[1]));
	int bot = pImage->height() - 1;
	pImage->circle((int)floor(center.m_vals[0] + 0.5), bot - (int)floor(center.m_vals[1] + 0.5), (float)radius, 0xff00ffff);
}

// virtual
void GRayTraceSphere::center(G3DVector* pOutPoint)
{
	memcpy(pOutPoint, &m_center, sizeof(G3DVector));
}

// -----------------------------------------------------------------------------

GRayTraceTriangle::GRayTraceTriangle(GDomNode* pNode, GRayTraceScene* pScene)
: GRayTraceObject()
{
	m_pMesh = pScene->mesh((size_t)pNode->field("mesh")->asInt());
	m_nIndex = (size_t)pNode->field("index")->asInt();
	if(m_nIndex < 0 || m_nIndex >= m_pMesh->triangleCount())
		ThrowError("out of range");
}

// virtual
GDomNode* GRayTraceTriangle::serialize(GDom* pDoc, GRayTraceScene* pScene)
{
	GDomNode* pNode = pDoc->newObj();
	pNode->addField(pDoc, "type", pDoc->newInt(type()));
	pNode->addField(pDoc, "mesh", pDoc->newInt(pScene->meshIndex(m_pMesh)));
	pNode->addField(pDoc, "index", pDoc->newInt(m_nIndex));
	return pNode;
}

//virtual
G3DReal GRayTraceTriangle::rayDistance(G3DVector* pRayOrigin, G3DVector* pRayDirection)
{
	return m_pMesh->rayDistanceToTriangle(m_nIndex, pRayOrigin, pRayDirection);
}

//virtual
void GRayTraceTriangle::normalVector(GRayTraceRay* pRay)
{
	m_pMesh->normalVector(pRay, m_nIndex);
}

//virtual
void GRayTraceTriangle::center(G3DVector* pOutPoint)
{
	m_pMesh->center(pOutPoint, m_nIndex);
}

//virtual
void GRayTraceTriangle::adjustBoundingBox(G3DVector* pMin, G3DVector* pMax)
{
	m_pMesh->adjustBoundingBox(m_nIndex, pMin, pMax);
}

G3DVector* GRayTraceTriangle::vertex(int nVertex)
{
	return m_pMesh->vertex(m_nIndex, nVertex);
}

void GRayTraceTriangle::drawWireFrame(GCamera* pCamera, GImage* pImage)
{
	G3DVector a, b, c;
	pCamera->project(m_pMesh->vertex(m_nIndex, 0), &a);
	pCamera->project(m_pMesh->vertex(m_nIndex, 1), &b);
	pCamera->project(m_pMesh->vertex(m_nIndex, 2), &c);
	if(a.m_vals[2] < 0 && b.m_vals[2] < 0 && c.m_vals[2] < 0)
		return;
	int bot = pImage->height() - 1;
	pImage->line((int)floor(a.m_vals[0] + 0.5), bot - (int)floor(a.m_vals[1] + 0.5), (int)floor(b.m_vals[0] + 0.5), bot - (int)floor(b.m_vals[1] + 0.5), 0xffff00ff);
	pImage->line((int)floor(b.m_vals[0] + 0.5), bot - (int)floor(b.m_vals[1] + 0.5), (int)floor(c.m_vals[0] + 0.5), bot - (int)floor(c.m_vals[1] + 0.5), 0xffff00ff);
	pImage->line((int)floor(c.m_vals[0] + 0.5), bot - (int)floor(c.m_vals[1] + 0.5), (int)floor(a.m_vals[0] + 0.5), bot - (int)floor(a.m_vals[1] + 0.5), 0xffff00ff);
}







GTriMeshBuilder::GTriMeshBuilder(GRayTraceMaterial* pMaterial)
: m_pMaterial(pMaterial)
{
	if(pMaterial->materialType() == GRayTraceMaterial::Image)
		ThrowError("Sorry, this class does not support image materials");
}

GTriMeshBuilder::~GTriMeshBuilder()
{
}

size_t GTriMeshBuilder::addPoint(const G3DVector& v)
{
	size_t index = 0;
	for(vector<G3DVector>::iterator it = m_points.begin(); it != m_points.end(); it++)
	{
		if(it->isEqual(v))
			return index;
		index++;
	}
	m_points.push_back(v);
	return index;
}

void GTriMeshBuilder::add(const G3DVector& a, const G3DVector& b, const G3DVector& c)
{
	if(a.isEqual(b) || b.isEqual(c) || a.isEqual(c))
		return;
	m_indexes.push_back(addPoint(a));
	m_indexes.push_back(addPoint(b));
	m_indexes.push_back(addPoint(c));
}

GRayTraceTriMesh* GTriMeshBuilder::mesh()
{
	GRayTraceTriMesh* pMesh = new GRayTraceTriMesh(m_pMaterial, m_points.size(), m_indexes.size() / 3, 0, 0);
	for(size_t i = 0; i < m_points.size(); i++)
		pMesh->setPoint(i, &m_points[i]);
	size_t j = 0;
	for(size_t i = 0; i < m_indexes.size(); )
	{
		size_t a = m_indexes[i++];
		size_t b = m_indexes[i++];
		size_t c = m_indexes[i++];
		pMesh->setTriangle(j++, a, b, c);
	}
	m_points.clear();
	m_indexes.clear();
	return pMesh;
}





G3dLetterMaker::G3dLetterMaker(GRayTraceMaterial* pMaterial)
: m_builder(pMaterial), m_lineWidth(0.1), m_circleSegments(24), m_spaceWidth(0.4)
{
	m_pos.set(0, 0, 0);
	m_basis.setToIdentity();
}

G3dLetterMaker::~G3dLetterMaker()
{
}

void G3dLetterMaker::move(G3DReal dx, G3DReal dy, G3DReal dz)
{
	G3DVector tmp(dx, dy, dz);
	G3DVector ofs;
	m_basis.multiply(&tmp, &ofs);
	m_pos.add(&ofs);
}

void G3dLetterMaker::move(G3DVector& vec)
{
	G3DVector ofs;
	m_basis.multiply(&vec, &ofs);
	m_pos.add(&ofs);
	m_pos.add(&vec);
}

void G3dLetterMaker::scale(G3DReal width, G3DReal height, G3DReal depth)
{
	m_basis.m_rows[0].multiply(width);
	m_basis.m_rows[1].multiply(height);
	m_basis.m_rows[2].multiply(depth);
}

void G3dLetterMaker::rotate(G3DReal yaw, G3DReal pitch, G3DReal roll)
{
	if(roll != 0)
	{
		G3DMatrix a;
		a.copy(&m_basis);
		G3DMatrix b;
		b.setToIdentity();
		b.m_rows[0].set(cos(roll), sin(roll), 0.0);
		b.m_rows[1].set(-sin(roll), cos(roll), 0.0);
		m_basis.multiply(&a, &b);
	}
	if(pitch != 0)
	{
		G3DMatrix a;
		a.copy(&m_basis);
		G3DMatrix b;
		b.setToIdentity();
		b.m_rows[1].set(0.0, cos(pitch), sin(pitch));
		b.m_rows[2].set(0.0, -sin(pitch), cos(pitch));
		m_basis.multiply(&a, &b);
	}
	if(yaw != 0)
	{
		G3DMatrix a;
		a.copy(&m_basis);
		G3DMatrix b;
		b.setToIdentity();
		b.m_rows[0].set(cos(yaw), 0.0, sin(yaw));
		b.m_rows[2].set(-sin(yaw), 0.0, cos(yaw));
		m_basis.multiply(&a, &b);
	}
}

double G3dLetterMaker::letterWidth(char c)
{
	double w = 0.5;
	switch(c)
	{
		case ' ': w = m_spaceWidth; break;
		case '"': w = m_lineWidth + m_lineWidth; break;
		case '\'': w = m_lineWidth; break;
		case '(': w = 0.1; break;
		case ')': w = 0.1; break;
		case '+': w = 0.4; break;
		case ',': w = 0.1; break;
		case '-': w = 0.4; break;
		case '.': w = 0.2; break;
		case '@': w = 0.6; break;
		case 'M': w = 0.6; break;
		case 'W': w = 0.6; break;
		case 'i': w = 0.1; break;
		case 'j': w = 0.225; break;
		case 'l': w = 0.225; break;
		case 'm': w = 0.6; break;
		case 'q': w = 0.6; break;
		case 't': w = 0.3; break;
		case 'w': w = 0.6; break;
	}
	return w + m_lineWidth;
}

GRayTraceTriMesh* G3dLetterMaker::makeLetter(char c)
{
	switch(c)
	{
		case ' ':
			break;
		case '!':
			vbar(0.0, 0.3, 0.0, 1.0);
			curve(0.025, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			break;
		case '"':
			vbar(0.0, 0.8, 0.0, 1.0);
			vbar(m_lineWidth + m_lineWidth, 0.8, m_lineWidth + m_lineWidth, 1.0);
			break;
		case '#':
			vbar(0.1, 0.0, 0.2, 1.0);
			vbar(0.3, 0.0, 0.4, 1.0);
			hbar(0.0, 0.333 - 0.5 * m_lineWidth, 0.5 + m_lineWidth);
			hbar(0.0, 0.667 - 0.5 * m_lineWidth, 0.5 + m_lineWidth);
			break;
		case '\'': // apostrophe
			vbar(0.0, 0.8, 0.0, 1.0);
			break;
		case '(':
			curve(0.1, 0.5, 0.4, 0.8, 1.8, 2.2);
			break;
		case ')':
			curve(0.5 - m_lineWidth, 0.5, 0.4, 0.8, 0.8, 1.2);
			break;
		case '+':
			vbar(0.2, 0.3, 0.2, 0.7);
			hbar(0.0, 0.5 - 0.5 * m_lineWidth, 0.4);
			break;
		case ',':
			curve(0.05, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			curve(0.05, m_lineWidth, 0.5 * m_lineWidth, 1.6 * m_lineWidth, 1.4, 2.0);
			break;
		case '-':
			hbar(0.0, 0.5 - 0.5 * m_lineWidth, 0.4);
			break;
		case '.':
			curve(0.025, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			break;
		case '/':
			vbar(0.0, 0.0, 0.5, 1.0);
			break;
		case '0':
			curve(0.5, 0.6, 0.5, 0.4, 0.0, 1.0);
			curve(0.5, 0.4, 0.5, 0.4, 1.0, 2.0);
			vbar(0.0, 0.4, 0.0, 0.6);
			vbar(0.5, 0.4, 0.5, 0.6);
			vbar(0.0, 0.4, 0.5, 0.6);
			break;
		case '1':
			vbar(0.15, 1.0, 0.0, 0.85);
			vbar(0.15, m_lineWidth, 0.15, 1.0);
			hbar(0.0, 0.0, 0.3 + m_lineWidth);
			break;
		case '2':
			curve(0.5, 0.65, 0.5, 0.35, 0.0, 1.0);
			vbar(0.0, m_lineWidth, 0.5, 0.65);
			hbar(0.0, 0.0, 0.5 + m_lineWidth);
			break;
		case '3':
			{
			double w = 0.5;
			double h = (w + m_lineWidth) / (4 * (w + m_lineWidth) - 2 * m_lineWidth);
			curve(0.5, 1.0 - h, w, h, 1.5, 3.0);
			curve(0.5, h, w, h, 1.0, 2.5);
			}
			break;
		case '4':
			vbar(0.375, 0.0, 0.375, 1.0);
			vbar(0.375, 1.0, 0.0, 0.35);
			hbar(0.0, 0.35 - m_lineWidth, 0.5 + m_lineWidth);
			break;
		case '5':
			{
			double w = 0.5;
			double h = 0.30;
			double hScale = 2.0 * h / (w + m_lineWidth);
			hbar(0.0, 1.0 - m_lineWidth, 0.5 + m_lineWidth);
			vbar(0.0, h + h, 0.0, 1.0 - m_lineWidth);
			hbar(0.0, h + h - m_lineWidth * hScale, 0.25 + 0.5 * m_lineWidth, hScale);
			curve(0.5, h, 0.5, h, 1.0, 2.5);
			}
			break;
		case '6':
			curve(0.5, 0.7, 0.5, 0.3, 0.25, 1.0);
			vbar(0.0, 0.3, 0.0, 0.7);
			curve(0.5, 0.3, 0.5, 0.3, 0.0, 2.0);
			break;
		case '7':
			hbar(0.0, 1.0 - m_lineWidth, 0.5 + m_lineWidth);
			vbar(0.15, 0.0, 0.5, 1.0 - m_lineWidth);
			break;
		case '8':
			{
			double w = 0.5;
			double h = (w + m_lineWidth) / (4 * (w + m_lineWidth) - 2 * m_lineWidth);
			curve(0.5, 1.0 - h, w, h, 0.0, 2.0);
			curve(0.5, h, w, h, 0.0, 2.0);
			}
			break;
		case '9':
			curve(0.5, 0.7, 0.5, 0.3, 0.0, 2.0);
			vbar(0.5, 0.3, 0.5, 0.7);
			curve(0.5, 0.3, 0.5, 0.3, 1.25, 2.0);
			break;
		case ':':
			curve(0.025, 0.4 + m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			curve(0.025, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			break;
		case ';':
			curve(0.05, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			curve(0.05, m_lineWidth, 0.5 * m_lineWidth, 1.6 * m_lineWidth, 1.4, 2.0);
			curve(0.025, 0.4 + m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			break;
		case '<':
			vbar(0.5, 0.8, 0.0, 0.5);
			vbar(0.0, 0.5, 0.5, 0.2);
			break;
		case '=':
			hbar(0.0, 0.3, 0.5 + m_lineWidth);
			hbar(0.0, 0.6, 0.5 + m_lineWidth);
			break;
		case '>':
			vbar(0.0, 0.8, 0.5, 0.5);
			vbar(0.5, 0.5, 0.0, 0.2);
			break;
		case '?':
			{
			curve(0.5, 0.75, 0.5, 0.25, 0.0, 1.0);
			vbar(0.5, 0.65, 0.5, 0.75);
			double h = (0.65 - 0.4) * (0.25 + m_lineWidth) / (0.25 * 2);
			curve(0.5, 0.65, 0.25, h, 1.5, 2.0);
			curve(0.5, 0.4, 0.25, h, 0.5, 1.0);
			vbar(0.25, 0.3, 0.25, 0.4);
			curve(0.275, m_lineWidth, 0.5 * m_lineWidth, 0.75 * m_lineWidth, 0.0, 2.0);
			}
			break;
		case '@':
			curve(0.42, 0.5, 0.26, 0.16, 0.0, 2.0);
			curve(0.6, 0.5, 0.6, 0.36, 0.0, 1.7);
			vbar(0.42, 0.4, 0.42, 0.66);
			vbar(0.6, 0.4, 0.6, 0.5);
			curve(0.6, 0.4, 0.18, 0.09, 1.0, 2.0);
			break;
		case 'A':
			vbar(0.0, 0.0, 0.25, 1.0);
			vbar(0.25, 1.0, 0.5, 0.0);
			hbar(0.08, 0.2, 0.32 + m_lineWidth);
			break;
		case 'B':
			{
			vbar(0.0, 0.0, 0.0, 1.0);
			double w = 0.6;
			double h = (w + m_lineWidth) / (4 * (w + m_lineWidth) - 2 * m_lineWidth);
			double hScale = 2.0 * h / (w + m_lineWidth);
			double bl = 0.5 + m_lineWidth - 0.5 * (w + m_lineWidth) - m_lineWidth;
			curve(0.5, 1.0 - h, w, h, 1.5, 2.5);
			curve(0.5, h, w, h, 1.5, 2.5);
			hbar(m_lineWidth, 1.0 - m_lineWidth * hScale, bl, hScale);
			hbar(m_lineWidth, 0.5 - 0.5 * m_lineWidth * hScale, bl, hScale);
			hbar(m_lineWidth, 0.0, bl, hScale);
			}
			break;
		case 'C':
			curve(0.5, 0.75, 0.5, 0.25, 0.0, 1.0);
			vbar(0.0, 0.25, 0.0, 0.75);
			curve(0.5, 0.25, 0.5, 0.25, 1.0, 2.0);
			break;
		case 'D':
			curve(0.5, 0.5, 0.6, 0.5, 1.5, 2.5);
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 1.0 - m_lineWidth / (0.6 + m_lineWidth), 0.2 - 0.5 * m_lineWidth, 1.0 / (0.6 + m_lineWidth));
			hbar(m_lineWidth, 0.0, 0.2 - 0.5 * m_lineWidth, 1.0 / (0.6 + m_lineWidth));
			break;
		case 'E':
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 1.0 - m_lineWidth, 0.5);
			hbar(m_lineWidth, 0.5 - 0.5 * m_lineWidth, 0.5);
			hbar(m_lineWidth, 0.0, 0.5);
			break;
		case 'F':
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 1.0 - m_lineWidth, 0.5 - m_lineWidth);
			hbar(m_lineWidth, 0.5 - 0.5 * m_lineWidth, 0.5 - m_lineWidth);
			break;
		case 'G':
			curve(0.5, 0.75, 0.5, 0.25, 0.0, 1.0);
			vbar(0.0, 0.25, 0.0, 0.75);
			curve(0.5, 0.25, 0.5, 0.25, 1.0, 2.0);
			hbar(0.25, 0.5 - 0.5 * m_lineWidth, 0.25);
			vbar(0.5, 0.0, 0.5, 0.5 + 0.5 * m_lineWidth);
			break;
		case 'H':
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 0.5 - 0.5 * m_lineWidth, 0.5 - m_lineWidth);
			vbar(0.5, 0.0, 0.5, 1.0);
			break;
		case 'I':
			hbar(0.0, 1.0 - m_lineWidth, 0.5 + m_lineWidth);
			vbar(0.25, m_lineWidth, 0.25, 1.0 - m_lineWidth);
			hbar(0.0, 0.0, 0.5 + m_lineWidth);
			break;
		case 'J':
			hbar(0.2, 1.0 - m_lineWidth, 0.3 + m_lineWidth);
			vbar(0.35, 0.2, 0.35, 1.0 - m_lineWidth);
			curve(0.35, 0.2, 0.35, 0.2, 1.0, 2.0);
			break;
		case 'K':
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.0, 0.5, 0.5 - 0.4 * m_lineWidth, 1.0, 1.4);
			vbar(0.0, 0.5, 0.5 - 0.4 * m_lineWidth, 0.0, 1.4);
			break;
		case 'L':
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 0.0, 0.5);
			break;
		case 'M':
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.0, 1.0, 0.3, 0.5);
			vbar(0.3, 0.5, 0.6, 1.0);
			vbar(0.6, 1.0, 0.6, 0.0);
			break;
		case 'N':
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.0, 1.0, 0.5, 0.0);
			vbar(0.5, 1.0, 0.5, 0.0);
			break;
		case 'O':
			curve(0.5, 0.8, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.8);
			vbar(0.5, 0.2, 0.5, 0.8);
			break;
		case 'P':
			{
			double h = (0.6 + m_lineWidth) / (4 * (0.6 + m_lineWidth) - 2 * m_lineWidth);
			curve(0.5, 1.0 - h, 0.6, h, 1.5, 2.5);
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 1.0 - 2 * h * m_lineWidth / (0.6 + m_lineWidth), 0.2, 2 * h / (0.6 + m_lineWidth));
			hbar(m_lineWidth, 0.5 - h * m_lineWidth / (0.6 + m_lineWidth), 0.2, 2 * h / (0.6 + m_lineWidth));
			}
			break;
		case 'Q':
			curve(0.5, 0.8, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.8);
			vbar(0.5, 0.2, 0.5, 0.8);
			vbar(0.25, 0.5, 0.5, 0.0);
			break;
		case 'R':
			{
			double h = (0.6 + m_lineWidth) / (4 * (0.6 + m_lineWidth) - 2 * m_lineWidth);
			curve(0.5, 1.0 - h, 0.6, h, 1.5, 2.5);
			vbar(0.0, 0.0, 0.0, 1.0);
			hbar(m_lineWidth, 1.0 - 2 * h * m_lineWidth / (0.6 + m_lineWidth), 0.2, 2 * h / (0.6 + m_lineWidth));
			hbar(m_lineWidth, 0.5 - h * m_lineWidth / (0.6 + m_lineWidth), 0.2, 2 * h / (0.6 + m_lineWidth));
			vbar(0.2, 0.5, 0.5, 0.0);
			}
			break;
		case 'S':
			curve(0.5, 0.75, 0.5, 0.25, 0.0, 1.0);
			curve(0.5, 0.75, 0.5, 0.25 + 0.5 * m_lineWidth, 1.0, 1.5);
			curve(0.5, 0.25, 0.5, 0.25 + 0.5 * m_lineWidth, 0.0, 0.5);
			curve(0.5, 0.25, 0.5, 0.25, 1.0, 2.0);
			break;
		case 'T':
			hbar(0.0, 1.0 - m_lineWidth, 0.5 + m_lineWidth);
			vbar(0.25, 0.0, 0.25, 1.0 - m_lineWidth);
			break;
		case 'U':
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 1.0);
			vbar(0.5, 0.2, 0.5, 1.0);
			break;
		case 'V':
			vbar(0.0, 1.0, 0.25, 0.0);
			vbar(0.25, 0.0, 0.5, 1.0);
			break;
		case 'W':
			vbar(0.0, 1.0, 0.15, 0.0);
			vbar(0.15, 0.0, 0.3, 0.7);
			vbar(0.3, 0.7, 0.45, 0.0);
			vbar(0.45, 0.0, 0.6, 1.0);
			break;
		case 'X':
			vbar(0.0, 0.0, 0.5, 1.0);
			vbar(0.0, 1.0, 0.5, 0.0);
			break;
		case 'Y':
			vbar(0.0, 1.0, 0.25, 0.5);
			vbar(0.25, 0.5, 0.5, 1.0);
			vbar(0.25, 0.0, 0.25, 0.5);
			break;
		case 'Z':
			vbar(0.0, m_lineWidth, 0.5, 1.0 - m_lineWidth);
			hbar(0.0, 0.0, 0.5 + m_lineWidth);
			hbar(0.0, 1.0 - m_lineWidth, 0.5 + m_lineWidth);
			break;
		case '\\':
			vbar(0.0, 1.0, 0.5, 0.0);
			break;
		case 'a':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.15 + 0.3 * m_lineWidth, 0.5, 1.0);
			curve(0.5, 0.5, 0.5, 0.15 + 0.3 * m_lineWidth, 1.5, 2.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.5, 0.0, 0.5, 0.5);
			break;
		case 'b':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.5, 0.2, 0.5, 0.5);
			break;
		case 'c':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			break;
		case 'd':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			vbar(0.5, 0.0, 0.5, 1.0);
			break;
		case 'e':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			vbar(0.5, 0.35 - 0.5 * m_lineWidth, 0.5, 0.5);
			hbar(m_lineWidth, 0.35 - 0.5 * m_lineWidth, 0.5);
			break;
		case 'f':
			curve(0.5, 0.8, 0.4, 0.2, 0.0, 1.0);
			vbar(0.1, 0.0, 0.1, 0.8);
			hbar(0.0, 0.6 - m_lineWidth, 0.4);
			break;
		case 'g':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			vbar(0.5, -0.1, 0.5, 0.7);
			curve(0.5, -0.1, 0.5, 0.2, 1.0, 2.0);
			break;
		case 'h':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.5, 0.0, 0.5, 0.5);
			break;
		case 'i':
			vbar(0.0, 0.2, 0.0, 0.5);
			curve(0.25, 0.2, 0.25, 0.2, 1.0, 1.5);
			curve(0.0, 0.7, 0.25 * m_lineWidth, 0.625 * m_lineWidth, 0.0, 2.0);
			break;
		case 'j':
			vbar(0.125, -0.1, 0.125, 0.5);
			curve(0.125, -0.1, 0.25, 0.2, 1.5, 2.0);
			curve(0.125, 0.7, 0.25 * m_lineWidth, 0.625 * m_lineWidth, 0.0, 2.0);
			break;
		case 'k':
			vbar(0.0, 0.0, 0.0, 1.0);
			vbar(0.0, 0.35, 0.5 - 0.4 * m_lineWidth, 0.7, 1.4);
			vbar(0.0, 0.35, 0.5 - 0.4 * m_lineWidth, 0.0, 1.4);
			break;
		case 'l':
			vbar(0.0, 0.2, 0.0, 1.0);
			curve(0.25, 0.2, 0.25, 0.2, 1.0, 1.5);
			break;
		case 'm':
			curve(0.3, 0.5, 0.3, 0.2, 0.0, 1.0);
			curve(0.6, 0.5, 0.3, 0.2, 0.0, 1.0);
			vbar(0.0, 0.0, 0.0, 0.7);
			vbar(0.3, 0.0, 0.3, 0.5);
			vbar(0.6, 0.0, 0.6, 0.5);
			break;
		case 'n':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			vbar(0.0, 0.0, 0.0, 0.7);
			vbar(0.5, 0.0, 0.5, 0.5);
			break;
		case 'o':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			vbar(0.5, 0.2, 0.5, 0.5);
			break;
		case 'p':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, -0.3, 0.0, 0.7);
			vbar(0.5, 0.2, 0.5, 0.5);
			break;
		case 'q':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.5);
			vbar(0.5, -0.1, 0.5, 0.7);
			curve(0.7, -0.1, 0.2, 0.2, 1.0, 1.5);
			break;
		case 'r':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			vbar(0.0, 0.0, 0.0, 0.7);
			break;
		case 's':
			curve(0.5, 0.5, 0.5, 0.2, 0.0, 1.0);
			curve(0.5, 0.5, 0.5, 0.15 + 0.3 * m_lineWidth, 1.0, 1.5);
			curve(0.5, 0.2, 0.5, 0.15 + 0.3 * m_lineWidth, 0.0, 0.5);
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			break;
		case 't':
			vbar(0.15, 0.2, 0.15, 1.0);
			hbar(0.0, 0.7 - m_lineWidth, 0.3 + m_lineWidth);
			curve(0.4, 0.2, 0.25, 0.2, 1.0, 1.5);
			break;
		case 'u':
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.7);
			vbar(0.5, 0.0, 0.5, 0.7);
			break;
		case 'v':
			vbar(0.0, 0.7, 0.25, 0.0);
			vbar(0.25, 0.0, 0.5, 0.7);
			break;
		case 'w':
			vbar(0.0, 0.7, 0.15, 0.0);
			vbar(0.15, 0.0, 0.3, 0.5);
			vbar(0.3, 0.5, 0.45, 0.0);
			vbar(0.45, 0.0, 0.6, 0.7);
			break;
		case 'x':
			vbar(0.0, 0.0, 0.5, 0.7);
			vbar(0.0, 0.7, 0.5, 0.0);
			break;
		case 'y':
			curve(0.5, 0.2, 0.5, 0.2, 1.0, 2.0);
			vbar(0.0, 0.2, 0.0, 0.7);
			vbar(0.5, -0.1, 0.5, 0.7);
			curve(0.5, -0.1, 0.5, 0.2, 1.0, 2.0);
			break;
		case 'z':
			vbar(0.0, m_lineWidth, 0.5, 0.7 - m_lineWidth);
			hbar(0.0, 0.0, 0.5 + m_lineWidth);
			hbar(0.0, 0.7 - m_lineWidth, 0.5 + m_lineWidth);
			break;
		default:
			// make an asterisk
			vbar(0.1, 0.0, 0.4, 0.7);
			vbar(0.1, 0.7, 0.4, 0.0);
			hbar(0.0, 0.35 - 0.5 * m_lineWidth, 0.5 + m_lineWidth);
			break;
	};
	return m_builder.mesh();
}

GRayTraceTriMesh* G3dLetterMaker::specialChar(const char* szName)
{
	if(strcmp(szName, "iota") == 0)
	{
		vbar(0.0, 0.2, 0.0, 0.5);
		curve(0.25, 0.2, 0.25, 0.2, 1.0, 1.5);
	}
	else
		ThrowError("Unrecognized special char");
	return m_builder.mesh();
}

void G3dLetterMaker::writeString(GRayTraceScene* pScene, const char* szString)
{
	while(*szString != '\0')
	{
		pScene->addMesh(makeLetter(*szString));
		move(letterWidth(*szString) + m_lineWidth);
		szString++;
	}
}

double G3dLetterMaker::measureWidth(const char* szString)
{
	double width = 0.0;
	while(*szString != '\0')
	{
		width += letterWidth(*szString) + m_lineWidth;
		szString++;
	}
	return width;
}

void G3dLetterMaker::set(G3DVector* pBack, G3DVector* pFront, G3DReal x, G3DReal y)
{
	G3DVector tmp(x, y, 0.0);
	m_basis.multiply(&tmp, pBack);
	pBack->add(&m_pos);
	tmp.set(x, y, 1.0);
	m_basis.multiply(&tmp, pFront);
	pFront->add(&m_pos);
}

void G3dLetterMaker::addQuad(G3DReal x1, G3DReal y1, G3DReal x2, G3DReal y2, G3DReal x3, G3DReal y3, G3DReal x4, G3DReal y4)
{
	G3DVector bl1, br1, bl2, br2;
	G3DVector fl1, fr1, fl2, fr2;
	set(&bl1, &fl1, x1, y1);
	set(&br1, &fr1, x2, y2);
	set(&bl2, &fl2, x3, y3);
	set(&br2, &fr2, x4, y4);

	// front face
	m_builder.add(fl1, fr1, fl2);
	m_builder.add(fl2, fr2, fr1);

	// back face
	m_builder.add(bl1, br1, bl2);
	m_builder.add(bl2, br2, br1);

	// left face
	m_builder.add(fl1, fl2, bl1);
	m_builder.add(bl1, bl2, fl2);

	// right face
	m_builder.add(fr1, fr2, br1);
	m_builder.add(br1, br2, fr2);

	// top face
	m_builder.add(fl1, fr1, bl1);
	m_builder.add(bl1, br1, fr1);

	// bottom face
	m_builder.add(fl2, fr2, bl2);
	m_builder.add(bl2, br2, fr2);
}

void G3dLetterMaker::vbar(G3DReal x1, G3DReal y1, G3DReal x2, G3DReal y2, G3DReal width)
{
	addQuad(x1, y1,
		x1 + width * m_lineWidth, y1,
		x2, y2,
		x2 + width * m_lineWidth, y2);
}

void G3dLetterMaker::hbar(G3DReal x, G3DReal y, G3DReal w, G3DReal height)
{
	addQuad(x, y + m_lineWidth * height,
		x + w, y + m_lineWidth * height,
		x, y,
		x + w, y);
}

void G3dLetterMaker::curve(G3DReal x, G3DReal y, G3DReal w, G3DReal hh, double from, double to)
{
	double fromRads = from * M_PI;
	double toRads = to * M_PI;
	if(toRads < fromRads)
		std::swap(fromRads, toRads);
	size_t segments = size_t((double(m_circleSegments) + 0.5) * (toRads - fromRads) / (2.0 * M_PI));
	double step = (toRads - fromRads) / segments;
	double r = fromRads;
	double cx = x - 0.5 * (w - m_lineWidth);
	double cy = y;
	double innerRadius = x - cx;
	double outerRadius = innerRadius + m_lineWidth;
	double verticalScale = hh / outerRadius;
	for(size_t s = 0; s < segments; s++)
	{
		double p = r;
		r += step;
		addQuad(cx + outerRadius * cos(p), cy + verticalScale * outerRadius * sin(p),
			cx + outerRadius * cos(r), cy + verticalScale * outerRadius * sin(r),
			cx + innerRadius * cos(p), cy + verticalScale * innerRadius * sin(p),
			cx + innerRadius * cos(r), cy + verticalScale * innerRadius * sin(r));
	}
}

} // namespace GClasses

