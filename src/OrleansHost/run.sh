#!/bin/bash
DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

siloName="MonoSilo"
##if [ -e "$1" ]
##then
##  siloName=$1
##  shift
##fi

cd $DIR

echo SiloName= $siloName
echo Starting Orleans node in `pwd`
mono OrleansHost.exe $siloName $*
