timeout 1
cd %~dp0
call powershell -command "Expand-Archive -Force net6.0-windows10.0.18362.0.zip ./"
del net6.0-windows10.0.18362.0.zip
start GakujoGUI.exe