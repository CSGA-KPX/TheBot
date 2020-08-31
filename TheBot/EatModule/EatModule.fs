module TheBot.Module.EatModule.Instance

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Module.EatModule.Utils

open TheBot.Utils.Dicer

type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    member x.HandleEat(msgArg : CommandArgs) =
        let at = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead

        let str = 
            let v = String.Join(" ", msgArg.Arguments)
            if eatAlias.ContainsKey(v) then eatAlias.[v]
            else v
        if str = "" then
            invalidOp "预设套餐：早中晚加 火锅 萨莉亚"

        use ret = msgArg.OpenResponse()
        match at with
        | Some Message.AtUserType.All -> ret.FailWith("DD不可取")
        | Some (Message.AtUserType.User uid) when uid = msgArg.SelfId ->
            ret.FailWith("吃了你哦")
        | Some (Message.AtUserType.User uid) ->
            let atUserName = GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, uid)
            msgArg.ApiCaller.CallApi(atUserName)
            ret.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.GetNicknameOrCard, atUserName.DisplayName)
        | None -> ()

        let seed  = if at.IsSome then 
                        SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                    else
                        SeedOption.SeedByUserDay(msgArg.MessageEvent)
        let dicer = new Dicer(seed, AutoRefreshSeed = false)

        if eatFuncs.ContainsKey(str) then
            ret.Write(eatFuncs.[str](dicer))
        else
            ret.Write(whenToEat(dicer, str))
