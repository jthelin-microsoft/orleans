#!/bin/bash
DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

export CONFIGURATION=Release
export OutDir=$DIR/Binaries/$CONFIGURATION

SrcDir=$DIR/src

NuGetDir=$SrcDir/.nuget
NuGetExe=$NuGetDir/NuGet.exe

if [ ! -f "$NuGetExe" ]
then
  echo Downloading NuGet.exe
  wget -O "$NuGetExe" http://nuget.org/nuget.exe
fi

xbuild /p:Configuration=$CONFIGURATION "$SrcDir/Orleans.sln"
