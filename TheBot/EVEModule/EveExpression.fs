module TheBot.Module.EveModule.EveExpression

open TheBot.Utils.RecipeRPN
open BotData.EveData.EveType
open TheBot.Module.EveModule.Utils.Data

type ItemAccumulator = ItemAccumulator<EveType>

type EveExpression() =
    inherit RecipeExpression<EveType>()

    override x.TryGetItemByName(str) = DataBundle.Instance.TryGetItem(str)
