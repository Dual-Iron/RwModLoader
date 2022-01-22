#!/bin/sh
cd "$(dirname "$0")"
dotnet restore
dotnet build VirtualEnums -c Release --no-restore
dotnet build Realm -c Release --no-restore
dotnet publish Mutator -c Release -f net6.0 -r win-x64 --self-contained -o "output" -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=link -p:TrimmerDefaultAction=link -p:EnableCompressionInSingleFile=true
mv output/backend.exe UserInstaller.exe
