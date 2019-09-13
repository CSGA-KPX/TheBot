module DiceModule
open System
open KPX.FsCqHttp.DataType.Event
open KPX.FsCqHttp.Instance.Base
open CommandHandlerBase
open Utils

type DiceModule() = 
    inherit CommandHandlerBase()

    [<MessageHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay, msg, AutoRefreshSeed = false)
        let sw = new IO.StringWriter()
        sw.WriteLine("1d100 选项")
        let choices =
            str.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun c ->
                (c, dicer.GetRandomFromString(c, 100u)))
            |> Array.sortBy (fun (_, n) -> n)
        for (c,n) in choices do 
            sw.WriteLine("  {0:000} {1}", n, c)
        arg.QuickMessageReply(sw.ToString())

    [<MessageHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(str : string, arg : ClientEventArgs, msg : Message.MessageEvent) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay, msg)
        let jrrp = dicer.GetRandom(100u)
        arg.QuickMessageReply(sprintf "%s今日人品值是%i" msg.GetNicknameOrCard jrrp)