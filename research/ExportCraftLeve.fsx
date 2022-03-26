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

let leveData =
    seq {
        for row in col.CraftLeve.TypedRows do
            if row.Leve.AsInt() <> 0 then
                let leve = row.Leve.AsRow()
                let name = leve.Name.AsString()
                let level = leve.ClassJobLevel.AsInt()
                let classjob = leve.ClassJobCategory.AsRow().Name.AsString()
                let repeats = row.Repeats.AsInt() + 1

                let items =
                    let items = row.Item.AsRows()
                    let counts = row.ItemCount.AsInts()

                    [| for i = 0 to items.Length - 1 do
                           let item = items.[i]
                           let count = counts.[i]

                           if item.Key.Main <> 0 then
                               (item.Name.AsString(), count * repeats) |]

                if classjob <> "捕鱼人" then
                    {| Name = name
                       Level = level
                       ClassJob = classjob
                       Items = items |}
    }
    |> Seq.toArray
    |> Array.sortByDescending (fun leve -> leve.Level)
    |> Array.groupBy (fun leve -> leve.ClassJob)

open OfficeOpenXml

let p = new ExcelPackage()

for (job, leves) in leveData do
    let sb = Text.StringBuilder()
    sb.AppendLine("名称,等级,物品,数量") |> ignore
    for leve in leves do
        for (item, count) in leve.Items do
            sb.AppendLine($"{leve.Name},{leve.Level},{item},{count}") |> ignore
    let ws = p.Workbook.Worksheets.Add(job)
    ws.Cells.["A1"].LoadFromText(sb.ToString()) |> ignore
    ws.Cells.AutoFitColumns() |> ignore
p.SaveAs(new FileInfo(Path.Join(__SOURCE_DIRECTORY__, "职业理符.xlsx")))

printfn "Done!"


