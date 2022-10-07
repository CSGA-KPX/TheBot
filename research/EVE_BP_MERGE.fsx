(*

此脚本导出理符任务到excel表格

*)

#I "../build/bin/"
#I "../build/bin/plugins/EvePlugin/"

#r "McMaster.NETCore.Plugins.dll"
#r "FsCqHttp.dll"
#r "TheBot.dll"
#r "Nlog.dll"
#r "LiteDB.dll"
#r @"EvePlugin.dll"


open System
open System.IO
open System.Collections.Generic
open System.Reflection

open KPX.TheBot.Host.Utils.HandlerUtils
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process

open KPX.EvePlugin.Utils.Helpers
open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.Data
open KPX.EvePlugin.Utils.Extensions


let cmdQueue = ResizeArray<string>()
let mutable state = true

while state do
    if cmdQueue.Count = 0 then
        printf "List> "

    let line = Console.In.ReadLine()

    if String.IsNullOrWhiteSpace(line) then
        let cmd = String.Join("\r\n", cmdQueue)

        if not <| String.IsNullOrWhiteSpace(cmd) then
            state <- false
    else
        cmdQueue.Add(line)

let acc = ItemAccumulator<EveType * int>()

for line in cmdQueue do
    let t = line.Split('\t')

    if t.Length > 4 then
        let item = t.[0] |> EveTypeCollection.Instance.GetByName
        let ime = t.[1] |> int
        //let te = t.[2]

        let key = item, ime
        let runs = t.[3] |> float
        acc.Update(key, runs)

for kv in acc do
    let item, ime = kv.Item
    let runs = kv.Quantity

    let quantity =
        let cfg = EveConfigParser()
        cfg.SetDefaultInputMe(ime)
        cfg.Parse(String.Empty)

        let epm = EveProcessManager(cfg)
        let proc = epm.GetRecipe(item)
        proc.Original.Product.Quantity * runs

    printfn $"{item.Name}*{quantity} ime:{ime}"
