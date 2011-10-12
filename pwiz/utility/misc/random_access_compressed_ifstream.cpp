// $Id$
//
// This is just an istream which chooses its streambuf implementation
// based on whether the file is gzipped or not.  Could be extended
// to work with other compression formats, too.
//
// What makes it interesting compared to the classic gzstream implementation
// is the ability to perform seeks in a reasonably efficient manner.  In the
// event that a seek is requested (other than a rewind, or tellg()) the file 
// is decompressed once and snapshots of the compression state are made 
// every 1MB or so.  Further seeks are then quite efficient since they don't
// have to begin at the head of the file.
//
// Copyright (C) Insilicos LLC 2008, ALl Rights Reserved.
//
// draws heavily on example code from the zlib distro, so
// for conditions of distribution and use, see copyright notice in zlib.h
//
// Copyright (C) Insilicos LLC 2008, ALl Rights Reserved.
// For conditions of distribution and use, see copyright notice in zlib.h
//
// based on:
/* gzio.c -- IO on .gz files
* Copyright (C) 1995-2005 Jean-loup Gailly.
* For conditions of distribution and use, see copyright notice in zlib.h
*/

// efficient random access stuff based on
/* zran.c -- example of zlib/gzip stream indexing and random access
* Copyright (C) 2005 Mark Adler
* For conditions of distribution and use, see copyright notice in zlib.h
Version 1.0  29 May 2005  Mark Adler */


#define PWIZ_SOURCE


#include <cstdio>

#include "zlib.h"

#include "random_access_compressed_ifstream.hpp"

#if defined(_MSC_VER) || defined(__MINGW32__)  // MSVC or MinGW
#include <winsock2.h>
#include <sys/types.h>
#include <fcntl.h>
#include <io.h>
#else
#include <stdint.h>
#include <netinet/in.h>
#endif

#include <sys/stat.h>
#include <vector>
#include <cassert>
#include <cerrno>
#include <iostream>
#include <cstring>

namespace pwiz {
namespace util {

#ifndef Z_BUFSIZE
#  ifdef MAXSEG_64K
#    define Z_BUFSIZE 4096 /* minimize memory usage for 16-bit DOS */
#  else
#    define Z_BUFSIZE 16384
#  endif
#endif

#define DECOMPRESS_BUFSIZE 16384

#define ALLOC(size) malloc(size)
#define TRYFREE(p) {if (p) free(p); p=NULL;}

static int const gz_magic[2] = {0x1f, 0x8b}; /* gzip magic header */

/* gzip flag byte */
#define ASCII_FLAG   0x01 /* bit 0 set: file probably ascii text */
#define HEAD_CRC     0x02 /* bit 1 set: header CRC present */
#define EXTRA_FIELD  0x04 /* bit 2 set: extra field present */
#define ORIG_NAME    0x08 /* bit 3 set: original file name present */
#define COMMENT      0x10 /* bit 4 set: file comment present */
#define RESERVED     0xE0 /* bits 5..7: reserved */


#define SPAN 1048576L       /* desired distance between access points */
#define WINSIZE 32768U      /* sliding window size */
#define CHUNK 16384         /* file input buffer size */
/* access point entry */
class synchpoint {
public:
	random_access_compressed_ifstream_off_t out;          /* corresponding offset in uncompressed data */
	random_access_compressed_ifstream_off_t in;           /* offset in input file of first full byte */
	z_stream *state; // stream state
};
//
// here's where the real customization of the stream happens
//

class random_access_compressed_streambuf : public std::streambuf {
   friend class random_access_compressed_ifstream; // so we can modify some behaviors
public:
	random_access_compressed_streambuf(const char *path, std::ios_base::openmode mode);
	virtual ~random_access_compressed_streambuf();
	bool is_open() const;
	void close();
protected:
    virtual pos_type seekoff(off_type off,
		std::ios_base::seekdir way,
        std::ios_base::openmode which = std::ios_base::in); // we don't do out
    virtual pos_type seekpos(pos_type pos,
		std::ios_base::openmode which = std::ios_base::in); // we don't do out
	virtual int_type underflow(); // repopulate the input buffer
private:
	std::streampos my_seekg(std::streampos offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode); // streampos not the same as streamoff, esp in win32
	z_stream stream;
	int      z_err;   /* error code for last stream operation */
	int      z_eof;   /* set if end of input file */
	std::ifstream *infile;   /* raw .gz file we're reading */
	Byte     *inbuf;  /* input buffer */
	Byte     *outbuf; /* output buffer */
	uLong    crc;     /* crc32 of uncompressed data */
	char     *path;   /* path name for debugging only */
	random_access_compressed_ifstream_off_t  start;   /* start of compressed data in file (header skipped) */
	random_access_compressed_ifstream_off_t  uncompressedLength; /* total length of uncompressed file */
	std::streampos  last_seek_pos; /* last requested seek position (uncompressed) */
	std::streampos  outbuf_headpos; /* filepos for head of outbuf */
	std::streamoff	outbuf_len; /* length of outbuf last time we populated it */
	std::vector<synchpoint *> index; // index for random access
	/* Add an entry to the access point list. */
   synchpoint *addIndexEntry(random_access_compressed_ifstream_off_t in, random_access_compressed_ifstream_off_t out);

	// gzip stuff
	int do_flush(int flush);
	int get_byte();
	int  get_buf(int len);
	void  check_header();
	int    destroy();
	uLong  getLong();
	int build_index();
	void update_istream_ptrs(std::streampos new_headpos,int new_buflen,int new_posoffset=0) {
		outbuf_headpos = new_headpos; // note the decompressed filepos corresponding to buf head
		outbuf_len = new_buflen; // how many bytes in the buffer are consumable?
		// declare first, next, last for istream use
		setg((char *)outbuf,(char *)outbuf+new_posoffset,(char *)outbuf + new_buflen);
	}
	std::streampos get_next_read_pos() {
		return outbuf_headpos+boost::iostreams::offset_to_position(gptr()-(char *)outbuf);
	}
	bool pos_is_in_outbuf(std::streampos pos) {
		return ((outbuf_headpos <= pos) && 
			(pos < (outbuf_headpos + outbuf_len)));
	}
};


#define gzio_raw_readerror(s) (s->infile->bad())

// default ctor
PWIZ_API_DECL
random_access_compressed_ifstream::random_access_compressed_ifstream() :
std::istream(new std::filebuf()) {
	compressionType = NONE;
}

// constructor
PWIZ_API_DECL
random_access_compressed_ifstream::random_access_compressed_ifstream(const char *path) :
std::istream(new std::filebuf()) 
{
	open(path);
}

PWIZ_API_DECL
void random_access_compressed_ifstream::open(const char *path) {
	std::filebuf *fb = (std::filebuf *)rdbuf();
	fb->open(path,std::ios::binary|std::ios::in);
	bool gzipped = false;
	if (fb->is_open()) {
	   // check for gzip magic header
		gzipped = ((fb->sbumpc() == gz_magic[0]) && (fb->sbumpc() == gz_magic[1])); 
		fb->pubseekpos(0); // rewind
	} else {
		this->setstate(std::ios::failbit); // could not open, set the fail flag
	}
	if (gzipped) { // replace streambuf with gzip handler
		fb->close();
		rdbuf( new random_access_compressed_streambuf(path,std::ios_base::in|std::ios_base::binary));
		delete fb;
		compressionType = GZIP;
	} else {
		compressionType = NONE;
	}
}

PWIZ_API_DECL
bool random_access_compressed_ifstream::is_open() const { // for ease of use as ifstream replacement
	if (NONE == compressionType) {
		return ((std::filebuf *)rdbuf())->is_open();
	} else {
		return ((random_access_compressed_streambuf *)rdbuf())->is_open();
	}
}

PWIZ_API_DECL
void random_access_compressed_ifstream::close() {
	if (rdbuf()) {
	if (NONE == compressionType) {
		((std::filebuf *)rdbuf())->close();
	} else {
		((random_access_compressed_streambuf *)rdbuf())->close();
			// in case object gets reused
			delete rdbuf();
			rdbuf(new std::filebuf()); 
			compressionType = NONE;
		}
	}
}

PWIZ_API_DECL
random_access_compressed_ifstream::~random_access_compressed_ifstream()
{
	delete rdbuf();
	rdbuf(NULL); // so parent doesn't mess with it
}

random_access_compressed_streambuf::random_access_compressed_streambuf(const char *path, std::ios_base::openmode mode) {
    int err;
	if (!path) {
		return;
	}
	if (!(mode & std::ios::binary)) {
		return;
	}

	this->stream.zalloc = (alloc_func)0;
	this->stream.zfree = (free_func)0;
	this->stream.opaque = (voidpf)0;
	this->stream.next_in = this->inbuf = Z_NULL;
	this->stream.next_out = this->outbuf = Z_NULL;
	this->stream.avail_in = this->stream.avail_out = 0;
	this->last_seek_pos = -1;
	this->infile = new std::ifstream(path,std::ios::in|std::ios::binary);
	this->z_err = Z_OK;
	this->z_eof = 0;
	this->crc = crc32(0L, Z_NULL, 0);

	this->path = strdup(path);
	if ((this->path == NULL) || (this->infile->fail())) {
		this->destroy();
		return;
	}

	bool bOK;

	this->stream.next_in  = this->inbuf = (Byte*)ALLOC(Z_BUFSIZE);
	this->outbuf = (Byte*)ALLOC(DECOMPRESS_BUFSIZE);

	err = inflateInit2(&(this->stream), -MAX_WBITS);
	/* windowBits is passed < 0 to tell that there is no zlib header.
	* Note that in this case inflate *requires* an extra "dummy" byte
	* after the compressed stream in order to complete decompression and
	* return Z_STREAM_END. Here the gzip CRC32 ensures that 4 bytes are
	* present after the compressed stream.
	*/
	if (err != Z_OK || this->inbuf == Z_NULL) {
		this->destroy();
		return;
	}
	this->stream.avail_out = DECOMPRESS_BUFSIZE;

	errno = 0;
	bOK = (this->infile != NULL) && (this->infile->is_open());
	if (!bOK) {
		this->destroy();
		return;
	}
	this->check_header(); /* skip the .gz header */
   this->infile->clear(); // clear eof flag for short files
	this->start = this->infile->tellg() - (std::streamoff)(this->stream.avail_in);
	this->update_istream_ptrs(0,0); // we're pointed at head of file, but no read yet
}


bool random_access_compressed_streambuf::is_open() const { // for ifstream-ish-ness
	return (this->infile && this->infile->is_open());
}

void random_access_compressed_streambuf::close() { // for ifstream-ish-ness
	this->destroy();
}

random_access_compressed_streambuf::~random_access_compressed_streambuf() {
	this->destroy();
}



/* ===========================================================================
Read a byte from a random_access_gzstream; update next_in and avail_in. Return EOF
for end of file.
IN assertion: the stream s has been sucessfully opened for reading.
*/
int random_access_compressed_streambuf::get_byte()
{
	if (this->z_eof) {
		return EOF;
	}
	if (this->stream.avail_in == 0) {
		errno = 0;
		this->infile->read((char *)this->inbuf, Z_BUFSIZE);
		this->stream.avail_in = this->infile->gcount(); // how many did we read?
		if (this->stream.avail_in <= 0) {
			this->z_eof = 1;
			if (gzio_raw_readerror(this)) {
				this->z_err = Z_ERRNO;
			} else if (this->infile->eof()) {
				this->infile->clear(); // clear eof flag
			}
			return EOF;
		}
		this->stream.next_in = this->inbuf;
	}
	this->stream.avail_in--;
	return *(this->stream.next_in)++;
}

/* ===========================================================================
Check the gzip header of a random_access_gzstream opened for reading. set this->err
to Z_DATA_ERROR if the magic header is not present, or present but the rest of the header
is incorrect.
IN assertion: the stream s has already been created sucessfully;
this->stream.avail_in is zero for the first time, but may be non-zero
for concatenated .gz files.
*/
void random_access_compressed_streambuf::check_header()
{
	int method; /* method byte */
	int flags;  /* flags byte */
	uInt len;
	int c;

	/* Assure two bytes in the buffer so we can peek ahead -- handle case
	where first byte of header is at the end of the buffer after the last
	gzip segment */
	len = this->stream.avail_in;
	if (len < 2) {
		if (len) {
			this->inbuf[0] = this->stream.next_in[0];
		}
		errno = 0;
		this->infile->read((char *)(this->inbuf + len), Z_BUFSIZE >> len);
		len = this->infile->gcount();
		if (len <= 0 && gzio_raw_readerror(this)) {
			this->z_err = Z_ERRNO;
		}
		this->stream.avail_in += len;
		this->stream.next_in = this->inbuf;
		if (this->stream.avail_in < 2) {
			if (this->stream.avail_in) {
				this->z_err = Z_DATA_ERROR;
			}
			return;
		}
	}

	/* Peek ahead to check the gzip magic header */
	if (this->stream.next_in[0] != gz_magic[0] ||
		this->stream.next_in[1] != gz_magic[1]) {
			this->z_err = Z_DATA_ERROR;
			return;
	}
	this->stream.avail_in -= 2;
	this->stream.next_in += 2;

	/* Check the rest of the gzip header */
	method = this->get_byte();
	flags = this->get_byte();
	if (method != Z_DEFLATED || (flags & RESERVED) != 0) {
		this->z_err = Z_DATA_ERROR;
		return;
	}

	/* Discard time, xflags and OS code: */
	for (len = 0; len < 6; len++) {
		this->get_byte();
	}

	if ((flags & EXTRA_FIELD) != 0) { /* skip the extra field */
		len  =  (uInt)this->get_byte();
		len += ((uInt)this->get_byte())<<8;
		/* len is garbage if EOF but the loop below will quit anyway */
		while (len-- != 0 && this->get_byte() != EOF) ;
	}
	if ((flags & ORIG_NAME) != 0) { /* skip the original file name */
		while ((c = this->get_byte()) != 0 && c != EOF) ;
	}
	if ((flags & COMMENT) != 0) {   /* skip the .gz file comment */
		while ((c = this->get_byte()) != 0 && c != EOF) ;
	}
	if ((flags & HEAD_CRC) != 0) {  /* skip the header crc */
		for (len = 0; len < 2; len++) {
			this->get_byte();
		}
	}
	this->z_err = this->z_eof ? Z_DATA_ERROR : Z_OK;
}

/* ===========================================================================
* Cleanup then free the given random_access_gzstream. Return a zlib error code.
Try freeing in the reverse order of allocations.
*/
int random_access_compressed_streambuf::destroy ()
{
	int err = Z_OK;

	bool bClosedOK=true;
	if (this->stream.state != NULL) {
		err = inflateEnd(&(this->stream));
		if (this->infile) {
			delete this->infile;
		}
		this->infile = NULL;
	}
	// clean up the seek index list if any
	for (int i=(int)this->index.size();i--;) {
		inflateEnd(this->index[i]->state);
		delete this->index[i]->state;
		delete this->index[i];
	}
	this->index.clear(); // set length 0
	if (!bClosedOK) {
#ifdef ESPIPE
		if (errno != ESPIPE) /* fclose is broken for pipes in HP/UX */
#endif
			err = Z_ERRNO;
	}
	if (this->z_err < 0) {
		err = this->z_err;
	}

	TRYFREE(this->inbuf);
	TRYFREE(this->outbuf);
	TRYFREE(this->path);
	return err;
}

//
// this gets called each time ifstream uses up its input buffer
//
random_access_compressed_streambuf::int_type random_access_compressed_streambuf::underflow() {
	int nread = 0;
	int len = DECOMPRESS_BUFSIZE; // we'll try to read the next full chunk

	if (this->last_seek_pos >= 0) { // we had a seek request

		/* here's where we tear out the inefficient default seek behavior and replace
		with the stuff from zran.c - bpratt */

		/* Use the index to read len bytes from offset into buf, return bytes read or
		negative for error (Z_DATA_ERROR or Z_MEM_ERROR).  If data is requested past
		the end of the uncompressed data, then extract() will return a value less
		than len, indicating how much as actually read into buf.  This function
		should not return a data error unless the file was modified since the index
		was generated.  extract() may also return Z_ERRNO if there is an error on
		reading or seeking the input file. */

		int skip, ret;
		unsigned char *discard = new unsigned char[WINSIZE];
		random_access_compressed_ifstream_off_t offset = 
			boost::iostreams::position_to_offset(this->last_seek_pos); // last requested absolute uncompress filepos
		std::streampos  next_read_pos = this->last_seek_pos; // will be new outbuf head pos
		this->last_seek_pos = -1; // satisfied it

		// first locate the index entry which will get us at or just before target
		size_t ind = this->index.size();
		while (--ind && this->index[ind]->out > offset);
		// and prepare to decompress
		synchpoint *synch = this->index[ind];
		inflateEnd(&this->stream); // tidy up old ptrs
		inflateCopy(&this->stream,synch->state);
		z_stream &strm = this->stream;
      assert(strm.total_in == synch->in-this->start);
		this->infile->clear(); // clear eof flag if any
		this->infile->seekg(boost::iostreams::offset_to_position(synch->in));

		/* skip uncompressed bytes until offset reached */
		offset -= synch->out;  // now offset is the number of uncompressed bytes we need to skip
		strm.avail_in = 0; // inflateCopy doesn't retain the input buffer
		skip = 1;                               /* while skipping to offset */
		do {
			/* define where to put uncompressed data, and how much */
			if (offset == 0 && skip) {          /* at offset now */
				strm.avail_out = len;
				strm.next_out = (Bytef *)this->outbuf;
				skip = 0;                       /* only do this once */
			}
			if (offset > WINSIZE) {             /* skip WINSIZE bytes */
				strm.avail_out = WINSIZE;
				strm.next_out = discard;
				offset -= WINSIZE;
			}
			else if (offset != 0) {             /* last skip */
				strm.avail_out = (unsigned)offset;
				strm.next_out = discard;
				offset = 0;
			}

			/* uncompress until avail_out filled, or end of stream */
			do {
				if (strm.avail_in == 0) {
					this->infile->read((char *)this->inbuf, CHUNK);
					strm.avail_in = this->infile->gcount();
					if (gzio_raw_readerror(this)) {
						ret = Z_ERRNO;
						goto perform_seek_ret;
					} else if (this->infile->eof()) {
						this->infile->clear(); // clear eof flag
					}
					if (strm.avail_in == 0) {
						ret = Z_DATA_ERROR;
						goto perform_seek_ret;
					}
					strm.next_in = this->inbuf;
				}
				ret = inflate(&strm, Z_NO_FLUSH);       /* normal inflate */
				if (ret == Z_NEED_DICT)
					ret = Z_DATA_ERROR;
				if (ret == Z_MEM_ERROR || ret == Z_DATA_ERROR)
					goto perform_seek_ret;
				if (ret == Z_STREAM_END) {
					break;
				}
			} while (strm.avail_out != 0);

			/* if reach end of stream, then don't keep trying to get more */
			if (ret == Z_STREAM_END) {
				// reached EOF, clear fail bit
				this->infile->clear();
				break;
			}

			/* do until offset reached and requested data read, or stream ends */
		} while (skip);
		nread = skip ? 0 : len - strm.avail_out;
		update_istream_ptrs(next_read_pos,nread);
		/* return error if any */
perform_seek_ret:
		delete []discard;

	} else {
      std::streampos buftailpos = get_next_read_pos(); // buf tail will be buf head unless we seek

		Bytef *start = (Bytef*)this->outbuf; /* starting point for crc computation */
		Byte  *next_out; /* == stream.next_out but not forced far (for MSDOS) */

		next_out = (Byte*)start;
		this->stream.next_out = next_out;
		this->stream.avail_out = len;

		while (this->stream.avail_out != 0) {

			if (this->stream.avail_in == 0 && !this->z_eof) {

				errno = 0;
				this->infile->read((char *)this->inbuf, Z_BUFSIZE);
				this->stream.avail_in = this->infile->gcount();
				if (this->stream.avail_in <= 0) {
					this->z_eof = 1;
					if (gzio_raw_readerror(this)) {
						this->z_err = Z_ERRNO;
						break;
					} else if (this->infile->eof()) {
						this->infile->clear(); // clear eof flag
					}
				}
				this->stream.next_in = this->inbuf;
			}
			this->z_err = inflate(&(this->stream), Z_NO_FLUSH);

			if (this->z_err != Z_OK || this->z_eof) {
				break;
			}
		}
		this->crc = crc32(this->crc, start, (uInt)(this->stream.next_out - start));

		if (len == (int)this->stream.avail_out &&
			(this->z_err == Z_DATA_ERROR || this->z_err == Z_ERRNO)) {
				return traits_type::eof();;
		}
		nread = (int)(len - this->stream.avail_out); // how many bytes actually read?
		update_istream_ptrs(buftailpos,nread); // update outbuf head position, length
	}
	return (nread>0)?outbuf[0]:traits_type::eof();
}

std::streampos random_access_compressed_streambuf::seekpos(std::streampos pos,std::ios_base::openmode Mode) {
	return my_seekg(pos,std::ios_base::beg,Mode);
}

std::streampos random_access_compressed_streambuf::seekoff(std::streamoff offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	return my_seekg(boost::iostreams::offset_to_position(offset),whence,Mode);
}

std::streampos random_access_compressed_streambuf::my_seekg(std::streampos offset, std::ios_base::seekdir whence,std::ios_base::openmode Mode) {
	if (!this->infile || !this->infile->is_open() || (!(Mode & std::ios_base::in)) ||
		this->z_err == Z_ERRNO || this->z_err == Z_DATA_ERROR) {
			return -1;
	 }
	// watch out for a simple rewind or tellg
	if (0==offset) {
		if (whence == std::ios_base::cur) {
			return get_next_read_pos(); // nothing to do
		}
		if ((whence == std::ios_base::beg) && // just a rewind
			!this->index.size()) { // no index yet
			// do we already have this decompressed?
			if (pos_is_in_outbuf(offset) || // already in buffer
				(offset==outbuf_headpos)) // or buffer is not yet loaded, let underflow() do it
			{
				update_istream_ptrs(outbuf_headpos,outbuf_len);
			} else  { // rewind without provoking index build
				this->stream.avail_in = 0;
				this->stream.avail_out = 0;
				this->stream.total_in = 0;
				this->stream.total_out = 0;
				this->stream.next_in = this->inbuf;
				this->crc = crc32(0L, Z_NULL, 0);
				(void)inflateReset(&this->stream);
				this->last_seek_pos = -1; // no need to seek
				this->infile->seekg(boost::iostreams::offset_to_position(this->start));
				update_istream_ptrs(0,0); // blow the cache
			}
			return 0; 
      } // end just a rewind and no index yet
   } // end if offset==0

	this->z_err = Z_OK;
	this->z_eof = 0;
	if (!this->index.size()) { // first seek - build index
		this->build_index();
      update_istream_ptrs(0,0); // blow the cache
	}
	/* find where in stream to start */
	/* compute absolute position */
	std::streampos pos = offset;
	if (whence == std::ios_base::cur) {
		pos += this->get_next_read_pos();
	} else if (whence == std::ios_base::end) {
		pos += boost::iostreams::offset_to_position(this->uncompressedLength);
	}

	// do we already have this decompressed?
	if (pos_is_in_outbuf(pos)) {
		update_istream_ptrs(outbuf_headpos,outbuf_len,pos-outbuf_headpos);
      this->last_seek_pos = -1; // no need to actually seek
	} else {
  	    // just note the request for now, actually seek at read time
		this->last_seek_pos = pos;
      update_istream_ptrs(outbuf_headpos,0); // blow the cache
	}
   return pos;
}

/* ===========================================================================
Reads a long in LSB order from the given random_access_gzstream. Sets z_err in case
of error.
*/
uLong random_access_compressed_streambuf::getLong ()
{
	uLong x = (uLong)this->get_byte();
	int c;

	x += ((uLong)this->get_byte())<<8;
	x += ((uLong)this->get_byte())<<16;
	c = this->get_byte();
	if (c == EOF) {
		this->z_err = Z_DATA_ERROR;
	}
	x += ((uLong)c)<<24;
	return x;
}


//
// from here we're stealing from zran.c
//

/* Add an entry to the access point list. */
synchpoint *random_access_compressed_streambuf::addIndexEntry(random_access_compressed_ifstream_off_t in, random_access_compressed_ifstream_off_t out)
{
	/* fill in entry and increment how many we have */
	synchpoint *next = new synchpoint();
	if (next) {
		next->in = in;
		next->out = out;
		next->state = new z_stream;
		inflateCopy(next->state,&this->stream);
		this->index.push_back(next);
	}
	return next;
}

/* Make one entire pass through the compressed stream and build an index, with
access points about every span bytes of uncompressed output -- span is
chosen to balance the speed of random access against the memory requirements
of the list, about 32K bytes per access point.  Note that data after the end
of the first zlib or gzip stream in the file is ignored.  build_index()
returns 0 on success, Z_MEM_ERROR for out of memory, Z_DATA_ERROR for an error in 
the input file, or Z_ERRNO for a file read error.  
On success, *built points to the resulting index. */
int random_access_compressed_streambuf::build_index()
{
	int ret;
	random_access_compressed_ifstream_off_t span=SPAN;
	random_access_compressed_ifstream_off_t totin, totout;        /* our own total counters to avoid 4GB limit */
	random_access_compressed_ifstream_off_t last;                 /* totout value of last access point */
	unsigned char *input = new unsigned char[CHUNK];
	unsigned char *window = new unsigned char[WINSIZE];
	z_stream &strm = this->stream;

	/* initialize inflate */
   this->stream.avail_in = 0;
   this->stream.avail_out = 0;
   this->stream.total_in = 0;
   this->stream.total_out = 0;
   this->stream.next_in = this->inbuf;
   this->crc = crc32(0L, Z_NULL, 0);
   ret = inflateReset(&strm); 
   this->infile->clear(); // clear stale eof bit if any
   this->infile->seekg((std::streamoff)this->start); // rewind
	if (ret != Z_OK) {
		goto build_index_error;
	}

	/* inflate the input, maintain a sliding window, and build an index -- this
	also validates the integrity of the compressed data using the check
	information at the end of the gzip or zlib stream */
   totout = last = 0;
   totin = this->start;
   this->addIndexEntry(totin,totout); // note head of file

	do {
		/* get some compressed data from input file */
		this->infile->read((char *)input, CHUNK);
		strm.avail_in = this->infile->gcount();
		if (gzio_raw_readerror(this)) {
			ret = Z_ERRNO;
			goto build_index_error;
		}
		if (strm.avail_in == 0) {
			ret = Z_DATA_ERROR;
			goto build_index_error;
		}
		strm.next_in = input;

		/* process all of that, or until end of stream */
		do {
			/* reset sliding window if necessary */
			if (strm.avail_out == 0) {
				strm.avail_out = WINSIZE;
				strm.next_out = window;
			}

			/* inflate until out of input, output, or at end of block --
			update the total input and output counters */
			totin += strm.avail_in; // note input filepos at start of inflate
			totout += strm.avail_out; // note uncompressed filepos prior to inflate
			ret = inflate(&strm, Z_BLOCK);      /* return at end of block */
			totin -= strm.avail_in; // if we use this as a synchpoint we'll have to repopulate the input buffer
			totout -= strm.avail_out;
			if (ret == Z_NEED_DICT)
				ret = Z_DATA_ERROR;
			if (ret == Z_MEM_ERROR || ret == Z_DATA_ERROR)
				goto build_index_error;
			if (ret == Z_STREAM_END) {
				// reached end successfully
				this->infile->clear(); // clear the fail bit
				break;
			}

         /* add an index entry every 'span' bytes   */
         if (( totout - last) > span) {
            if (!this->addIndexEntry(totin,totout)) {
							ret = Z_MEM_ERROR;
							goto build_index_error;
					}
					last = totout;
			}
		} while (strm.avail_in != 0);
	} while (ret != Z_STREAM_END);

	/* return index (release unused entries in list) */
	this->uncompressedLength = totout;

	/* return error */
build_index_error:
	delete[] window;
	delete[] input;
	return ret;
}

} // namespace util
} // namespace pwiz 

