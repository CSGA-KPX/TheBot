namespace KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.EveData.EveType
open KPX.TheBot.Data.EveData.Process


type IEveCalculatorConfig =
    abstract InputME : int
    abstract DerivedME : int
    abstract ExpandPlanet : bool
    abstract ExpandReaction : bool

/// 制造、反应和行星材料
/// 未加Me的方法均返回原始过程
type EveProcessManager(cfg : IEveCalculatorConfig) as x =
    inherit RecipeManager<EveType, EveProcess>()

    do
        x.AddProvider(BlueprintCollection.Instance)
        x.AddProvider(PlanetProcessCollection.Instance)

    static let instance =
        EveProcessManager(
            { new IEveCalculatorConfig with
                member x.InputME = 0
                member x.DerivedME = 0
                member x.ExpandPlanet = false
                member x.ExpandReaction = false }
        )

    /// 0材料，不展开行星和反应衍生
    static member Default = instance

    member x.TryGetRecipe(item, quantity : ProcessQuantity, me : int) =
        x.SearchRecipes(item)
        |> Seq.tryHead
        |> Option.map
            (fun proc ->
                { proc with
                      TargetMe = me
                      TargetQuantity = quantity })

    /// 获取指定数量的0效率配方
    override x.TryGetRecipe(item, quantity) = x.TryGetRecipe(item, quantity, cfg.InputME)

    /// 获取1流程，0效率的配方
    override x.TryGetRecipe(item) = x.TryGetRecipe(item, ByRun 1.0, cfg.InputME)

    /// 获取指定数量、IMe/DMe效率的递归配方
    member x.TryGetRecipeRecMe(item : EveType, quantity : ProcessQuantity, ?ime : int, ?dme : int) =
        let ime = defaultArg ime cfg.InputME
        let dme = defaultArg dme cfg.DerivedME

        let canExpand (recipe : EveProcess) =
            (recipe.Type = ProcessType.Manufacturing)
            || (recipe.Type = ProcessType.Planet
                && cfg.ExpandPlanet)
            || (recipe.Type = ProcessType.Reaction
                && cfg.ExpandReaction)

        x.TryGetRecipe(item)
        |> Option.filter
            (fun r ->
                // 如果根据条件不能展开，按没找到处理
                canExpand (r))
        |> Option.map
            (fun r ->
                let intermediate = ResizeArray<EveProcess>()
                let acc = RecipeProcessAccumulator<EveType>()

                let rec Calc i (q : float) me =
                    let recipe = x.TryGetRecipe(i, ByItem q, me)

                    if recipe.IsNone then
                        acc.Input.Update(i, q)
                    else
                        if canExpand (recipe.Value) then
                            intermediate.Add(recipe.Value)
                            let proc = recipe.Value.ApplyFlags(MeApplied)

                            for m in proc.Input do
                                Calc m.Item m.Quantity dme
                        else
                            acc.Input.Update(i, q)

                let itemQuantity = quantity.ToItems(r.Original)

                acc.Output.Update(item, itemQuantity)
                Calc item (itemQuantity) ime

                {| InputProcess = r
                   InputRuns = quantity.ToRuns(r.Original)
                   FinalProcess = acc.AsRecipeProcess()
                   IntermediateProcess = intermediate.ToArray() |})
