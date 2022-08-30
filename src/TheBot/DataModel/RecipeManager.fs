namespace KPX.TheBot.Host.DataModel.Recipe

open System.Collections.Generic


/// 不足一流程时计算方式
[<Struct>]
[<RequireQualifiedAccess>]
type ProcessRunRounding =
    /// 按照理论材料计算
    | AsIs
    /// 进位1流程计算
    | RoundUp

[<Struct>]
type ProcessQuantity =
    /// 按流程数计量
    | ByRun of runs: float
    /// 按产出物品数量计量
    | ByItem of items: float

    member x.ToItems(proc: RecipeProcess<_>) =
        match x with
        | ByItem value -> value
        | ByRun value ->
            let qPerRun = proc.GetFirstProduct().Quantity
            value * qPerRun

    /// 将物品数转换为流程数
    member x.ToRuns(proc: RecipeProcess<_>) =
        match x with
        | ByRun value -> value
        | ByItem value ->
            let qPerRun = proc.GetFirstProduct().Quantity
            value / qPerRun

    /// 将物品数转换为流程数
    member x.ToRuns(proc: RecipeProcess<_>, rounding: ProcessRunRounding) =
        match x with
        | ByRun value -> value
        | ByItem value ->
            let qPerRun = proc.GetFirstProduct().Quantity
            let runs = value / qPerRun

            match rounding with
            | ProcessRunRounding.AsIs -> runs
            | ProcessRunRounding.RoundUp -> ceil runs

[<AbstractClass>]
type RecipeManager<'Item, 'Recipe when 'Item: equality>(providers: seq<IRecipeProvider<'Item, 'Recipe>>) =

    /// 从Provider查找所有配方
    member x.SearchRecipes(item: 'Item) =
        providers |> Seq.choose (fun p -> p.TryGetRecipe(item))

    /// 查找指定物品的生产配方
    abstract TryGetRecipe: 'Item -> 'Recipe option
    abstract TryGetRecipe: 'Item * ProcessQuantity -> 'Recipe option

    member x.TryGetRecipe(material: RecipeMaterial<'Item>) =
        x.TryGetRecipe(material.Item, ByItem material.Quantity)

    member x.GetRecipe(item: 'Item) = x.TryGetRecipe(item).Value

    member x.GetRecipe(item: 'Item, quantity: ProcessQuantity) = x.TryGetRecipe(item, quantity).Value

    member x.GetRecipe(material: RecipeMaterial<'Item>) = x.TryGetRecipe(material).Value

type IRecipeProcess<'Item when 'Item: equality> =
    /// 获取单流程配方
    abstract Process: RecipeProcess<'Item>

type RecipeCalculationContext<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>>() =
    let recipeBook = Dictionary<'Item, 'Recipe list>()

    let input = ItemAccumulator<'Item>()
    let output = ItemAccumulator<'Item>()

    /// 获取所有相关物品，方便批量获取信息
    member x.GetAllItems() =
        let ret = HashSet<'Item>()

        for recipes in recipeBook.Values do
            for recipe in recipes do
                for i in recipe.Process.Input do
                    ret.Add(i.Item) |> ignore

                for o in recipe.Process.Output do
                    ret.Add(o.Item) |> ignore

        let arr = Array.zeroCreate ret.Count
        ret.CopyTo(arr)
        arr

    member x.AddRecipe(recipes: 'Recipe list) =
        if recipes.Length = 0 then
            invalidArg "recipes" "没有配方：列表长度为0"

        let product = recipes.[0].Process.GetFirstProduct().Item

        let allRecipeSameProduct =
            recipes
            |> List.forall (fun proc -> proc.Process.GetFirstProduct().Item = product)

        if not allRecipeSameProduct then
            invalidArg "recipes" "配方产物不一致"

        recipeBook.Add(product, recipes)

    member x.GetRecipes(item) = recipeBook.[item]

    member x.TryGetRecipes(item) =
        if recipeBook.ContainsKey(item) then
            Some recipeBook.[item]
        else
            None

    member x.GetRecipe(item) = recipeBook.[item].Head

    member x.TryGetRecipe(item) =
        if recipeBook.ContainsKey(item) then
            Some recipeBook.[item].Head
        else
            None

    member x.ContainsRecipe(item) = recipeBook.ContainsKey(item)

    member x.AddInventory(item: 'Item, quantity: float) =
        if quantity < 0 then
            invalidArg "quantity" "已有材料数量错误"

        input.Update(item, -quantity)

    member x.AddInventory(mr: RecipeMaterial<'Item>) =
        if mr.Quantity < 0 then
            invalidArg "quantity" "已有材料数量错误"

        input.Update(mr.Item, -mr.Quantity)

    member x.Materials = input

    member x.Products = output

    /// 获取扣除已有后材料数量（不会更新已有材料）
    member x.GetRequired(item: 'Item, quantity: float) =
        if input.Contains(item) && input.[item].Quantity < 0 then
            quantity + input.[item].Quantity
        else
            quantity

[<AbstractClass>]
type RecipeManager2<'Item, 'Recipe when 'Item: equality and 'Recipe :> IRecipeProcess<'Item>>
    (
        providers: seq<IRecipeProvider<'Item, 'Recipe>>
    ) =

    /// 从Provider查找所有配方
    member x.GetRecipes(item: 'Item) =
        providers |> Seq.choose (fun p -> p.TryGetRecipe(item)) |> Seq.toList

    /// 从Provider查找所有配方
    member x.GetRecipe(item: 'Item) =
        let ret = x.GetRecipes(item)

        if ret.Length = 0 then
            invalidOp $"找不到{item}的制作配方"

        ret |> List.head

    abstract CanExpandRecipe: 'Recipe -> bool

    member x.GetRecipeContext(items: seq<'Item>) =
        let ctx = RecipeCalculationContext<_, _>()

        let depthLimit = System.Int32.MaxValue

        let rec build items depth =
            for item in items do
                if not <| ctx.ContainsRecipe(item) then
                    let ret = x.GetRecipes(item) |> List.filter x.CanExpandRecipe

                    if ret.Length <> 0 then
                        ctx.AddRecipe(ret |> List.filter x.CanExpandRecipe)

                        for recipe in ret do
                            let items = recipe.Process.Input |> Seq.map (fun mr -> mr.Item)
                            let nextDepth = depth + 1

                            if nextDepth < depthLimit then
                                build items nextDepth

        build items 0

        ctx

    member x.GetRecipeContext(item: 'Item) = x.GetRecipeContext(Seq.singleton item)

    member x.GetRecipeContext(mrs: seq<RecipeMaterial<'Item>>) =
        x.GetRecipeContext(mrs |> Seq.map (fun mr -> mr.Item))
