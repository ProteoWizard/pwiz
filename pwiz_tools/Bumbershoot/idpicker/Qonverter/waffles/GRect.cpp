#include "GRect.h"

using namespace GClasses;

void GRect::clip(GRect* pClippingRect)
{
	if(x + w > pClippingRect->x + pClippingRect->w)
		w = pClippingRect->x + pClippingRect->w - x;
	if(y + h > pClippingRect->y + pClippingRect->h)
		h = pClippingRect->y + pClippingRect->h - y;
	if(x < pClippingRect->x)
	{
		w -= (pClippingRect->x - x);
		x = pClippingRect->x;
	}
	if(y < pClippingRect->y)
	{
		h -= (pClippingRect->y - y);
		y = pClippingRect->y;
	}
}
