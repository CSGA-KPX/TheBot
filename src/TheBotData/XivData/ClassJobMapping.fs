namespace KPX.TheBot.Data.XivData.ClassJobMapping

open System

open KPX.TheBot.Data.Common.Database


[<CLIMutable>]
type ClassJobMapping =
    { [<LiteDB.BsonId(false)>]
      Id : string
      Value : string }

type ClassJobMappingCollection private () =
    inherit CachedTableCollection<string, ClassJobMapping>(DefaultDB)

    static let instance = ClassJobMappingCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        let col = BotDataInitializer.XivCollectionChs

        seq {
            for row in col.ClassJob.TypedRows do
                let abbr = row.Abbreviation.AsString()
                yield abbr, abbr
                yield row.Name.AsString(), abbr
                yield row.RAW_2.AsString(), abbr

            yield! [| "占星", "AST"; "诗人", "BRD" |]
        }
        |> Seq.filter (fun (a, _) -> not <| String.IsNullOrWhiteSpace(a))
        |> Seq.map (fun (a, b) -> { Id = a; Value = b })
        |> db.InsertBulk
        |> ignore

    member x.SearchByName(name : string) = x.DbCollection.SafeFindById(name)
    member x.TrySearchByName(name : string) = x.DbCollection.TryFindById(name)
