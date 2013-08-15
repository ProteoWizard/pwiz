REM run this to regenerate the java bindings - but there's not really any need to, probably
swig.exe -c++ -java -package proteowizard.pwiz.RAMPAdapter -outdir ..\java\proteowizard\pwiz\RAMPAdapter -o RAMPAdapter_wrap_java.cxx pwiz_swigbindings.i
