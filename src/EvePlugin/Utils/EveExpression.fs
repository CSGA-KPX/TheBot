module KPX.EvePlugin.Utils.EveExpression

open KPX.TheBot.Host.Utils.RecipeRPN

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Utils.Data


type ItemAccumulator = ItemAccumulator<EveType>

type EveExpression() =
    inherit RecipeExpression<EveType>()

    override x.TryGetItemByName(str) = DataBundle.Instance.TryGetItem(str)
