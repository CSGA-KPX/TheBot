namespace KPX.EvePlugin.Data.Process

open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Process


type IEveCalculatorConfig =
    abstract InputMe: int
    abstract DerivationMe: int
    abstract ExpandPlanet: bool
    abstract ExpandReaction: bool

/// 制造、反应和行星材料
/// 未加Me的方法均返回原始过程
type EveProcessManager(cfg: IEveCalculatorConfig) =
    inherit RecipeManager<EveType, EveProcess>([ BlueprintCollection.Instance; PlanetProcessCollection.Instance ])

    static let instance =
        EveProcessManager(
            { new IEveCalculatorConfig with
                member x.InputMe = 0
                member x.DerivationMe = 0
                member x.ExpandPlanet = false
                member x.ExpandReaction = false }
        )

    /// 0材料，不展开行星和反应衍生
    static member Default = instance

    member x.TryGetRecipe(item, quantity: ProcessQuantity, me: int) =
        x.SearchRecipes(item)
        |> Seq.tryHead
        |> Option.map (fun proc ->
            { proc with
                TargetMe = me
                TargetQuantity = quantity })

    /// 获取指定数量的ime效率配方
    override x.TryGetRecipe(item, quantity) =
        x.TryGetRecipe(item, quantity, cfg.InputMe)

    /// 获取1流程，ime效率的配方
    override x.TryGetRecipe(item) =
        x.TryGetRecipe(item, ByRun 1.0, cfg.InputMe)

    /// 检查在当前条件下是否可以被展开
    member x.CanExpand(recipe: EveProcess) =
        (recipe.Type = ProcessType.Manufacturing)
        || (recipe.Type = ProcessType.Planet && cfg.ExpandPlanet)
        || (recipe.Type = ProcessType.Reaction && cfg.ExpandReaction)

    /// 获取指定数量、IMe/DMe效率的递归配方
    member x.TryGetRecipeRecMe(item: EveType, quantity: ProcessQuantity, ?ime: int, ?dme: int) =
        let ime = defaultArg ime cfg.InputMe
        let dme = defaultArg dme cfg.DerivationMe

        x.TryGetRecipe(item)
        |> Option.map (fun r ->
            let intermediate = ResizeArray<EveProcess>()
            let acc = RecipeProcessAccumulator<EveType>()

            let rec Calc i (q: float) me =
                let recipe = x.TryGetRecipe(i, ByItem q, me)

                if recipe.IsNone then
                    acc.Input.Update(i, q)
                else if x.CanExpand(recipe.Value) then
                    intermediate.Add(recipe.Value)
                    let proc = recipe.Value.ApplyFlags(MeApplied)

                    for m in proc.Input do
                        Calc m.Item m.Quantity dme
                else
                    acc.Input.Update(i, q)

            let itemQuantity = quantity.ToItems(r.Original)

            acc.Output.Update(r.Original.GetFirstProduct().Item, itemQuantity)
            Calc item itemQuantity ime

            {| InputProcess = r
               InputRuns = quantity.ToRuns(r.Original)
               FinalProcess = acc.AsRecipeProcess()
               IntermediateProcess = intermediate.ToArray() |})

/// 制造、反应和行星材料
/// 未加Me的方法均返回原始过程
type EveProcessManager2(cfg: IEveCalculatorConfig) =
    inherit RecipeManager2<EveType, EveProcess>([ BlueprintCollection.Instance; PlanetProcessCollection.Instance ])

    static let instance =
        EveProcessManager2(
            { new IEveCalculatorConfig with
                member x.InputMe = 0
                member x.DerivationMe = 0
                member x.ExpandPlanet = false
                member x.ExpandReaction = false }
        )

    /// 0材料，不展开行星和反应衍生
    static member Default = instance

    override x.CanExpandRecipe(proc) =
        (proc.Type = ProcessType.Manufacturing)
        || (proc.Type = ProcessType.Planet && cfg.ExpandPlanet)
        || (proc.Type = ProcessType.Reaction && cfg.ExpandReaction)