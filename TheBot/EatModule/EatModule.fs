module TheBot.Module.EatModule.Instance

open System
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Module.EatModule.Utils

open TheBot.Utils.Dicer

type EatModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    member x.HandleEat(msgArg : CommandArgs) =
        let at = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        if at.IsSome then
            invalidOp "你管别人怎么吃啊？"

        let str = 
            let v = String.Join(" ", msgArg.Arguments)
            if eatAlias.ContainsKey(v) then eatAlias.[v]
            else v
        if str = "" then
            invalidOp "预设套餐：早中晚加 火锅 萨莉亚"

        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent), AutoRefreshSeed = false)

        if eatFuncs.ContainsKey(str) then
            msgArg.CqEventArgs.QuickMessageReply(eatFuncs.[str](dicer))
        else
            let ret = 
                match dicer.GetRandomFromString("吃" + str, 100u) with
                | x when x  =100 -> "黄连素备好"
                | x when x >= 96 -> "上秤看看"
                | x when x >= 76 -> "算了吧"
                | x when x >= 51 -> "不推荐"
                | x when x >= 26 -> "也不是不行"
                | x when x >=  6 -> "还好"
                | x when x >=  1 -> "好主意"
                | d -> 
                    x.Logger.Fatal(sprintf "ret is %i" d)
                    failwith "你说啥来着？"
            msgArg.CqEventArgs.QuickMessageReply(ret)