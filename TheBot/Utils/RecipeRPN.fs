module TheBot.Utils.RecipeRPN
open System
open System.Text
open System.Collections.Generic
open TheBot.Utils.GenericRPN

type ItemAccumulator<'Item when 'Item : equality>() =
    inherit Dictionary<'Item, float>()

    member x.AddOrUpdate(item, runs) =
        if x.ContainsKey(item) then x.[item] <- x.[item] + runs
        else x.Add(item, runs)

    member x.MergeWith(a : ItemAccumulator<'Item>, ?isAdd : bool) =
        let add = defaultArg isAdd true
        for kv in a do
            if add then x.AddOrUpdate(kv.Key, kv.Value)
            else x.AddOrUpdate(kv.Key, -(kv.Value))
        x

    static member Singleton(item : 'Item) =
        let a = ItemAccumulator()
        a.Add(item, 1.0)
        a

type RecipeOperand<'Item when 'Item : equality> =
    | Number of float
    | Accumulator of ItemAccumulator<'Item>

    interface IOperand<RecipeOperand<'Item>> with

        override l.Add(r) =
            match l, r with
            | (Number i1), (Number i2) -> Number(i1 + i2)
            | (Accumulator a1), (Accumulator a2) -> Accumulator(a1.MergeWith(a2))
            | (Number i), (Accumulator a) -> raise <| InvalidOperationException("不允许材料和数字相加")
            | (Accumulator a), (Number i) -> (r :> IOperand<RecipeOperand<'Item>>).Add(l)

        override l.Sub(r) =
            match l, r with
            | (Number i1), (Number i2) -> Number(i1 - i2)
            | (Accumulator a1), (Accumulator a2) -> Accumulator(a1.MergeWith(a2, false))
            | (Number i), (Accumulator a) -> raise <| InvalidOperationException("不允许材料和数字相减")
            | (Accumulator a), (Number i) -> (r :> IOperand<RecipeOperand<'Item>>).Sub(l)

        override l.Mul(r) =
            match l, r with
            | (Number i1), (Number i2) -> Number(i1 * i2)
            | (Accumulator a1), (Accumulator a2) -> raise <| InvalidOperationException("不允许材料和材料相乘")
            | (Number i), (Accumulator a) ->
                let keys = a.Keys |> Seq.toArray
                for k in keys do
                    a.[k] <- a.[k] * i
                Accumulator a
            | (Accumulator a), (Number i) -> (r :> IOperand<RecipeOperand<'Item>>).Mul(l)

        override l.Div(r) =
            match l, r with
            | (Number i1), (Number i2) -> Number(i1 / i2)
            | (Accumulator a1), (Accumulator a2) -> raise <| InvalidOperationException("不允许材料和材料相减")
            | (Number i), (Accumulator a) ->
                let keys = a.Keys |> Seq.toArray
                for k in keys do
                    a.[k] <- a.[k] / i
                Accumulator a
            | (Accumulator a), (Number i) -> (r :> IOperand<RecipeOperand<'Item>>).Div(l)

/// 用于计算物品数量的RPN解析器
/// 
/// 
/// 
[<AbstractClass>]
type RecipeExpression<'Item when 'Item : equality>() =
    inherit GenericRPNParser<RecipeOperand<'Item>>()

    /// 把物品名称转换为物品类型
    ///
    /// 如果没有匹配，返回None
    abstract TryGetItemByName : string -> 'Item option

    override x.Tokenize(token) = 
        match token with
        | _ when String.forall Char.IsDigit token ->
            Operand(Number(token |> float))
        | _ ->
            let item = x.TryGetItemByName(token)
            if item.IsNone then failwithf "找不到物品 %s" token
            Operand(Accumulator(ItemAccumulator.Singleton(item.Value)))