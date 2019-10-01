module DiceModule
open System
open KPX.FsCqHttp.Handler.CommandHandlerBase
open Utils

module ChoiceHelper = 
    open System.Text.RegularExpressions
    let YesOrNoRegex = new Regex("(.+)([没不]\1)(.*)", RegexOptions.Compiled)
    let doDice((dicer : Dicer), (opts : string []))=
        opts
        |> Array.map (fun c ->
            (c, dicer.GetRandomFromString(c, 100u)))
        |> Array.sortBy (fun (_, n) -> n)


type DiceModule() = 
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) = 
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        let seed = 
            if atUser.IsSome then
                SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
            else
                SeedOption.SeedByUserDay(msgArg.MessageEvent)
        let dicer = new Dicer(seed, AutoRefreshSeed = false)
        let sw = new IO.StringWriter()
        if atUser.IsSome then
            let atUserId = 
                match atUser.Value with
                | KPX.FsCqHttp.DataType.Message.AtUserType.All ->
                    failwithf ""
                | KPX.FsCqHttp.DataType.Message.AtUserType.User x -> x
            let atUserName = KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, atUserId)
            let ret = msgArg.CqEventArgs.CallApi(atUserName)
            sw.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.GetNicknameOrCard, ret.DisplayName)
        sw.WriteLine("1d100 选项")
        let opts = 
            if msgArg.Arguments.Length = 1 then
                [|
                    let msg = msgArg.Arguments.[0]
                    let   m = ChoiceHelper.YesOrNoRegex.Match(msg)
                    if m.Success then
                        yield m.Groups.[1].Value + m.Groups.[3].Value 
                        yield m.Groups.[2].Value + m.Groups.[3].Value 
                    else
                        yield! msgArg.Arguments
                |]
            else
                msgArg.Arguments
        for (c,n) in ChoiceHelper.doDice(dicer, opts) do 
            sw.WriteLine("  {0:000} {1}", n, c)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) = 
        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.CqEventArgs.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)