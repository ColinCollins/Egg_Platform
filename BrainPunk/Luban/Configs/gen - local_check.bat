set PROJECT=%~dp0..\..
set WORKSPACE=%PROJECT%\Luban
set LUBAN_DLL=%WORKSPACE%\Luban\Luban.dll
set CONF_ROOT=%WORKSPACE%\Configs

dotnet %LUBAN_DLL% ^
    -t client ^
    -d json ^
    -c cs-simple-json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%WORKSPACE%\Configs\Code ^
    -x outputDataDir=%WORKSPACE%\Configs\Output

pause