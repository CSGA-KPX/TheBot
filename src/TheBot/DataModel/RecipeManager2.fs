namespace rec KPX.TheBot.Host.DataModel.Recipe


type IRecipeProvider<'Item, 'Recipe when 'Item: equality> =
    abstract TryGetRecipe: 'Item -> 'Recipe option

type IRecipeConfig<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>> =
    /// 所需数量不足1流程时计算方式
    abstract RunRounding: ProcessRunRounding
    /// 判断该配方能否被展开
    abstract CanExpandRecipe: 'Recipe -> bool
    /// 当出现多个配方时，选择一个进行后续计算
    abstract SolveSameProduct: seq<'Recipe> -> 'Recipe

[<AbstractClass>]
type RecipeConfig<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>>() =
    member val RunRounding = ProcessRunRounding.RoundUp with get, set

    abstract CanExpandRecipe: 'Recipe -> bool

    default x.CanExpandRecipe(_) = true

    abstract SolveSameProduct: seq<'Recipe> -> 'Recipe

    interface IRecipeConfig<'Item, 'Recipe> with

        member x.RunRounding = x.RunRounding

        member x.CanExpandRecipe(recipe) = x.CanExpandRecipe(recipe)

        member x.SolveSameProduct(recipes) = x.SolveSameProduct(recipes)

[<AbstractClass>]
type RecipeManager<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>>
    (
        providers: seq<IRecipeProvider<'Item, 'Recipe>>,
        cfg: IRecipeConfig<'Item, 'Recipe>
    ) =

    /// 配方 数量 展开层数
    abstract ApplyProcessQuantity: 'Recipe * ProcessQuantity * int -> IRecipeProcess<'Item>

    member x.Config = cfg

    /// 从Provider查找所有配方，需要满足CanExpand
    member x.GetRecipes(item: 'Item) =
        providers
        |> Seq.choose (fun p ->
            p.TryGetRecipe(item)
            |> Option.filter (fun recipe -> cfg.CanExpandRecipe(recipe)))
        |> Seq.toList

    /// 从Provider查找所有配方
    member x.GetRecipe(item: 'Item) =
        let ret = x.GetRecipes(item)

        if ret.Length = 0 then
            invalidOp $"找不到{item}的制作配方"

        ret |> cfg.SolveSameProduct

    member x.TryGetRecipe(item: 'Item) =
        let ret = x.GetRecipes(item)

        if ret.Length = 0 then
            None
        else
            ret |> cfg.SolveSameProduct |> Some

    /// 从Provider查找所有配方
    member x.GetMaterials(item: 'Item, ?quantity: ProcessQuantity) =
        let quantity = defaultArg quantity (ByRuns 1.0)
        let ret = x.GetRecipes(item)

        if ret.Length = 0 then
            invalidOp $"找不到{item}的制作配方"

        x.ApplyProcessQuantity(ret |> cfg.SolveSameProduct, quantity, 0)

    member x.TryGetMaterials(item: 'Item, ?quantity: ProcessQuantity) =
        let quantity = defaultArg quantity (ByRuns 1.0)

        x.TryGetRecipe(item)
        |> Option.map (fun proc -> x.ApplyProcessQuantity(proc, quantity, 0))

    member x.GetMaterials(mr: RecipeMaterial<'Item>) =
        x.GetMaterials(mr.Item, ByItems mr.Quantity)

    member x.TryGetMaterials(mr: RecipeMaterial<'Item>) =
        x.TryGetMaterials(mr.Item, ByItems mr.Quantity)

    member x.GetMaterials(mrs: seq<RecipeMaterial<'Item>>) =
        let acc = RecipeProcessBuilder<'Item>()

        for mr in mrs do
            let ret = x.GetMaterials(mr)

            for m in ret.Materials do
                acc.Materials.Update(m)

            for p in ret.Products do
                acc.Products.Update(p)

        acc :> IRecipeProcess<_>

    member x.GetMaterialsRec(mrs: seq<RecipeMaterial<'Item>>, ?inv: MaterialInventory<'Item>, ?depthLimit: int) =
        let depthLimit = defaultArg depthLimit System.Int32.MaxValue
        let inv = defaultArg inv (MaterialInventory<'Item>(Seq.empty))
        let acc = RecipeProcessBuilder<'Item>()
        let intermediate = ResizeArray<ProcessQuantity * 'Recipe * int>()

        let rec build (mrs: seq<RecipeMaterial<'Item>>) depth =
            for mr in mrs do
                if mr.Quantity < 0.0 then
                    invalidArg $"{mr}" "参数错误：需要数量为负数"

                let required =
                    if depth = 0 then
                        mr.Quantity
                    else
                        inv.Rent(mr)

                if depth >= depthLimit then
                    acc.Materials.Update(mr.Item, required)
                else
                    match x.TryGetRecipe(mr.Item) with
                    | None ->
                        if depth = 0 then
                            invalidOp $"输入物品%A{mr.Item} 没有生产配方"

                        acc.Materials.Update(mr.Item, required)
                    | Some recipe ->
                        let quantity = ByItems required
                        intermediate.Add(quantity, recipe, depth)
                        let proc = x.ApplyProcessQuantity(recipe, ByItems required, depth)

                        if depth = 0 then
                            for p in proc.Products do
                                acc.Products.Update(p)

                        build proc.Materials (depth + 1)

        build mrs 0

        let items = System.Collections.Generic.HashSet<'Item>()

        for (_, recipe, _) in intermediate do
            for material in recipe.Materials do
                items.Add(material.Item) |> ignore

            for product in recipe.Products do
                items.Add(product.Item) |> ignore

        { FinalProcess = acc :> IRecipeProcess<'Item>
          IntermediateProcess = intermediate.ToArray()
          RelatedItems = items |> Seq.toArray }

    member x.TryGetMaterialsRec(mr: RecipeMaterial<'Item>, ?inv: MaterialInventory<'Item>, ?depthLimit: int) =
        let depthLimit = defaultArg depthLimit System.Int32.MaxValue
        let inv = defaultArg inv (MaterialInventory<'Item>(Seq.empty))
        let acc = RecipeProcessBuilder<'Item>()
        let intermediate = ResizeArray<ProcessQuantity * 'Recipe * int>()

        let rec build (mrs: seq<RecipeMaterial<'Item>>) depth =
            for mr in mrs do
                if mr.Quantity < 0.0 then
                    invalidArg $"{mr}" "参数错误：需要数量为负数"

                let required =
                    if depth = 0 then
                        mr.Quantity
                    else
                        inv.Rent(mr)

                if depth >= depthLimit then
                    acc.Materials.Update(mr.Item, required)
                else
                    match x.TryGetRecipe(mr.Item) with
                    | None ->
                        if depth = 0 then
                            invalidOp $"输入物品%A{mr.Item} 没有生产配方"

                        acc.Materials.Update(mr.Item, required)
                    | Some recipe ->
                        let quantity = ByItems required
                        intermediate.Add(quantity, recipe, depth)
                        let proc = x.ApplyProcessQuantity(recipe, ByItems required, depth)

                        if depth = 0 then
                            for p in proc.Products do
                                acc.Products.Update(p)

                        build proc.Materials (depth + 1)

        let materials = x.TryGetMaterials(mr)

        acc.Products.Update(mr)

        if materials.IsSome then
            // 已经展开过一次了，所以加一层
            build (Seq.singleton mr) 0

            let items = System.Collections.Generic.HashSet<'Item>()

            for (_, recipe, _) in intermediate do
                for material in recipe.Materials do
                    items.Add(material.Item) |> ignore

                for product in recipe.Products do
                    items.Add(product.Item) |> ignore

            Some
                { FinalProcess = acc :> IRecipeProcess<'Item>
                  IntermediateProcess = intermediate.ToArray()
                  RelatedItems = items |> Seq.toArray }
        else
            None

type MaterialsRecContext<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>> =
    { FinalProcess: IRecipeProcess<'Item>
      /// 注意：这里的配方都是原始配方
      IntermediateProcess: (ProcessQuantity * 'Recipe * int) []
      RelatedItems: 'Item [] }
