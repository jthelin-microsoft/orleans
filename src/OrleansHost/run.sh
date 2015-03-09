pushd `dirname $0` > /dev/null
ScriptDir=`pwd`
popd > /dev/null

siloName="MonoSilo"
##if [ -e "$1" ]
##then
##  siloName=$1
##  shift
##fi

cd $ScriptDir

echo SiloName= $siloName
echo Starting Orleans node in `pwd`
mono OrleansHost.exe $siloName $*
