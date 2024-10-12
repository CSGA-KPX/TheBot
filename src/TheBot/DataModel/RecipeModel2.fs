namespace rec KPX.TheBot.Host.DataModel.Recipe

open System
open System.Collections.Generic


[<CLIMutable>]
[<Struct>]
type RecipeMaterial<'Item> =
    { Item: 'Item
      Quantity: float }

    static member inline Create(item, quantity) = { Item = item; Quantity = quantity }

    static member (*)(that: RecipeMaterial<'Item>, c: int) =
        { that with Quantity = that.Quantity * (float c) }

    static member (*)(that: RecipeMaterial<'Item>, c: float) =
        { that with Quantity = that.Quantity * c }

    static member (+)(that: RecipeMaterial<'Item>, c: int) =
        { that with Quantity = that.Quantity + (float c) }

    static member (+)(that: RecipeMaterial<'Item>, c: float) =
        { that with Quantity = that.Quantity + c }

/// 不足一流程时计算方式
[<Struct>]
[<RequireQualifiedAccess>]
type ProcessRunRounding =
    /// 按照理论材料计算
    | AsIs
    /// 进位1流程计算
    | RoundUp

    member x.GetRuns(targetQuantity, itemsPerRun) =
        let runs = targetQuantity / itemsPerRun

        match x with
        | AsIs -> runs
        | RoundUp -> ceil runs

[<Struct>]
type ProcessQuantity =
    /// 按流程数计量
    | ByRuns of runs: float
    /// 按产出物品数量计量
    | ByItems of items: float

    member x.ToItems(proc: RecipeProcess<_>) =
        match x with
        | ByItems value -> value
        | ByRuns value -> value * proc.Product.Quantity

    /// 将物品数转换为流程数
    member x.ToRuns(proc: RecipeProcess<_>, ?rounding: ProcessRunRounding) =
        match x with
        | ByRuns value -> value
        | ByItems value ->
            let rounding = defaultArg rounding ProcessRunRounding.RoundUp
            rounding.GetRuns(value, proc.Product.Quantity)

type IRecipeProcess<'Item when 'Item: equality> =
    abstract Materials: seq<RecipeMaterial<'Item>>
    abstract Products: seq<RecipeMaterial<'Item>>

[<CLIMutable>]
[<Struct>]
type RecipeProcess<'Item when 'Item: equality> =
    { Materials: RecipeMaterial<'Item> []
      Product: RecipeMaterial<'Item> }

    static member (*)(that: RecipeProcess<'Item>, runs: int) =
        { Materials = that.Materials |> Array.map (fun mr -> mr * runs)
          Product = that.Product * (float runs) }

    static member (*)(that: RecipeProcess<'Item>, runs: float) =
        { Materials = that.Materials |> Array.map (fun mr -> mr * runs)
          Product = that.Product * runs }

    interface IRecipeProcess<'Item> with
        member x.Materials = x.Materials
        member x.Products = Seq.singleton x.Product

type ItemAccumulator<'Item when 'Item: equality>(mrs: seq<RecipeMaterial<'Item>>) =
    let data = Dictionary<'Item, RecipeMaterial<'Item>>()

    do
        for mr in mrs do
            data.[mr.Item] <- RecipeMaterial<_>.Create (mr.Item, mr.Quantity)

    new(item: 'Item) =
        let mr = RecipeMaterial<'Item>.Create (item, 1.0)
        ItemAccumulator<'Item>(Seq.singleton mr)

    new() = ItemAccumulator<'Item>(Seq.empty)

    member x.Count = data.Count

    member x.IsEmpty = data.Count = 0

    member x.Clear() = data.Clear()

    member x.Update(material: RecipeMaterial<'Item>) =
        x.Update(material.Item, material.Quantity)

    member x.Update(item: 'Item, quantity: float) =
        if data.ContainsKey(item) then
            data.[item] <- data.[item] + quantity
        else
            data.[item] <- RecipeMaterial<_>.Create (item, quantity)

    member x.Contains(item) = data.ContainsKey(item)

    member x.Item
        with get item =
            if not <| data.ContainsKey(item) then
                data.[item] <- RecipeMaterial<_>.Create (item, 0)

            data.[item].Quantity
        and set item v = data.[item] <- RecipeMaterial<_>.Create (item, v)

    member x.ToArray() = data.Values |> Seq.toArray

    member x.PositiveQuantityItems = x |> Seq.filter (fun x -> x.Quantity > 0)

    member x.NegativeQuantityItems = x |> Seq.filter (fun x -> x.Quantity < 0)

    member x.NonZeroItems = x |> Seq.filter (fun x -> x.Quantity <> 0)

    member x.GetItems() = data.Keys |> Seq.toArray

    interface IEnumerable<RecipeMaterial<'Item>> with
        member x.GetEnumerator() =
            data.Values.GetEnumerator() :> IEnumerator<RecipeMaterial<'Item>>

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            data.Values.GetEnumerator() :> Collections.IEnumerator

type MaterialInventory<'Item when 'Item: equality>(mrs: seq<RecipeMaterial<'Item>>) =
    inherit ItemAccumulator<'Item>(mrs)

    new() = MaterialInventory<'Item>(Seq.empty)

    /// 返回还需要多少
    member x.Rent(mr: RecipeMaterial<'Item>) =
        if not <| x.Contains(mr.Item) then
            x.Update(mr.Item, 0.0)

        let had = x.[mr.Item]
        let need = max 0.0 (mr.Quantity - had)
        x.[mr.Item] <- max 0.0 (had - mr.Quantity)

        need

    member x.RentTo(acc: ItemAccumulator<'Item>) =
        for m in acc do
            acc.[m.Item] <- x.Rent(m)

type RecipeProcessBuilder<'Item when 'Item: equality>() =

    new(proc: RecipeProcess<'Item>, ?runs: float) as x =
        RecipeProcessBuilder<'Item>()
        then
            let runs = defaultArg runs 1.0

            for material in proc.Materials do
                x.Materials.Update(material.Item, material.Quantity * runs)

            x.Products.Update(proc.Product * runs)

    new(proc: IRecipeProcess<'Item>, ?runs: float) as x =
        RecipeProcessBuilder<'Item>()
        then
            let runs = defaultArg runs 1.0

            for material in proc.Materials do
                x.Materials.Update(material.Item, material.Quantity * runs)

            for product in proc.Products do
                x.Products.Update(product * runs)

    member val Materials: ItemAccumulator<'Item> = ItemAccumulator<'Item>()
    member val Products: ItemAccumulator<'Item> = ItemAccumulator<'Item>()

    member x.UpdateFrom(proc: IRecipeProcess<'Item>) =
        for m in proc.Materials do
            x.Materials.Update(m)

        for p in proc.Products do
            x.Products.Update(p)

    interface IRecipeProcess<'Item> with
        member x.Materials = x.Materials
        member x.Products = x.Products
