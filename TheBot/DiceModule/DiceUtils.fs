module KPX.TheBot.Module.DiceModule.Utils

open System

open KPX.FsCqHttp.Api

open KPX.TheBot.Utils.GenericRPN
open KPX.TheBot.Utils.Dicer


module ChoiceHelper =
    open System.Text.RegularExpressions

    let YesOrNoRegex =
        Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

module DiceExpression =
    type DicerOperand(i : float []) =
        member x.Value = i

        member x.Sum = i |> Array.sum

        static member (+)(l : DicerOperand, r : DicerOperand) =
            (l.Sum + r.Sum) |> Array.singleton |> DicerOperand

        static member (-)(l : DicerOperand, r : DicerOperand) =
            (l.Sum - r.Sum) |> Array.singleton |> DicerOperand

        static member (*)(l : DicerOperand, r : DicerOperand) =
            (l.Sum * r.Sum) |> Array.singleton |> DicerOperand

        static member (/)(l : DicerOperand, r : DicerOperand) =
            (l.Sum / r.Sum) |> Array.singleton |> DicerOperand

        override x.ToString() =
            String.Format("{0:N2}", x.Sum).Replace(".00", "")

    type DiceExpression(dicer : Dicer) as x =
        inherit GenericRPNParser<DicerOperand>(seq {
                                                   GenericOperator<_>('+', 2, (+))
                                                   GenericOperator<_>('-', 2, (-))
                                                   GenericOperator<_>('*', 3, (*))
                                                   GenericOperator<_>('/', 3, (/))
                                               })

        do
            let dFunc (l : DicerOperand) (r : DicerOperand) =
                let lsum = l.Sum |> int
                let rsum = r.Sum |> uint32
                if lsum > 100 then failwithf "投骰次数过多，目前上限为100。"

                let ret =
                    Array.init (lsum) (fun _ -> dicer.GetPostive(rsum) |> float)

                DicerOperand(ret)

            x.Operatos.Add(GenericOperator<_>('D', 5, dFunc))
            x.Operatos.Add(GenericOperator<_>('d', 5, dFunc))

            let kFunc (l : DicerOperand) (r : DicerOperand) =
                let rsum = r.Sum |> int

                l.Value
                |> Array.sortDescending
                |> Array.truncate rsum
                |> DicerOperand

            x.Operatos.Add(GenericOperator<_>('k', 4, kFunc))
            x.Operatos.Add(GenericOperator<_>('K', 4, kFunc))

        /// 创建一个随机骰子
        new() = DiceExpression(Dicer.RandomDicer)

        /// 获取创建DiceExpression所用的Dicer
        member x.Dicer = dicer

        /// 返回一个新的实例，该实例所有D取最小值
        static member ForceMinDiceing =
            let x = DiceExpression()

            let dFunc (l : DicerOperand) (_ : DicerOperand) =
                let lsum = l.Sum |> int
                let ret = Array.init (lsum) (fun _ -> 1.0)
                DicerOperand(ret)

            x.Operatos.Remove('d') |> ignore
            x.Operatos.Remove('D') |> ignore
            x.Operatos.Add(GenericOperator<_>('D', 5, dFunc))
            x.Operatos.Add(GenericOperator<_>('d', 5, dFunc))
            x

        /// 返回一个新的实例，该实例所有D取最大值
        static member ForceMaxDiceing =
            let x = DiceExpression()

            let dFunc (l : DicerOperand) (r : DicerOperand) =
                let lsum = l.Sum |> int
                let rsum = r.Sum
                let ret = Array.init (lsum) (fun _ -> rsum)
                DicerOperand(ret)

            x.Operatos.Remove('d') |> ignore
            x.Operatos.Remove('D') |> ignore
            x.Operatos.Add(GenericOperator<_>('D', 5, dFunc))
            x.Operatos.Add(GenericOperator<_>('d', 5, dFunc))
            x

        override x.Tokenize(token) =
            let succ, number = Double.TryParse(token)

            if succ then
                Operand(DicerOperand(Array.singleton number))
            else
                failwithf "无法将 %s 解析为数字或运算符" token
