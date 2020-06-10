module TheBot.Module.EveModule.EveExpression
open TheBot.Utils.RecipeRPN
open EveData
open TheBot.Module.EveModule.Utils

type ItemAccumulator = ItemAccumulator<EveType>

type EveExpression() = 
    inherit RecipeExpression<EveType>()

    override x.TryGetItemByName(str) = 
        let succ, item = EveTypeNameCache.TryGetValue(str)
        if succ then Some(item) else None