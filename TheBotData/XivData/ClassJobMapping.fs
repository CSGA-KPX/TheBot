namespace BotData.XivData.ClassJobMapping

open System

open BotData.Common.Database

[<CLIMutable>]
type Mapping = 
    {
        [<LiteDB.BsonIdAttribute(false)>]
        Id : string
        Value : string
    }

type ClassJobMappingCollection private () =
    inherit CachedTableCollection<string, Mapping>()

    static let instance = ClassJobMappingCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        use col = BotDataInitializer.XivCollectionChs
        let sht = col.GetSheet("ClassJob")
        seq {
            for row in sht do
                let abbr = row.As<string>("Abbreviation")
                yield row.As<string>("Name"), abbr
                yield row.As<string>("Abbreviation"), abbr
                yield row.As<string>(2), abbr
            yield! [|
                "占星", "AST"
                "诗人", "BRD"
            |]
        }
        |> Seq.filter (fun (a,_) -> not <| String.IsNullOrWhiteSpace(a))
        |> Seq.map (fun (a,b) -> {Id = a; Value = b})
        |> db.InsertBulk
        |> ignore

    member x.SearchByName(name) = x.GetByKey(name)
    member x.TrySearchByName(name) = x.TryGetByKey(name)