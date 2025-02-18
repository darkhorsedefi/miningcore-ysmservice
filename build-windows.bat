@echo off

REM Check prerequisites
where cmake >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo CMake is required but not found.
    echo Please install CMake from https://cmake.org/download/
    exit /b 1
)

REM Check environment variables
if not defined LIBSNARK_ROOT (
    echo LIBSNARK_ROOT environment variable is not set
    echo Please set it to the libsnark installation directory
    exit /b 1
)

if not defined GMP_ROOT (
    echo GMP_ROOT environment variable is not set
    echo Please set it to the GMP installation directory
    exit /b 1
)

if not defined BOOST_ROOT (
    echo BOOST_ROOT environment variable is not set
    echo Please set it to the Boost installation directory
    exit /b 1
)

REM Build all Native libraries
cd src\Native

REM Build existing libraries
msbuild libmultihash\libmultihash.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% neq 0 exit /b 1

msbuild libcryptonight\libcryptonight.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% neq 0 exit /b 1

msbuild libethhash\libethhash.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% neq 0 exit /b 1

msbuild libxehash\libxehash.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% neq 0 exit /b 1

REM Build ZKSnark library
msbuild libzksnark\libzksnark.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% neq 0 exit /b 1

cd ..\..

REM Build Miningcore
dotnet build Miningcore.sln -c Release
if %ERRORLEVEL% neq 0 exit /b 1

echo Build completed successfully
