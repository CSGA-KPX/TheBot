module KPX.TheBot.Host.Utils.RecipeRPN

open System

open KPX.TheBot.Host.Utils.GenericRPN

open KPX.TheBot.Host.DataModel.Recipe


type RecipeOperand<'Item when 'Item: equality> =
    | Number of float
    | Accumulator of ItemAccumulator<'Item>

    static member (+)(l, r) =
        match l, r with
        | Number i1, Number i2 -> Number(i1 + i2)
        | Accumulator a1, Accumulator a2 ->
            for mr in a2 do
                a1.Update(mr)

            Accumulator a1
        | Number _, Accumulator _ -> raise <| InvalidOperationException("不允许材料和数字相加")
        | Accumulator _, Number _ -> r + l

    static member (-)(l, r) =
        match l, r with
        | Number i1, Number i2 -> Number(i1 - i2)
        | Accumulator a1, Accumulator a2 ->
            for mr in a2 do
                a1.Update(mr.Item, -mr.Quantity)

            Accumulator(a1)
        | Number _, Accumulator _ -> raise <| InvalidOperationException("不允许材料和数字相减")
        | Accumulator _, Number _ -> r - l

    static member (*)(l, r) =
        match l, r with
        | Number i1, Number i2 -> Number(i1 * i2)
        | Accumulator _, Accumulator _ -> raise <| InvalidOperationException("不允许材料和材料相乘")
        | Number i, Accumulator a ->
            for mr in a do
                a.[mr.Item] <- a.[mr.Item] * i

            Accumulator a
        | Accumulator _, Number _ -> r * l

    static member (/)(l, r) =
        match l, r with
        | Number i1, Number i2 -> Number(i1 / i2)
        | Accumulator _, Accumulator _ -> raise <| InvalidOperationException("不允许材料和材料相减")
        | Number i, Accumulator a ->
            for mr in a do
                a.[mr.Item] <- a.[mr.Item] * (1.0 / i)

            Accumulator a
        | Accumulator _, Number _ -> r / l

/// 用于计算物品数量的RPN解析器
[<AbstractClass>]
type RecipeExpression<'Item when 'Item: equality>() =
    inherit GenericRPNParser<RecipeOperand<'Item>>(seq {
        GenericOperator<_>('+', 2, BinaryFunc = Some(+))
        GenericOperator<_>('-', 2, BinaryFunc = Some(-))
        GenericOperator<_>('*', 3, BinaryFunc = Some(*))
        GenericOperator<_>('/', 3, BinaryFunc = Some(/))
    })

    /// 把物品名称转换为物品类型
    ///
    /// 如果没有匹配，返回None
    abstract TryGetItemByName: string -> 'Item option

    override x.Tokenize(token) =
        match token with
        | _ when String.forall Char.IsDigit token -> Operand(Number(token |> float))
        | _ ->
            let item = x.TryGetItemByName(token)

            if item.IsNone then
                failwithf $"找不到物品 %s{token}"

            Operand(Accumulator(ItemAccumulator(item.Value)))
