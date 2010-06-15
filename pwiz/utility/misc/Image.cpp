//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "Image.hpp"
#include "gd.h"
#include "gdfontt.h"
#include "gdfonts.h"
#include "gdfontmb.h"
#include "gdfontl.h"
#include "gdfontg.h"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace util {


namespace {

class ScopedData
{
    public:
    ScopedData() : size_(0), data_(0) {}
    int size() const {return size_;}
    void* data() {return data_;}
    const void* data() const {return data_;}
    ~ScopedData() {gdFree(data_);}

    protected:
    int size_;
    void* data_;
};


class ScopedDataJpg : public ScopedData
{
    public:
    ScopedDataJpg(gdImagePtr im)
    {
        data_ = gdImageJpegPtr(im, &size_, -1);
    }
};


class ScopedDataPng : public ScopedData
{
    public:
    ScopedDataPng(gdImagePtr im)
    {
        data_ = gdImagePngPtr(im, &size_);
    }
};


bool writeScopedData(const char* filename, const ScopedData& sd)
{
    ofstream os(filename, ios::binary);

    if (!os)
    {
        cerr << "[writeScopedData()] Error opening file " << filename << endl;
        return false;
    }

    if (!sd.data() || !sd.size())
    {
        cerr << "[writeScopedData()] No data to write.\n";
        return false;
    }

    //cout << "Writing " << sd.size() << " bytes to file " << filename << endl;
    os.write((const char*)sd.data(), sd.size());
    return true;
}

} // namespace


class ImageImpl : public Image
{
    public:
    ImageImpl(int width, int height)
    :   width_(width), height_(height)
    {
        im_ = gdImageCreateTrueColor(width_, height_);
    }

    void pixel(const Point& point, const Color& color)
    {
        gdImageSetPixel(im_, point.x, point.y, color2gd(color));
    }

    void string(const std::string& text, const Point& point, const Color& color, Size size, int align)
    {
        // copy text into gd-friendly (unsigned char) buffer

        vector<unsigned char> buffer;
        copy(text.begin(), text.end(), back_inserter(buffer));
        buffer.push_back('\0');

        // choose font 

        gdFontPtr font;
        switch (size)
        {
            case Tiny: font = gdFontGetTiny(); break;
            case Small: font = gdFontGetSmall(); break;
            case MediumBold: font = gdFontGetMediumBold(); break;
            case Large: font = gdFontGetLarge(); break;
            case Giant: font = gdFontGetGiant(); break;
            default: throw runtime_error("[ImageImpl::string()] This isn't happening.");
        }

        // calculate position

        Point position = point;
        int length = (int)text.size() * font->w;
        int height = font->h;
        
        if (align & CenterX) position.x -= length/2;
        else if (align & Right) position.x -= length;
            
        if (align & CenterY) position.y -= height/2;
        else if (align & Bottom) position.y -= height;

        // draw the string
            
        gdImageString(im_, font, position.x, position.y, &buffer[0], color2gd(color)); 
    }

    void stringUp(const std::string& text, const Point& point, const Color& color, Size size, int align)
    {
        // copy text into gd-friendly (unsigned char) buffer

        vector<unsigned char> buffer;
        copy(text.begin(), text.end(), back_inserter(buffer));
        buffer.push_back('\0');

        // choose font 

        gdFontPtr font;
        switch (size)
        {
            case Tiny: font = gdFontGetTiny(); break;
            case Small: font = gdFontGetSmall(); break;
            case MediumBold: font = gdFontGetMediumBold(); break;
            case Large: font = gdFontGetLarge(); break;
            case Giant: font = gdFontGetGiant(); break;
            default: throw runtime_error("[ImageImpl::string()] This isn't happening.");
        }

        // calculate position

        Point position = point;
        int length = (int)text.size() * font->w;
        int height = font->h;
        
        if (align & CenterX) position.x -= height/2;
        else if (align & Right) position.x -= height;
            
        if (align & CenterY) position.y -= length/2;
        else if (align & Bottom) position.y -= length;

        // draw the string vertically
            
        gdImageStringUp(im_, font, position.x, position.y, &buffer[0], color2gd(color)); 
    }

    void rectangle(const Point& point1, const Point& point2, const Color& color, bool filled)
    {
        if (filled)
            gdImageFilledRectangle(im_, point1.x, point1.y, point2.x, point2.y, color2gd(color));
        else    
            gdImageRectangle(im_, point1.x, point1.y, point2.x, point2.y, color2gd(color));
    }

    void circle(const Point& center, int radius, const Color& color, bool filled)
    {
        if (filled)
            gdImageFilledEllipse(im_, center.x, center.y, radius*2, radius*2, color2gd(color)); 
        else    
            gdImageArc(im_, center.x, center.y, radius*2, radius*2, 0, 360, color2gd(color)); 
    }

    void line(const Point& point1, const Point& point2, const Color& color)
    {
        gdImageLine(im_, point1.x, point1.y, point2.x, point2.y, color2gd(color));
    }

    void clip(const Point& point1, const Point& point2)
    {
        gdImageSetClip(im_, point1.x, point1.y, point2.x, point2.y);
    }

    virtual bool writeJpg(const char* filename) const
    {
        ScopedDataJpg imageData(im_);
        return writeScopedData(filename, imageData);
    }

    virtual bool writePng(const char* filename) const
    {
        ScopedDataPng imageData(im_);
        return writeScopedData(filename, imageData);
    }

    ~ImageImpl()
    {
        gdImageDestroy(im_);
    }

    private:
    int width_;
    int height_;
    gdImagePtr im_;

    int color2gd(const Color& color) {return gdTrueColor(color.red, color.green, color.blue);} 
};


PWIZ_API_DECL auto_ptr<Image> Image::create(int width, int height)
{
    return auto_ptr<Image>(new ImageImpl(width, height));
}


} // namespace util
} // namespace pwiz
