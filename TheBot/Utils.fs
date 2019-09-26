module Utils
open System
open System.Text.RegularExpressions
open System.Collections.Generic
open KPX.FsCqHttp.DataType

let private CSTOffset = TimeSpan.FromHours(8.0)

type SeedOption = 
    | SeedDate
    | SeedRandom
    | SeedCustom of string

    member x.GetSeedString() = 
        match x with
        | SeedDate     -> DateTimeOffset.UtcNow.ToOffset(CSTOffset).ToString("yyyyMMdd")
        | SeedRandom   -> Guid.NewGuid().ToString()
        | SeedCustom s -> s

    static member GetSeedString(a : SeedOption []) =
        a
        |> Array.fold (fun str x -> str + (x.GetSeedString())) ""

    static member SeedByUserDay(msg : Event.Message.MessageEvent)= 
        [|
            SeedDate
            SeedCustom (msg.UserId.ToString())
        |]

    static member SeedByAtUserDay(msg : Event.Message.MessageEvent)= 
        [|
            SeedDate
            SeedCustom
                (
                    let at = msg.Message.GetAts()
                    if at.Length = 0 then
                        raise <| InvalidOperationException("没有用户被At！")
                    else
                        at.[0].ToString()
                )
        |]

type Dicer (initSeed : byte []) as x =
    static let utf8 = Text.Encoding.UTF8
    static let md5  = System.Security.Cryptography.MD5.Create()

    let mutable hash = initSeed

    let refreshSeed () = 
        if x.AutoRefreshSeed then
            hash <- md5.ComputeHash(hash)

    let hashToDice (hash, faceNum) = 
        let num = BitConverter.ToUInt32(hash, 0) % faceNum |> int32
        num + 1

    new (seed : SeedOption []) =
        let initSeed = 
            seed
            |> SeedOption.GetSeedString
            |> utf8.GetBytes
            |> md5.ComputeHash
        new Dicer(initSeed)

    /// Init using SeedOption.SeedRandom
    new()  =  new Dicer(Array.singleton SeedOption.SeedRandom)

    member private x.GetHash() = refreshSeed(); hash

    member val AutoRefreshSeed = true with get, set

    /// 返回Base64编码后的初始种子
    member x.InitialSeed =
        Convert.ToBase64String(initSeed)

    member x.GetRandomFromString(str : string, faceNum) = 
        refreshSeed()
        let seed = Array.append (x.GetHash()) (utf8.GetBytes(str))
        let hash = md5.ComputeHash(seed)
        hashToDice(hash, faceNum)

    /// 获得一个随机数
    member x.GetRandom(faceNum) = 
        refreshSeed()
        hashToDice(x.GetHash(), faceNum)

    /// 获得一组随机数，不重复
    member x.GetRandomArray(faceNum, count) =
        let tmp = new Collections.Generic.HashSet<int>()
        while tmp.Count <> count do
            tmp.Add(x.GetRandom(faceNum)) |> ignore
        let ret= Array.zeroCreate<int> tmp.Count
        tmp.CopyTo(ret)
        ret
    
    /// 从数组获得随机项
    member x.GetRandomItem(srcArr : 'T []) = 
        let idx = x.GetRandom(srcArr.Length |> uint32) - 1
        srcArr.[idx]

    /// 从数组获得一组随机项，不重复
    member x.GetRandomItems(srcArr : 'T [], count) = 
        [|
            let faceNum = srcArr.Length |> uint32
            for i in x.GetRandomArray(faceNum, count) do 
                yield srcArr.[i-1]
        |]

type OperatorType(c, p) = 
    static let operators =
            let NullOperator l r = 
                raise <| NotImplementedException()
            [|
                OperatorType ('(', -1)
                OperatorType (')', -1)
                OperatorType ('+' , 2)
                OperatorType ('-' , 2)
                OperatorType ('*' , 3)
                OperatorType ('/' , 3)
                OperatorType ('D' , 5)
                OperatorType ('d' , 5)
            |]
            |> Array.map (fun x -> x.Char.ToString(), x)
            |> readOnlyDict

    static member Operators = 
        operators

    member val Char = c
    member val Precedence = p

    member x.IsLeftParen = x.Char = '('
    member x.IsRightParen = x.Char = ')'
    member x.IsParen = x.IsLeftParen || x.IsRightParen

    override x.ToString() = 
        sprintf "%c" x.Char

type DiceToken = 
    | Operand of int
    | Operator of OperatorType


module DicerExpressionUtils = 
    let tokenRegex = new Regex("([^0-9])", RegexOptions.Compiled)
    let Operators =
            let NullOperator l r = 
                raise <| NotImplementedException()
            [|
                OperatorType ('(', -1)
                OperatorType (')', -1)
                OperatorType ('+' , 2)
                OperatorType ('-' , 2)
                OperatorType ('*' , 3)
                OperatorType ('/' , 3)
                OperatorType ('D' , 5)
                OperatorType ('d' , 5)
            |]
            |> Array.map (fun x -> x.Char.ToString(), x)
            |> readOnlyDict

    let Tokenize (str) = 
        [|
            let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
            for str in strs do 
                match str with
                | _ when Char.IsDigit(str.[0]) -> yield Operand (str |> int32)
                | _ when Operators.ContainsKey(str) -> yield Operator (Operators.[str])
                | _ -> failwithf "Unknown token %s" str
        |]

    let InfixToPostfix (x : DiceToken[]) = 
        let stack = new Stack<OperatorType>()
        let output= new Queue<DiceToken>()
        for token in x do 
            match token with
            | Operand _ ->
                output.Enqueue(token)
            | Operator o when o.IsLeftParen ->
                stack.Push(o)
            | Operator o when o.IsRightParen ->
                while not (stack.Peek().IsLeftParen) do 
                    output.Enqueue(Operator (stack.Pop()))
                if stack.Peek().IsLeftParen then
                    stack.Pop() |> ignore
            | Operator o ->
                while
                    (stack.Count <> 0)
                    && (stack.Peek().Precedence > o.Precedence)
                    && (not <| stack.Peek().IsLeftParen)
                    do 
                        output.Enqueue(Operator (stack.Pop()))
                stack.Push(o)
        while stack.Count <> 0 do
            output.Enqueue(Operator (stack.Pop()))
        output |> Seq.toList

    let GetDiceOperator (dicer : Dicer) = 
        (fun l r -> 
            Array.init<int> l (fun _ -> dicer.GetRandom(r |> uint32) |> int32)
            |> Array.sum
        )

type DiceExpression(str) = 
    let logger = NLog.LogManager.GetCurrentClassLogger()

    member x.EvalWith(dicer : Dicer) = 
        let tokens = DicerExpressionUtils.Tokenize(str)
        let rpn    = DicerExpressionUtils.InfixToPostfix(tokens)
        let stack = new Stack<int>()
        for token in rpn do
            match token with
            | Operand i ->
                stack.Push(i)
            | Operator o -> 
                let r,l = stack.Pop(), stack.Pop()
                let func = 
                    match o.Char with
                    | '+' -> (+)
                    | '-' -> (-)
                    | '*' -> (*)
                    | '/' -> (/)
                    | 'd' | 'D' -> DicerExpressionUtils.GetDiceOperator(dicer)
                    | unk -> failwithf "%c" unk
                stack.Push(func l r)
        stack.Pop()

    member x.TryEvalWith(dicer : Dicer) = 
        try
            let ret = x.EvalWith(dicer)
            Ok (ret)
        with
        |e -> 
            logger.Fatal(e.ToString())
            Error e