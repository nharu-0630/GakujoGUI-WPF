timeout 1
pushd .
cd net6.0-windows10.0.18362.0
move * ../  
popd 
rd net6.0-windows10.0.18362.0
start GakujoGUI.exe