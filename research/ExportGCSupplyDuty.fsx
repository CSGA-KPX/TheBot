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


let jobOrder =
    let jobMap =
        seq {
            for row in col.ClassJob.TypedRows do
                yield row.Abbreviation.AsString(), row.Name.AsString()
        }
        |> dict

    [| "CRP"
       "CUL"
       "ALC"
       "ARM"
       "LTW"
       "GSM"
       "WVR"
       "BSM"

       "MIN"
       "BTN"
       "FSH" |]
    |> Array.map (fun k -> jobMap.[k])

fsi.AddPrinter(fun (p: PatchNumber) -> $"Patch{p.PatchNumber}")

let SupplyDuties =
    [| for row in col.GCSupplyDuty.TypedRows do
           let level = row.Key.Main
           let items = row.Item.AsRows()
           let counts = row.ItemCount.AsInts()

           for i = items.GetLowerBound(1) to items.GetUpperBound(1) do
               let job = jobOrder.[i]

               for j = items.GetLowerBound(0) to items.GetUpperBound(0) do
                   let item = items.[j, i]
                   let count = counts.[j, i]

                   if item.Key.Main <> 0 then
                       {| Name = item.Name.AsString()
                          Count = count
                          Job = job
                          Level = level
                          Patch = PatchNumber.FromPlayerLevel(level) |} |]
    |> Array.sortBy (fun duty -> duty.Level)
    |> Array.groupBy (fun duty -> duty.Job)
