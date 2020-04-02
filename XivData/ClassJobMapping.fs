module XivData.ClassJobMapping

open System
open LibFFXIV.GameData.Raw

type Mapping = 
    {
        [<LiteDB.BsonIdAttribute(false)>]
        Id : string
        Value : string
    }

type ClassJobMappingCollection private () =
    inherit Utils.XivDataSource<string, Mapping>()

    static let instance = ClassJobMappingCollection()
    static member Instance = instance

    override x.BuildCollection() =
        let db = x.Collection
        printfn "Building ClassJobMappingCollection"
        let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified) :> IXivCollection

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
        |> Seq.map (fun (a,b) -> {Id = a; Value = b})
        |> db.InsertBulk
        |> ignore
        GC.Collect()