namespace BotData.EveData.Process

open System

open BotData.CommonModule.Recipe

open BotData.EveData.EveType
open BotData.EveData.Process


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
    /// 0材料，不展开
    static member Default = instance

    member private x.ApplyQuantity(recipe : EveProcess, quantity : ProcessQuantity) =
        if recipe.Flag.HasFlag(ProcessFlags.MeApplied) then invalidOp "已经计算过材料效率"

        if recipe.Flag.HasFlag(ProcessFlags.QuantityApplied) then invalidOp "已经调整过流程数"

        let runs = quantity.ToRuns(recipe.Process)

        { recipe with
              Process =
                  { Input =
                        recipe.Process.Input
                        |> Array.map
                            (fun mr ->
                                { mr with
                                      Quantity = mr.Quantity * runs })
                    Output =
                        recipe.Process.Output
                        |> Array.map
                            (fun mr ->
                                { mr with
                                      Quantity = mr.Quantity * runs }) }
              Flag = ProcessFlags.QuantityApplied }

    member private x.ApplyMe(recipe : EveProcess, me) =
        if recipe.Type = ProcessType.Manufacturing then
            if recipe.Flag.HasFlag(ProcessFlags.MeApplied) then invalidOp "已经计算过材料效率"
            // 只有制造项目使用材料效率
            let meFactor = (float (100 - me)) / 100.0

            let input =
                recipe.Process.Input
                |> Array.map
                    (fun rm ->
                        { rm with
                              Quantity = rm.Quantity * meFactor |> ceil })

            let proc = { recipe.Process with Input = input }

            { recipe with
                  Process = proc
                  Flag = recipe.Flag ||| ProcessFlags.MeApplied }
        else
            recipe

    member x.ApplyProcess(proc : EveProcess, quantity : ProcessQuantity, ?ime : int) =
        let ime = defaultArg ime cfg.InputME
        x.ApplyMe(x.ApplyQuantity(proc, quantity), ime)

    /// 获取指定数量和效率配方
    member x.TryGetRecipeMe(item : EveType, quantity, ?ime : int) =
        let ime = defaultArg ime cfg.InputME

        x.TryGetRecipe(item)
        |> Option.map (fun recipe -> x.ApplyMe(x.ApplyQuantity(recipe, quantity), ime))

    /// 获取指定数量的0效率配方
    override x.TryGetRecipe(item, quantity) = x.TryGetRecipeMe(item, quantity, 0)

    /// 获取1流程，0效率的配方
    override x.TryGetRecipe(item) = x.SearchRecipes(item) |> Seq.tryHead

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
                let intermediate = Collections.Generic.List<EveProcess>()
                let acc = RecipeProcessAccumulator<EveType>()

                let rec Calc i (q : float) me =
                    let recipe = x.TryGetRecipeMe(i, ByItem q, me)

                    if recipe.IsNone then
                        acc.Input.Update(i, q)
                    else
                        let recipe = recipe.Value

                        if canExpand (recipe) then
                            intermediate.Add(recipe)

                            for m in recipe.Process.Input do
                                Calc m.Item m.Quantity dme
                        else
                            acc.Input.Update(i, q)

                acc.Output.Update(item, quantity.ToItems(r.Process))
                Calc item (quantity.ToItems(r.Process)) ime

                let final =
                    { Process = acc.AsRecipeProcess()
                      Type = ProcessType.Invalid
                      Flag =
                          ProcessFlags.MeApplied
                          ||| ProcessFlags.QuantityApplied }

                {| FinalProcess = final
                   IntermediateProcess = intermediate.ToArray() |})
