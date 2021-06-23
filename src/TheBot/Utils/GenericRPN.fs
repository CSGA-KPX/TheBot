namespace KPX.TheBot.Utils.GenericRPN

open System
open System.Collections.Generic

open KPX.FsCqHttp.Handler


type GenericOperator<'Operand>(c, p) =
    member x.Char = c
    member x.Precedence = p
    member x.IsLeftParen = x.Char = '('
    member x.IsRightParen = x.Char = ')'
    member x.IsParen = x.IsLeftParen || x.IsRightParen

    member val BinaryFunc : ('Operand -> 'Operand -> 'Operand) option = None with get, set

    member val UnaryFunc : ('Operand -> 'Operand) option = None with get, set

    override x.ToString() = x.Char |> string

type RPNToken<'T> =
    | Operand of 'T
    | Operator of GenericOperator<'T> * isUnary : bool

    override x.ToString() =
        match x with
        | Operand i -> $"(Operand {i})"
        | Operator (o, b) -> $"(Operator %A{o} : isUnary : %b{b})"

[<AbstractClass>]
type GenericRPNParser<'Operand>(ops : seq<_>) =
    let opsDict =
        let col =
            { new Collections.ObjectModel.KeyedCollection<char, GenericOperator<'Operand>>() with
                member x.GetKeyForItem(item) = item.Char }

        col.Add(GenericOperator<'Operand>('(', -1))
        col.Add(GenericOperator<'Operand>(')', -1))

        for op in ops do
            col.Add(op)

        col

    new() = GenericRPNParser<'Operand>(Seq.empty)

    /// 把字符串转换为操作数
    abstract Tokenize : string -> RPNToken<'Operand>

    /// 操作符转义字符
    member val OperatorEscape = '\\' with get, set

    /// 获得操作符集合
    ///
    /// 写入会破坏线程安全
    member x.Operators = opsDict

    member private x.SplitString(str : string) =
        let ret = List<_>()
        let sb = Text.StringBuilder()

        for i = 0 to str.Length - 1 do
            let c = str.[i]

            if x.Operators.Contains(c) then
                let isEscaped =
                    // 前一个字符还不能是转义符号
                    if i = 0 then
                        false // 第一个字符不可能被转义
                    else
                        str.[i - 1] = x.OperatorEscape

                if isEscaped then
                    // 删掉转义字符，然后添加
                    sb.Remove(sb.Length - 1, 1).Append(c) |> ignore
                else
                    let token = sb.ToString()
                    sb.Clear() |> ignore

                    if not <| String.IsNullOrWhiteSpace(token) then
                        ret.Add(x.Tokenize(token))
                    
                    let isUnary =
                        ret.Count = 0
                        || match ret.[ret.Count - 1] with
                           | Operand _ -> false
                           | Operator (o, _) -> o.IsLeftParen
                    
                    ret.Add(Operator (x.Operators.[c], isUnary))
            else
                sb.Append(c) |> ignore

        let last = sb.ToString()

        if not <| String.IsNullOrWhiteSpace(last) then
            ret.Add(x.Tokenize(last))

        ret.ToArray()

    member private x.InfixToPostfix(tokens : RPNToken<'Operand> []) =
        let stack = Stack<GenericOperator<'Operand> * bool>()
        let output = Queue<RPNToken<'Operand>>()

        for token in tokens do
            match token with
            | Operand _ -> output.Enqueue(token)
            | Operator (o, iU) when o.IsLeftParen -> stack.Push(o, iU)
            | Operator (o, _) when o.IsRightParen ->
                while not (fst <| stack.Peek()).IsLeftParen do
                    output.Enqueue(Operator(stack.Pop()))

                if (fst <| stack.Peek()).IsLeftParen then stack.Pop() |> ignore
            | Operator (o, iU) ->
                while (stack.Count <> 0)
                      && ((fst <| stack.Peek()).Precedence >= o.Precedence)
                      && (not <| (fst <| stack.Peek()).IsLeftParen) do
                    output.Enqueue(Operator(stack.Pop()))

                stack.Push(o, iU)

        while stack.Count <> 0 do
            output.Enqueue(Operator(stack.Pop()))

        output |> Seq.toList

    member x.Eval(str) =
        let rpn = str |> x.SplitString |> x.InfixToPostfix
        
        printfn $"%A{rpn}"
        
        if rpn.Length = 0 then
            raise <| ModuleException(InputError, "输入错误：表达式为空")

        try
            let stack = Stack<'Operand>()

            for token in rpn do
                match token with
                | Operand i -> stack.Push(i)
                | Operator (o, isUnary) ->
                    if isUnary && o.UnaryFunc.IsSome then
                        let l = stack.Pop()
                        stack.Push(o.UnaryFunc.Value l)
                    else
                        let r, l = stack.Pop(), stack.Pop()
                        stack.Push(o.BinaryFunc.Value l r)

            stack.Pop()
        with :? InvalidOperationException ->
            let operators =
                x.Operators |> Seq.map (fun op -> op.Char)

            raise
            <| ModuleException(
                InputError,
                "计算错误：表达式未配平。如果表达式中出现{0}等运算符请使用{1}进行转义。",
                String.Join("", operators),
                x.OperatorEscape
            )

    member x.TryEval(str : string) =
        try
            let ret = x.Eval(str)
            Ok(ret)
        with e -> Error e
