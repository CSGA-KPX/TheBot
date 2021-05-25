module KPX.TheBot.Module.XivModule.Utils.XivExpression

open System

open KPX.TheBot.Data.XivData

open KPX.TheBot.Utils.GenericRPN
open KPX.TheBot.Utils.RecipeRPN


type ItemAccumulator = ItemAccumulator<XivItem>

type XivExpression() as x =
    inherit RecipeExpression<XivItem>()

    do
        let itemOperator =
            GenericOperator<_>(
                '#',
                Int32.MaxValue,
                fun l _ ->
                    match l with
                    | Number f ->
                        let item =
                            ItemCollection.Instance.GetByItemId(int f)

                        let acu = ItemAccumulator.SingleItemOf item
                        Accumulator acu
                    | Accumulator _ -> failwithf "#符号仅对数字使用"
            )

        itemOperator.IsBinary <- false
        x.Operators.Add(itemOperator)

    override x.TryGetItemByName(str) =
        ItemCollection.Instance.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))
