namespace BotData.EveData.Process

open System

open BotData.Common.Database

open BotData.CommonModule.Recipe

open BotData.EveData.EveType

open LiteDB


type internal EveDbMaterial = RecipeMaterial<int>

type ProcessType = 
    | Invalid = 0
    | Manufacturing = 1
    | Planet = 2
    | Reaction = 3
    | Refine = 4

[<Flags>]
type ProcessFlags = 
    | None = 0
    | QuantityApplied = 1
    | MeApplied = 2

type EveProcess =
    { Process : RecipeProcess<EveType>
      Type : ProcessType
      Flag : ProcessFlags }

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

        { Process = { Input = x.Process.Input |> Array.map convertMaterial
                      Output = x.Process.Output |> Array.map convertMaterial}
          Type = x.Type
          Flag = ProcessFlags.None }

[<AbstractClass>]
type EveProcessCollection() = 
    inherit CachedTableCollection<int, EveDbProcess>()

    override x.IsExpired = false

    override x.Depends = [| typeof<EveTypeCollection> |]