module TheBot.GenericRPN

open System.Collections.Generic

type IOperand<'T when 'T :> IOperand<'T>> =
    abstract Add : 'T -> 'T
    abstract Div : 'T -> 'T
    abstract Sub : 'T -> 'T
    abstract Mul : 'T -> 'T

type GenericOperator(c, p) =
    member x.Char = c
    member x.Precedence = p

    member x.IsLeftParen = x.Char = '('
    member x.IsRightParen = x.Char = ')'
    member x.IsParen = x.IsLeftParen || x.IsRightParen
    // unary or binary?
    member val IsBinary = true with get, set
    override x.ToString() = x.Char |> string

type RPNToken<'T when 'T :> IOperand<'T>> =
    | Operand of 'T
    | Operator of GenericOperator

    override x.ToString() =
        match x with
        | Operand i -> sprintf "(Operand %O)" i
        | Operator o -> sprintf "(Operator %O)" o

type EvalDelegate<'T when 'T :> IOperand<'T>> = delegate of (char * 'T * 'T) -> 'T

[<AbstractClass>]
type GenericRPNParser<'Operand when 'Operand :> IOperand<'Operand>>() =

    static let defaultOps =
        [| GenericOperator('(', -1)
           GenericOperator(')', -1)
           GenericOperator('+', 2)
           GenericOperator('-', 2)
           GenericOperator('*', 3)
           GenericOperator('/', 3) |]


    let opsDict =
        let dict = Dictionary<string, GenericOperator>()
        for op in defaultOps do
            dict.Add(op.Char.ToString(), op)
        dict

    abstract Tokenize : string -> RPNToken<'Operand> []

    member x.Operatos = opsDict

    member x.AddOperator(o : GenericOperator) = opsDict.Add(o.Char.ToString(), o)

    member private x.InfixToPostfix(tokens : RPNToken<'Operand> []) =
        let stack = Stack<GenericOperator>()
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

    member x.EvalWith(str, func : EvalDelegate<'Operand>) =
        let tokens = x.Tokenize(str)
        let rpn = x.InfixToPostfix(tokens)
        let stack = Stack<'Operand>()
        for token in rpn do
            match token with
            | Operand i -> stack.Push(i)
            | Operator o when not o.IsBinary  ->
                let l = stack.Pop()
                let ret = func.Invoke(o.Char, l, l)
                stack.Push(ret)
            | Operator o ->
                let r, l = stack.Pop(), stack.Pop()
                let ret = func.Invoke(o.Char, l, r)
                stack.Push(ret)
        stack.Pop()
