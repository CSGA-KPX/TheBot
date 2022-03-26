(*
@echo off
pushd bin
cls
dotnet fsi --use:..\Run_REPL.fsx
goto :EOF
*)

// DO NOT USE NON-ASCII CHARS!

#I "bin/"

#r "McMaster.NETCore.Plugins.dll"
#r "FsCqHttp.dll"
#r "TheBot.dll"
#r @"bin\plugins\XivPlugin\XivPlugin.dll"

open System
open System.IO
open System.Reflection

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance

open KPX.TheBot.Host
open KPX.TheBot.Host.Data

open KPX.XivPlugin

Environment.CurrentDirectory <- Path.Join(__SOURCE_DIRECTORY__, "../build/bin/")
let discover = HostedModuleDiscover()
discover.ScanAssembly(Assembly.GetAssembly(typeof<ItemCollection>))
discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

