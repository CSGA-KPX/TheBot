module KPX.TheBot.Module.DiceModule.Utils

open System

open KPX.FsCqHttp.Api

open KPX.TheBot.Utils.GenericRPN
open KPX.TheBot.Utils.Dicer


module ChoiceHelper =
    open System.Text.RegularExpressions

    let private yesOrNoRegex =
        Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)
        
    let expandYesOrNo str =
       [|
           let m = yesOrNoRegex.Match(str)

           if m.Success then
               yield
                   m.Groups.[1].Value
                   + m.Groups.[2].Value
                   + m.Groups.[4].Value

               yield
                   m.Groups.[1].Value
                   + m.Groups.[3].Value
                   + m.Groups.[4].Value
            else
                yield str
       |]

        

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
                                                   GenericOperator<_>('+', 2, BinaryFunc = Some (+))
                                                   GenericOperator<_>('-', 2, BinaryFunc = Some (-))
                                                   GenericOperator<_>('*', 3, BinaryFunc = Some (*))
                                                   GenericOperator<_>('/', 3, BinaryFunc = Some (/))
                                               })

        do
            let binaryDiceFunc (l : DicerOperand) (r : DicerOperand) =
                let lSum = l.Sum |> int
                let rSum = r.Sum |> uint32
                if lSum > 100 then failwithf "投骰次数过多，目前上限为100。"

                let ret =
                    Array.init lSum (fun _ -> dicer.GetPositive(rSum) |> float)

                DicerOperand(ret)
            
            let unaryDiceFunc (r : DicerOperand) =
                let lSum = 1
                let rSum = r.Sum |> uint32
                if lSum > 100 then failwithf "投骰次数过多，目前上限为100。"

                let ret =
                    Array.init lSum (fun _ -> dicer.GetPositive(rSum) |> float)

                DicerOperand(ret)
            
            x.Operators.Add(GenericOperator<_>('D', 5, BinaryFunc = Some binaryDiceFunc, UnaryFunc = Some unaryDiceFunc))
            x.Operators.Add(GenericOperator<_>('d', 5, BinaryFunc = Some binaryDiceFunc, UnaryFunc = Some unaryDiceFunc))

            let kFunc (l : DicerOperand) (r : DicerOperand) =
                let rSum = r.Sum |> int

                l.Value
                |> Array.sortDescending
                |> Array.truncate rSum
                |> DicerOperand

            x.Operators.Add(GenericOperator<_>('k', 4, BinaryFunc = Some kFunc))
            x.Operators.Add(GenericOperator<_>('K', 4, BinaryFunc = Some kFunc))

        /// 创建一个随机骰子
        new() = DiceExpression(Dicer.RandomDicer)

        /// 获取创建DiceExpression所用的Dicer
        member x.Dicer = dicer

        /// 返回一个新的实例，该实例所有D取最小值
        static member ForceMinDiceing =
            let x = DiceExpression()

            let dFunc (l : DicerOperand) (_ : DicerOperand) =
                let lsum = l.Sum |> int
                let ret = Array.init lsum (fun _ -> 1.0)
                DicerOperand(ret)

            let dFuncUnary (_ : DicerOperand) =
                let ret = Array.init 1 (fun _ -> 1.0)
                DicerOperand(ret)
            
            x.Operators.Remove('d') |> ignore
            x.Operators.Remove('D') |> ignore
            x.Operators.Add(GenericOperator<_>('D', 5, BinaryFunc = Some dFunc, UnaryFunc = Some dFuncUnary))
            x.Operators.Add(GenericOperator<_>('d', 5, BinaryFunc = Some dFunc, UnaryFunc = Some dFuncUnary))
            x

        /// 返回一个新的实例，该实例所有D取最大值
        static member ForceMaxDiceing =
            let x = DiceExpression()

            let dFunc (l : DicerOperand) (r : DicerOperand) =
                let lsum = l.Sum |> int
                let rsum = r.Sum
                let ret = Array.init lsum (fun _ -> rsum)
                DicerOperand(ret)
                
            let dFuncUnary (r : DicerOperand) =
                let rsum = r.Sum
                let ret = Array.init 1 (fun _ -> rsum)
                DicerOperand(ret)

            x.Operators.Remove('d') |> ignore
            x.Operators.Remove('D') |> ignore
            x.Operators.Add(GenericOperator<_>('D', 5, BinaryFunc = Some dFunc, UnaryFunc = Some dFuncUnary))
            x.Operators.Add(GenericOperator<_>('d', 5, BinaryFunc = Some dFunc, UnaryFunc = Some dFuncUnary))
            x

        override x.Tokenize(token) =
            let succ, number = Double.TryParse(token)

            if succ then
                Operand(DicerOperand(Array.singleton number))
            else
                failwithf $"无法将 %s{token} 解析为数字或运算符"
