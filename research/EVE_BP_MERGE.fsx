(*

此脚本导出理符任务到excel表格

*)

#I "../build/bin/"
#I "../build/bin/plugins/EvePlugin/"

#r "Newtonsoft.Json.dll"
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

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance
open KPX.FsCqHttp.Testing

open KPX.TheBot.Host
open KPX.TheBot.Host.Data


let blueprints =
    let cmdQueue = File.ReadAllLines("EVE_BP_MERGE_BP.txt")
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

    let ret = Text.StringBuilder()

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

        ret.AppendLine($"{item.Name}*{quantity} ime:{ime} dme:8") |> ignore

    ret.ToString()

let materials = File.ReadAllText("EVE_BP_MERGE_INV.txt")


let discover =
    let binPath = __SOURCE_DIRECTORY__ + "/../build/bin/"

    NLog.LogManager.LoadConfiguration(binPath + "NLog.config")
    |> ignore

    Environment.CurrentDirectory <- Path.Join(binPath)

    let discover = HostedModuleDiscover()
    discover.ScanPlugins(IO.Path.Combine(binPath, "plugins"))
    discover.ScanAssembly(Assembly.GetExecutingAssembly())
    discover.ScanAssembly(typeof<Data.DataAgent>.Assembly)
    discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

    discover

let ctx =
    let userId =
        Environment.GetEnvironmentVariable("REPL_UserId")
        |> Option.ofObj<string>
        |> Option.map uint64
        |> Option.defaultValue 10000UL
        |> UserId

    let userName =
        Environment.GetEnvironmentVariable("REPL_UserName")
        |> Option.ofObj<string>
        |> Option.defaultValue "测试机"

    TestContext(discover, userId, userName)

let printMsg (msgs : Message.ReadOnlyMessage []) =
    for msg in msgs do
        for seg in msg do
            Console.Out.WriteLine("msg>>")

            if seg.TypeName = "text" then
                Console.Out.Write(seg.Values.["text"])
            else
                Console.Out.Write($"[{seg.TypeName}]")

            Console.WriteLine()

Console.WriteLine("-----Set INV----")
ctx.InvokeCommand($"#eveinv id:KPX\r\n{materials}") |> printMsg

Console.WriteLine("-----calc witnout inv----")
ctx.InvokeCommand($"#err text: dme:8 \r\n{blueprints}")  |> printMsg

Console.WriteLine("-----calc witn inv----")
ctx.InvokeCommand($"#err text: dme:8 id:KPX \r\n{blueprints}")  |> printMsg
