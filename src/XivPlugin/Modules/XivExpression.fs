module KPX.XivPlugin.Modules.Utils.XivExpression

open System

open KPX.XivPlugin.Data

open KPX.TheBot.Host.Utils.GenericRPN
open KPX.TheBot.Host.Utils.RecipeRPN


type ItemAccumulator = ItemAccumulator<XivItem>

type XivExpression() as x =
    inherit RecipeExpression<XivItem>()

    do
        let unaryFunc (l: RecipeOperand<XivItem>) =
            match l with
            | Number f ->
                let item = ItemCollection.Instance.GetByItemId(int f)

                let acu = ItemAccumulator.SingleItemOf item
                Accumulator acu
            | Accumulator _ -> failwithf "#符号仅对数字使用"

        let itemOperator = GenericOperator<_>('#', Int32.MaxValue, UnaryFunc = Some unaryFunc)

        x.Operators.Add(itemOperator)

    override x.TryGetItemByName(str) =
        failwith ""
        // 不再使用
        ItemCollection.Instance.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    override x.Tokenize(token) =
        match token with
        | _ when String.forall Char.IsDigit token -> Operand(Number(token |> float))
        | _ ->
            if token.Contains("@") then
                let tmp = token.Split('@')

                if tmp.Length <> 2 then
                    failwithf "装等表达式错误"

                let job = ClassJobMapping.TryParse(tmp.[0])
                let iLevel = int tmp.[1]
                let cgc = CraftableGearCollection.Instance

                if job.IsNone then
                    failwith "未知职业，如确定无误请联系开发者添加简写"

                let items =
                    cgc.Search(iLevel, job.Value)
                    |> Array.map (fun g -> ItemCollection.Instance.GetByItemId(g.ItemId))

                if items.Length = 0 then
                    failwithf "不存在指定的装备"

                let acu = ItemAccumulator()

                for item in items do
                    acu.Update(item)

                Operand(Accumulator(acu))
            else
                let item = ItemCollection.Instance.TryGetByName(token.TrimEnd(CommandUtils.XivSpecialChars))

                if item.IsNone then
                    failwithf $"找不到物品 %s{token}"

                Operand(Accumulator(ItemAccumulator.SingleItemOf(item.Value)))
