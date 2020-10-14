module TheBot.Module.DiceModule.Utils

open System
open KPX.FsCqHttp.Api
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Utils.TextTable

open TheBot.Utils.GenericRPN
open TheBot.Utils.Dicer

module ChoiceHelper =
    open System.Text.RegularExpressions
    open System.Collections.Generic

    let YesOrNoRegex = Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

module DiceExpression =
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
