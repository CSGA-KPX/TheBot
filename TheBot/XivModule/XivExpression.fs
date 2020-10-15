module TheBot.Module.XivModule.Utils.XivExpression

open System

open BotData.XivData

open TheBot.Utils.GenericRPN
open TheBot.Utils.RecipeRPN

type ItemAccumulator = ItemAccumulator<Item.ItemRecord>

type XivExpression() as x = 
    inherit RecipeExpression<Item.ItemRecord>()
    
    do
        let itemOperator = GenericOperator<_>('#', Int32.MaxValue, fun l _ ->
            match l with
            | Number f ->
                let item = Item.ItemCollection.Instance.GetByItemId(int f)
                let acu = ItemAccumulator.Singleton item
                Accumulator acu
            | Accumulator _ -> failwithf "#符号仅对数字使用")

        itemOperator.IsBinary <- false
        x.Operatos.Add(itemOperator)

    override x.TryGetItemByName(str) = 
        Item.ItemCollection.Instance.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))