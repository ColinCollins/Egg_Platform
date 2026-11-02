set PROJECT=%~dp0..\..
set WORKSPACE=%PROJECT%\Luban
set LUBAN_DLL=%WORKSPACE%\Luban\Luban.dll
set CONF_ROOT=%WORKSPACE%\Configs
set ASSET_ROOT=%PROJECT%\Assets
set RESOURCES=%ASSET_ROOT%\Game\Resources
set GAMECONFIG=%ASSET_ROOT%\Game

dotnet %LUBAN_DLL% ^
    -t client ^
    -d json ^
    -c cs-simple-json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%ASSET_ROOT%\Configs\Code ^
    -x outputDataDir=%GAMECONFIG%\Configs\

pause