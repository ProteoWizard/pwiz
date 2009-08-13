#!/bin/sh

pushd $1/libraries > /dev/null

# test for last file extracted so we re-extract after a partial extraction
if [ ! -e boost_1_39_0/tools/jam/test/var_expand.jam ]; then
	echo "Extracting boost tarball..."
	
	# delete existing boost directory in case of partial extraction (because the last file extracted may be incomplete)
  if [ -e boost_1_39_0 ]; then
    rm -fdr boost_1_39_0
  fi

  # we only extract [chi]pp from boost_1_39_0/libs and [hi]pp from boost_1_39_0/boost
	tar -xkjf boost_1_39_0.tar.bz2 "boost_1_39_0/libs*.?pp" "boost_1_39_0/boost*.?pp" boost_1_39_0/tools/jam boost_1_39_0/tools/build "boost_1_39_0/libs*timeconv.inl"
fi

popd > /dev/null
