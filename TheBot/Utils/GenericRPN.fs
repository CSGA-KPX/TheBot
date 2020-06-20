module TheBot.Utils.GenericRPN
open System
open System.Collections.Generic

type IOperand<'T when 'T :> IOperand<'T>> =
    abstract Add : 'T -> 'T
    abstract Div : 'T -> 'T
    abstract Sub : 'T -> 'T
    abstract Mul : 'T -> 'T

type GenericOperator<'Operand>(c, p, f : 'Operand -> 'Operand -> 'Operand) =
    member x.Char = c
    member x.Precedence = p
    /// l -> r -> return
    member x.Func = f
    member x.IsLeftParen = x.Char = '('
    member x.IsRightParen = x.Char = ')'
    member x.IsParen = x.IsLeftParen || x.IsRightParen

    /// 一元运算符还是二元运算符
    ///
    /// 一元运算符时，Func中l=r
    member val IsBinary = true with get, set
    override x.ToString() = x.Char |> string

type RPNToken<'T when 'T :> IOperand<'T>> =
    | Operand of 'T
    | Operator of GenericOperator<'T>

    override x.ToString() =
        match x with
        | Operand i -> sprintf "(Operand %O)" i
        | Operator o -> sprintf "(Operator %O)" o

[<AbstractClass>]
type GenericRPNParser<'Operand when 'Operand :> IOperand<'Operand>>(?OperatorEscapeChar : Char) =
    let opsDict =
        let defaultOps =
                [| GenericOperator<'Operand>('(', -1, fun _ -> invalidOp "")
                   GenericOperator<'Operand>(')', -1, fun _ -> invalidOp "")
                   GenericOperator<'Operand>('+',  2, fun l r -> l.Add(r))
                   GenericOperator<'Operand>('-',  2, fun l r -> l.Sub(r))
                   GenericOperator<'Operand>('*',  3, fun l r -> l.Mul(r))
                   GenericOperator<'Operand>('/',  3, fun l r -> l.Div(r)) |]

        let col = 
            { new System.Collections.ObjectModel.KeyedCollection<char, GenericOperator<'Operand>>() with
                member x.GetKeyForItem(item) = item.Char}

        for op in defaultOps do
            col.Add(op)
        col

    /// 把字符串转换为操作数
    abstract Tokenize : string -> RPNToken<'Operand>

    /// 操作符转义字符
    member val OperatorEscape = defaultArg OperatorEscapeChar '\\'

    /// 获得操作符集合
    /// 
    /// 写入会破坏线程安全
    member x.Operatos = opsDict

    member private x.SplitString(str : string) = 
        [|
            let sb = Text.StringBuilder()
            for i = 0 to str.Length - 1 do 
                let c = str.[i]
                if x.Operatos.Contains(c) then
                    let isEscaped = 
                        // 前一个字符还不能是转义符号
                        if i = 0 then
                            false // 第一个字符不可能被转义
                        else
                            str.[i-1] = x.OperatorEscape
                    if isEscaped then
                        // 删掉转义字符，然后添加
                        sb.Remove(sb.Length - 1, 1).Append(c) |> ignore
                    else
                        let token = sb.ToString()
                        sb.Clear() |> ignore
                        if not <| String.IsNullOrWhiteSpace(token) then yield Choice1Of2 token
                        yield Choice2Of2 x.Operatos.[c]
                else
                    sb.Append(c) |> ignore
            let last = sb.ToString()
            if not <| String.IsNullOrWhiteSpace(last) then yield Choice1Of2 last
        |]

    member private x.InfixToPostfix(tokens : RPNToken<'Operand> []) =
        let stack = Stack<GenericOperator<'Operand>>()
        let output = Queue<RPNToken<'Operand>>()
        for token in tokens do
            match token with
            | Operand _ -> output.Enqueue(token)
            | Operator o when o.IsLeftParen -> stack.Push(o)
            | Operator o when o.IsRightParen ->
                while not (stack.Peek().IsLeftParen) do
                    output.Enqueue(Operator(stack.Pop()))
                if stack.Peek().IsLeftParen then stack.Pop() |> ignore
            | Operator o ->
                while (stack.Count <> 0) && (stack.Peek().Precedence >= o.Precedence)
                      && (not <| stack.Peek().IsLeftParen) do
                    output.Enqueue(Operator(stack.Pop()))
                stack.Push(o)
        while stack.Count <> 0 do
            output.Enqueue(Operator(stack.Pop()))
        output |> Seq.toList

    member x.Eval(str) =
        let rpn =
            str
            |> x.SplitString
            |> Array.map (
                function
                | Choice1Of2 str -> x.Tokenize(str)
                | Choice2Of2 op -> Operator op)
            |> x.InfixToPostfix

        let stack = Stack<'Operand>()
        for token in rpn do
            match token with
            | Operand i -> stack.Push(i)
            | Operator o when not o.IsBinary  ->
                let l = stack.Pop()
                stack.Push(o.Func l l)
            | Operator o ->
                let r, l = stack.Pop(), stack.Pop()
                stack.Push(o.Func l r)
        stack.Pop()

    member x.TryEval(str : string) =
        try
            let ret = x.Eval(str)
            Ok(ret)
        with e -> Error e
