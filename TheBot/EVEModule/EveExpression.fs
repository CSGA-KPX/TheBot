module KPX.TheBot.Module.EveModule.EveExpression

open KPX.TheBot.Utils.RecipeRPN
open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Module.EveModule.Utils.Data

type ItemAccumulator = ItemAccumulator<EveType>

type EveExpression() =
    inherit RecipeExpression<EveType>()

    override x.TryGetItemByName(str) = DataBundle.Instance.TryGetItem(str)
