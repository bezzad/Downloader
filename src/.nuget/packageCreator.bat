@echo off
Echo ---------------------- Nuget Packaging ---------------------------------

mkdir ..\..\NuGetPackages 2> NUL
nuget pack -OutputDirectory ..\..\NuGetPackages package.nuspec

Echo ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

pause