@echo off
setlocal EnableExtensions EnableDelayedExpansion
title llama-server

rem ==== settings ============================================================
set "LLAMA_DIR=f:\Robot\llamacpp"
set "HOST=127.0.0.1"
set "PORT=8080"
set "CTX=65536"

rem ==== model catalogue =====================================================
rem <N>_NAME - label shown in the menu
rem <N>_PATH - path to the .gguf file
rem <N>_ARGS - extra arguments used only by this model
rem To add a model: bump MODEL_COUNT and add the three lines below.
set "MODEL_COUNT=2"

set "MODEL_1_NAME=Cyber-Tiel-Coder-35B-A3B (MTP)"
set "MODEL_1_PATH=f:\ML\models\Cyber-Tiel-Coder-35B-A3B-MTP-UD-Q2_K_XL.gguf"
set "MODEL_1_ARGS=--spec-type draft-mtp --spec-draft-n-max 2"

set "MODEL_2_NAME=Qwen3.8-27B"
set "MODEL_2_PATH=f:\Robot\llm_models\Qwen3.8-27B-UD-IQ3_XXS.gguf"
set "MODEL_2_ARGS="

rem ==== argument parsing ====================================================
rem Usage: start-server.bat [model-number] [extra llama-server args...]
rem Examples:
rem   start-server.bat                -> menu
rem   start-server.bat 2              -> Qwen3.8-27B
rem   start-server.bat 2 --port 8081  -> Qwen3.8-27B on another port
rem   start-server.bat --help         -> model 1 with --help
set "SEL="
set "EXTRA="

:parse_args
if "%~1"=="" goto :parsed
set "PEEK=%~1"
if "%PEEK:~0,1%"=="-" goto :flag_arg
if defined SEL goto :extra_arg
set "SEL=%PEEK%"
shift
goto :parse_args

:flag_arg
if defined SEL goto :extra_arg
set "SEL=1"
goto :extra_arg

:extra_arg
set "EXTRA=%EXTRA% %1"
shift
goto :parse_args

rem ==== model selection ====================================================
:parsed
if defined SEL goto :resolve

:menu
echo Available models:
for /l %%i in (1,1,%MODEL_COUNT%) do call :print_model %%i
echo.
set "SEL="
set /p "SEL=Select model [1-%MODEL_COUNT%] (Enter = 1): "
if not defined SEL set "SEL=1"

:resolve
set "IDX="
for /l %%i in (1,1,%MODEL_COUNT%) do if "%SEL%"=="%%i" set "IDX=%%i"
if not defined IDX (
    echo [ERROR] unknown model: %SEL%
    echo [HINT] valid values: 1..%MODEL_COUNT%
    goto :fail
)
set "MODEL_NAME=!MODEL_%IDX%_NAME!"
set "MODEL_PATH=!MODEL_%IDX%_PATH!"
set "MODEL_ARGS=!MODEL_%IDX%_ARGS!"
if not defined MODEL_PATH goto :fail
title llama-server - %MODEL_NAME%

rem ==== pre-flight checks ==================================================
cd /d "%LLAMA_DIR%"
if errorlevel 1 (
    echo [ERROR] cannot cd to "%LLAMA_DIR%"
    goto :fail
)
if not exist "%LLAMA_DIR%\llama-server.exe" (
    echo [ERROR] llama-server.exe not found in "%LLAMA_DIR%"
    goto :fail
)
if not exist "%MODEL_PATH%" (
    echo [ERROR] model not found: "%MODEL_PATH%"
    goto :fail
)

rem let explicit --host/--port from the command line win over the defaults
set "HOST_ARG=--host %HOST%"
set "PORT_ARG=--port %PORT%"
echo "%EXTRA%" | findstr /i /c:"--host" >nul && set "HOST_ARG="
echo "%EXTRA%" | findstr /i /c:"--port" >nul && set "PORT_ARG="

echo [INFO] model : %MODEL_NAME%
echo [INFO] file  : %MODEL_PATH%
echo [INFO] server: http://%HOST%:%PORT%
echo.

rem ==== run ================================================================
llama-server.exe ^
    -m "%MODEL_PATH%" ^
    -ngl 99 ^
    --jinja ^
    %MODEL_ARGS% ^
    -c %CTX% ^
    -fa on ^
    -ctk q8_0 ^
    -ctv q8_0 ^
    %HOST_ARG% ^
    %PORT_ARG% ^
    %EXTRA%

set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo.
    echo [ERROR] llama-server exited with code %RC%
    pause
)
endlocal & exit /b %RC%

rem ==== subroutines ========================================================
:print_model
echo   %~1) !MODEL_%~1_NAME!
echo        !MODEL_%~1_PATH!
exit /b 0

:fail
echo.
echo [INFO] window closes in 15 seconds...
timeout /t 15 >nul 2>nul
endlocal & exit /b 1
