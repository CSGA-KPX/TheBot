module TheBot.Module.EveModule.EveExpression
open TheBot.Utils.RecipeRPN
open EveData
open TheBot.Module.EveModule.Utils.Data
open TheBot.Module.EveModule.Utils.Extensions

type ItemAccumulator = ItemAccumulator<EveType>

type EveExpression() = 
    inherit RecipeExpression<EveType>()

    override x.TryGetItemByName(str) = 
        DataBundle.Instance.TryGetItem(str)

