@for %%f in (..\bin\*.nupkg) do @..\.nuget\NuGet.exe push %%f
pause