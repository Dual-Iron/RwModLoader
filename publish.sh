#!/bin/sh
cd "$(dirname "$0")"
dotnet restore
dotnet build VirtualEnums -c Release --no-restore
dotnet build Realm -c Release --no-restore
dotnet publish Backend -c Release -f net6.0 -r win-x64 --self-contained -o "output" -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=link -p:TrimmerDefaultAction=link -p:EnableCompressionInSingleFile=true
cp -a output/backend.exe UserInstaller.exe
cp -a RwBep ManualInstall
mkdir "ManualInstall/BepInEx/realm"
cp -a output/backend.exe ManualInstall/BepInEx/realm/backend.exe
