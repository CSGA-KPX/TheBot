(*

在不人工设定数据的前提下，使用LibFFXIV数据计算出潜水艇部件级别

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
open System.IO
open System.Collections.Generic
open System.Reflection
open System.Text.RegularExpressions

open KPX.TheBot.Host

open KPX.XivPlugin.Data


Environment.CurrentDirectory <- Path.Join(__SOURCE_DIRECTORY__, "../build/bin/")
let discover = HostedModuleDiscover()
discover.ScanAssembly(Assembly.GetAssembly(typeof<ItemCollection>))
discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

// 以上用于加载必须的配置文件，请勿改动

let col = ChinaDistroData.GetCollection()

type BuildInfo =
    { Item: XivItem
      PartRank: int
      ClassName: string
      FilterGroup: int
      Slot: int }

let shipClassDict =
    let subPart = col.SubmarinePart

    let submarineItems =
        col.Item
        |> Seq.filter (fun row -> row.FilterGroup.AsInt() = 36)
        |> Seq.map (fun item ->
            let p = subPart.[item.AdditionalData.AsInt()]

            { Item = ItemCollection.Instance.GetByItemId(item.Key.Main)
              ClassName =
                let itemName = item.Name.AsString()
                itemName.Remove(itemName.IndexOf('级'))
              PartRank = p.Rank.AsInt()
              FilterGroup = 36
              Slot = p.Slot.AsInt() })
        |> Seq.toArray
        |> Array.groupBy (fun info -> info.Slot)

    let classDict =
        let classAcc = Dictionary<string, string>()
        let items = submarineItems |> Array.head |> snd

        items
        |> Array.filter (fun info -> not <| info.ClassName.Contains("改"))
        |> Array.sortBy (fun info -> info.PartRank)
        |> Array.iteri (fun idx info ->
            classAcc.Add(info.ClassName, $"{idx + 1}")
            classAcc.Add(info.ClassName + "改", $"{idx + 1}改"))

        classAcc

    let outAcc = Dictionary<string, XivItem>()

    for (slot, slotItems) in submarineItems do
        for info in slotItems do
            let key = $"潜水艇:{info.ClassName}:{slot}"
            let key2 = $"潜水艇:{classDict.[info.ClassName]}:{slot}"
            outAcc.Add(key, info.Item)
            outAcc.Add(key2, info.Item)

    outAcc

let parse (input: string) =
    let pattern = "(?<part>[一-九1-9]改?){4}"
    let ms = Regex.Matches(input, pattern)

    if ms.Count = 1 then
        let succ, group = ms.[0].Groups.TryGetValue("part")

        if succ then
            let parts =
                group.Captures
                |> Seq.mapi (fun slotId capture ->
                    let item = shipClassDict.[$"潜水艇:{capture.Value}:{slotId}"]
                    $"{item.DisplayName}({item.ItemId})")

            printfn $"{String.Join('+', parts)}"
    else
        printfn "匹配失败！"

parse "1改2改3改4改"
parse "1111"
parse "1该1改1该1"
parse "4444"

(*
    潜水艇
    Slot : 0 船体；1 船尾；2 船首；4 舰桥
    ClassMapping :
        鲨鱼        3   1     ItemId
        甲鲎        2   2
        须鲸        1   3
        腔棘鱼      4   4
        希尔德拉    5   5

                    8   1改
                    7   2改
                    6   3改
                    9   4改
                   10   5改
        1，鲨鱼 -> 3(Rank)->Slot->ItemId
        Dict1 名称->Rank
        Dict2 Rank+Slot->ItemId
*)

(*
    飞空艇 不做了！
    Slot : 0 船体；1 气囊；2 船首；4 船尾
    ClassMapping ：
        野马   1
        企业   2
        奥德赛 3
        无敌   4
        无敌改 5
        塔塔诺拉 6
        威尔特甘斯 7


let airshipItems =
    let parts =
        col.AirshipExplorationPart
        |> Seq.map (fun row -> row.Key.Main, row)
        |> readOnlyDict

    col.Item
    |> Seq.filter (fun row -> row.FilterGroup.AsInt() = 28)
    |> Seq.map (fun row -> row.Name.AsString(), parts.[row.AdditionalData.AsInt()].Class.AsInt())
    |> Seq.sortBy (fun (a,b) -> b)
    |> Seq.toArray

*)