namespace BotData.CommonModule.Recipe

open System
open System.Collections.Generic


[<CLIMutable>]
type RecipeMaterial<'Item> =
    {
        Item : 'Item
        Quantity : float
    }

type ItemAccumulator<'Item when 'Item : equality>() =
    let data = Dictionary<'Item, RecipeMaterial<'Item>>()

    member x.IsEmpty = data.Count = 0

    member x.Update(material : RecipeMaterial<'Item>) = x.Update(material.Item, material.Quantity)
    member x.Update(item : 'Item) = x.Update(item, 1.0)
    member x.Update(item : 'Item, quantity : float) =
        let succ, ret = data.TryGetValue(item)
        if succ then 
            data.[item] <- {ret with Quantity = ret.Quantity + quantity}
        else
            data.[item] <- {Item = item; Quantity = quantity}

    member x.Set(item, quantity) = 
        data.[item] <- {Item = item; Quantity = quantity}

    member x.AsMaterials() = 
        data.Values |> Seq.toArray
    
    member x.MergeFrom(y : ItemAccumulator<'Item>) = 
        for v in y do
            x.Update(v)

    interface IEnumerable<RecipeMaterial<'Item>> with
        member x.GetEnumerator() = data.Values.GetEnumerator() :> IEnumerator<RecipeMaterial<'Item>>

    interface Collections.IEnumerable with
        member x.GetEnumerator() = data.Values.GetEnumerator() :> Collections.IEnumerator


type RecipeProcessAccumulator<'Item when 'Item : equality>() = 
    let input = ItemAccumulator<'Item>()
    let output = ItemAccumulator<'Item>()

    member x.Input = input
    member x.Output = output

    member x.AsRecipeProcess() = 
        { Input = x.Input.AsMaterials()
          Output = x.Output.AsMaterials() }

and [<CLIMutable>] RecipeProcess<'Item when 'Item : equality> = 
    { Input : RecipeMaterial<'Item> []
      Output : RecipeMaterial<'Item> [] }

    /// 获得第一个Output(一般游戏只有一个输出)
    /// 如果有多个则抛出异常
    member x.GetFirstProduct() = 
        if x.Output.Length <> 1 then invalidOp (sprintf "该过程不止一个产物：%A" x)
        x.Output |> Array.head

type IRecipeProvider<'Item, 'Recipe when 'Item : equality> =
    abstract TryGetRecipe : 'Item -> 'Recipe option