module TheBot.Module.DiceModule.Utils

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer
open TheBot.Utils.TextTable

module ChoiceHelper =
    open System.Text.RegularExpressions
    open System.Collections.Generic

    let YesOrNoRegex = Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

    type MessageParser() = 
        
        static let dateTable = 
            [|
                "大后天", TimeSpan.FromDays(3.0)
                "大前天", TimeSpan.FromDays(-2.0)
                "明天", TimeSpan.FromDays(1.0)
                "后天", TimeSpan.FromDays(2.0)
                "昨天", TimeSpan.FromDays(-1.0)
                "前天", TimeSpan.FromDays(-1.0)
                "", TimeSpan.Zero
            |]

        member val AllowAtUser = true with get, set

        member x.Parse(arg : CommandArgs) =
            let atUser = 
                arg.MessageEvent.Message.GetAts()
                |> Array.tryHead
                |> Option.filter (fun _ -> x.AllowAtUser)

            [
                for arg in arg.Arguments do 
                    let seed = List<SeedOption>()
                    let choices = List<string>()

                    if atUser.IsSome then 
                        let at = atUser.Value
                        if at = Message.AtUserType.All then invalidOp "at全体无效"
                        seed.Add(SeedOption.SeedCustom(atUser.Value.ToString()))

                    //拆分条目
                    let m = YesOrNoRegex.Match(arg)
                    if m.Success then
                        choices.Add(m.Groups.[1].Value + m.Groups.[2].Value + m.Groups.[4].Value)
                        choices.Add(m.Groups.[1].Value + m.Groups.[3].Value + m.Groups.[4].Value)
                    else
                        choices.Add(arg)


                    //处理选项
                    for c in choices do 
                        let seed = 
                            [|
                                yield! seed
                                let (_, ts) = dateTable |> Array.find (fun x -> c.Contains(fst x))
                                yield SeedOption.SeedCustom((GetCstTime() + ts).ToString("yyyyMMdd"))
                            |]
                        yield seed, c
            ]

module DiceExpression =
    open System.Globalization

    type DicerOperand(i : float) =
        member x.Value = i

        interface IOperand<DicerOperand> with
            override l.Add(r) = DicerOperand(l.Value + r.Value)
            override l.Sub(r) = DicerOperand(l.Value - r.Value)
            override l.Div(r) = DicerOperand(l.Value / r.Value)
            override l.Mul(r) = DicerOperand(l.Value * r.Value)

        override x.ToString() = 
            let fmt =
                if (i % 1.0) <> 0.0 then "{0:N2}"
                else "{0:N0}"
            System.String.Format(fmt, i)

    type DiceExpression(dicer : Dicer) as x =
        inherit GenericRPNParser<DicerOperand>()

        do
            let func (l : DicerOperand) (r : DicerOperand) =
                let ret = Array.init<int> (l.Value |> int) (fun _ -> dicer.GetRandom(r.Value |> uint32)) |> Array.sum
                DicerOperand(ret |> float)

            x.Operatos.Add(GenericOperator<_>('D', Int32.MaxValue, func))
            x.Operatos.Add(GenericOperator<_>('d', Int32.MaxValue, func))

        new () = DiceExpression(Dicer(SeedOption.SeedRandom))

        override x.Tokenize(token) =
            let succ, number = Double.TryParse(token)
            if succ then
                Operand(DicerOperand(number))
            else
                failwithf "无法解析 %s" token
