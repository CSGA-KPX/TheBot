module DiceModule
open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open Utils

type DiceModule() = 
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay, msgArg.MessageEvent, AutoRefreshSeed = false)
        let sw = new IO.StringWriter()
        sw.WriteLine("1d100 选项")
        let choices =
            msgArg.Arguments
            |> Array.map (fun c ->
                (c, dicer.GetRandomFromString(c, 100u)))
            |> Array.sortBy (fun (_, n) -> n)
        for (c,n) in choices do 
            sw.WriteLine("  {0:000} {1}", n, c)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay, msgArg.MessageEvent)
        let jrrp = dicer.GetRandom(100u)
        msgArg.CqEventArgs.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)