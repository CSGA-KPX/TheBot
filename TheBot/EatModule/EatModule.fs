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

        let str = msgArg.RawMessage
        let (key, func) = eatFuncTable |> Array.find (fun (x,_) -> str.Contains(x))

        let dicer = new Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        msgArg.CqEventArgs.QuickMessageReply(func(dicer))