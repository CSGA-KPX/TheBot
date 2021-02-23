namespace KPX.TheBot.Data.EveData.Process

open KPX.TheBot.Data.Common.Database

open KPX.TheBot.Data.CommonModule.Recipe

open KPX.TheBot.Data.EveData.EveType

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
    | QuantityApplied
    | MeApplied

[<CLIMutable>]
type EveProcess =
    { Original : RecipeProcess<EveType>
      TargetQuantity : ProcessQuantity
      TargetMe : int
      Type : ProcessType }

    member x.ApplyFlags(flag : ProcessFlag) =
        match flag with
        | Original -> x.Original
        | MeApplied when x.Type = ProcessType.Manufacturing ->
            let proc = x.ApplyFlags(QuantityApplied)
            let meFactor = (float (100 - x.TargetMe)) / 100.0

            let input =
                proc.Input
                |> Array.map
                    (fun rm ->
                        { rm with
                                Quantity = rm.Quantity * meFactor |> ceil })

            { proc with Input = input }
        | QuantityApplied | MeApplied ->
            let runs = x.TargetQuantity.ToRuns(x.Original)
            x.Original * runs

[<CLIMutable>]
type EveDbProcess =
    { [<BsonId(false)>]
      Id : int
      Process : RecipeProcess<int>
      Type : ProcessType }

    member x.AsEveProcess() =
        let convertMaterial (m : RecipeMaterial<int>) =
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
    inherit CachedTableCollection<int, EveDbProcess>(DefaultDB)

    override x.IsExpired = false

    override x.Depends = [| typeof<EveTypeCollection> |]
