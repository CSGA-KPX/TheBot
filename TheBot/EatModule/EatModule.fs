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

        let head = msgArg.Arguments |> Array.tryHead
        if head.IsNone then
            invalidOp "预设套餐：早中晚加 火锅 萨莉亚"

        let str = head.Value
        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent), AutoRefreshSeed = false)

        let ret = eatFuncTable |> Array.tryFind (fun (x,_,_) -> str.Contains(x))
        if ret.IsNone then
            let ret = 
                match dicer.GetRandomFromString("吃" + str, 100u) with
                | x when x  =100 -> "上秤看看吧。。。"
                | x when x >= 96 -> "黄连素备好"
                | x when x >= 51 -> "不推荐哦"
                | x when x >= 10 -> "还行"
                | x when x >=  0 -> "就这个"
                | d -> 
                    x.Logger.Fatal(sprintf "ret is %i" d)
                    failwith "你说啥来着？"
            msgArg.CqEventArgs.QuickMessageReply(ret)
        else
            let (_, _, func) = ret.Value
            msgArg.CqEventArgs.QuickMessageReply(func(dicer))