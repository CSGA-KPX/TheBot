namespace KPX.EvePlugin.Data.Process

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process


type IEveCalculatorConfig =
    abstract InputMe: int
    abstract DerivationMe: int
    abstract ExpandPlanet: bool
    abstract ExpandReaction: bool
    abstract RunRounding : ProcessRunRounding

type EveRecipeConfig(cfg: IEveCalculatorConfig) =
    inherit RecipeConfig<EveType, EveProcess>(RunRounding = cfg.RunRounding)

    override x.CanExpandRecipe(proc) =
        (proc.Type = ProcessType.Manufacturing)
        || (proc.Type = ProcessType.Planet && cfg.ExpandPlanet)
        || (proc.Type = ProcessType.Reaction && cfg.ExpandReaction)

    override x.SolveSameProduct(values) =
        // 毕竟不太可能吧！
        Seq.exactlyOne values

type EveProcessManager(cfg: IEveCalculatorConfig) =
    inherit RecipeManager<EveType, EveProcess>
        (
            [ BlueprintCollection.Instance; PlanetProcessCollection.Instance ],
            EveRecipeConfig(cfg)
        )

    member x.GetRecipe(mr : RecipeMaterial<EveType>, ?ime : int) =
        let ime = defaultArg ime cfg.InputMe

        x.GetRecipe(mr.Item).Set(ByItems mr.Quantity, ime)

    override x.ApplyProcessQuantity(proc, quantity, depth) =
        if depth = 0 then
            proc
                .Set(quantity, cfg.InputMe)
                .ApplyFlags(MeApplied x.Config.RunRounding)
        else
            proc
                .Set(quantity, cfg.DerivationMe)
                .ApplyFlags(MeApplied x.Config.RunRounding)
