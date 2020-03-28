@echo off
Echo ---------------------- Build Start -------------------------------------

IF NOT "%VS160COMNTOOLS%" == "" (call "%VS160COMNTOOLS%vsvars32.bat")
IF NOT "%VS150COMNTOOLS%" == "" (call "%VS150COMNTOOLS%vsvars32.bat")
IF NOT "%VS140COMNTOOLS%" == "" (call "%VS140COMNTOOLS%vsvars32.bat")
IF NOT "%VS130COMNTOOLS%" == "" (call "%VS130COMNTOOLS%vsvars32.bat")
IF NOT "%VS120COMNTOOLS%" == "" (call "%VS120COMNTOOLS%vsvars32.bat")
IF NOT "%VS110COMNTOOLS%" == "" (call "%VS110COMNTOOLS%vsvars32.bat")

for /F %%A in ('dir /b ..\*.sln') do call devenv ..\%%A /Rebuild "Release" 

Echo ------------------------------------------------------------------------

Echo ---------------------- Nuget Packaging ---------------------------------

mkdir ..\..\NuGetPackages 2> NUL
nuget pack -OutputDirectory ..\..\NuGetPackages package.nuspec

pause