(*

此脚本导出理符任务到excel表格

*)

#I "../build/bin/"
#I "../build/bin/plugins/XivPlugin/"

#r "McMaster.NETCore.Plugins.dll"
#r "FsCqHttp.dll"
#r "TheBot.dll"
#r "Nlog.dll"
#r "LiteDB.dll"
#r @"XivPlugin.dll"
#r @"LibFFXIV.GameData.dll"
#r @"CsvParser.dll"

#r "nuget: EPPlus, 6.0.2-beta"


open System
open System.Drawing
open System.IO
open System.Collections.Generic
open System.Reflection

open KPX.TheBot.Host

open KPX.XivPlugin.Data


Environment.CurrentDirectory <- Path.Join(__SOURCE_DIRECTORY__, "../build/bin/")
let discover = HostedModuleDiscover()
discover.ScanAssembly(Assembly.GetAssembly(typeof<ItemCollection>))
discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

// 以上用于加载必须的配置文件，请勿改动

let col = ChinaDistroData.GetCollection()
