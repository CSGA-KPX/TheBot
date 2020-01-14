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
            msgArg.QuickMessageReply(eatFuncs.[str](dicer))
        else
            let types = [|"早餐"; "午餐"; "晚餐"; "加餐"|]
            let sb = Text.StringBuilder()
            for t in types do 
                let str = sprintf "%s吃%s" t str
                let d = dicer.GetRandomFromString(str, 100u)
                let ret = 
                    match d with
                    | _ when d  =100 -> "黄连素备好"
                    | _ when d >= 96 -> "上秤看看"
                    | _ when d >= 76 -> "算了吧"
                    | _ when d >= 51 -> "不推荐"
                    | _ when d >= 26 -> "也不是不行"
                    | _ when d >=  6 -> "还好"
                    | _ when d >=  1 -> "好主意"
                    | _ -> 
                        x.Logger.Fatal(sprintf "ret is %i" d)
                        failwith "你说啥来着？"
                sb.AppendLine(sprintf "%s : %s(%i)" str ret d) |> ignore
            msgArg.QuickMessageReply(sb.ToString())
