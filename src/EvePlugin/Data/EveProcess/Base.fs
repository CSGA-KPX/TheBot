namespace KPX.EvePlugin.Data.Process

open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataModel.Recipe

open KPX.EvePlugin.Data.EveType

open LiteDB


type internal EveDbMaterial = RecipeMaterial<int>

type ProcessType =
    | Invalid = 0
    | Manufacturing = 1
    | Planet = 2
    | Reaction = 3
    | Refine = 4

type ProcessFlag =
    | Original
    | QuantityApplied of ProcessRunRounding
    /// MeApplied隐含QuantityApplied。对于制造项目会计算材料效率
    | MeApplied of ProcessRunRounding

[<CLIMutable>]
type EveProcess =
    { Original: RecipeProcess<EveType>
      TargetQuantity: ProcessQuantity
      TargetMe: int
      Type: ProcessType }

    member x.ApplyFlags(flag: ProcessFlag) =
        match flag with
        | Original -> x.Original
        | MeApplied rounding when x.Type = ProcessType.Manufacturing ->
            let meFactor = (float (100 - x.TargetMe)) / 100.0
            let runs = x.TargetQuantity.ToRuns(x.Original, rounding)

            let meApplied =
                { x.Original with
                    Input =
                        x.Original.Input
                        |> Array.map (fun rm -> { rm with Quantity = rm.Quantity * meFactor |> ceil }) }

            meApplied * runs
        // 除了制造以外的项目不需要计算材料效率
        | QuantityApplied rounding ->
            let runs = x.TargetQuantity.ToRuns(x.Original, rounding)
            x.Original * runs
        | MeApplied rounding ->
            let runs = x.TargetQuantity.ToRuns(x.Original, rounding)
            x.Original * runs

    member x.SetQuantity(quantity: ProcessQuantity) = { x with TargetQuantity = quantity }

    member x.SetMe(me: int) = { x with TargetMe = me }

    member x.Set(quantity: ProcessQuantity, me: int) =
        { x with
            TargetQuantity = quantity
            TargetMe = me }

    interface IRecipeProcess<EveType> with
        member x.Process = x.Original

[<CLIMutable>]
type EveDbProcess =
    { [<BsonId(false)>]
      Id: int
      Process: RecipeProcess<int>
      Type: ProcessType }

    member x.AsEveProcess() =
        let convertMaterial (m: RecipeMaterial<int>) =
            { Item = EveTypeCollection.Instance.GetById(m.Item)
              Quantity = m.Quantity }

        let proc =
            { Input = x.Process.Input |> Array.map convertMaterial
              Output = x.Process.Output |> Array.map convertMaterial }

        { Original = proc
          Type = x.Type
          TargetQuantity = ByRun 1.0
          TargetMe = 0 }

[<AbstractClass>]
type EveProcessCollection() =
    inherit CachedTableCollection<EveDbProcess>()

    override x.IsExpired = false

    override x.Depends = [| typeof<EveTypeCollection> |]
