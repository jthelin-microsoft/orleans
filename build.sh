pushd `dirname $0` > /dev/null
ScriptDir=`pwd`
popd > /dev/null

export CONFIGURATION=Release
export OutDir=$ScriptDir/Binaries/$CONFIGURATION

SrcDir=$ScriptDir/src

NuGetDir=$SrcDir/.nuget
NuGetExe=$NuGetDir/NuGet.exe

if [ ! -f $NuGetExe ]
then
  echo Downloading NuGet.exe
  wget -O $NuGetExe http://nuget.org/nuget.exe
fi

xbuild /p:Configuration=$CONFIGURATION $SrcDir/Orleans.sln
