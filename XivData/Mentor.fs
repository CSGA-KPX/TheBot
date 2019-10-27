module XivData.Mentor
open System
open LibFFXIV.GameData.Raw

[<CLIMutable>]
type StringRecord = 
    {
        Id    : int
        Value : string
    }

    static member FromString(str) = 
        {
            Id = 0 
            Value = str
        }

type ShouldOrAvoidCollection private () =
    inherit Utils.XivDataSource()

    let colName = "ShouldOrAvoid"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<StringRecord>(colName)

    do
        if not exists then
            let db = Utils.Db.GetCollection<StringRecord>(colName)
            printfn "Building ShouldOrAvoidCollection"
            let col = new XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let sht = col.GetSelectedSheet("ContentFinderCondition", [|"Name"; "MentorRoulette"|])
            seq {
                for row in sht do
                    let row = row.Value
                    if row.As<bool>("MentorRoulette") then
                        yield {Id = 0; Value = row.As<string>("Name")}
                yield!
                    "中途参战，红色划水，蓝色carry，绿色擦屁股，辱骂毒豆芽，辱骂假火，副职导随".Split('，')
                    |> Array.map (StringRecord.FromString)
                printfn "end"
            } |> db.InsertBulk |> ignore
            GC.Collect()

    static let instance = new ShouldOrAvoidCollection()
    static member Instance = instance

    member x.Count = db.Count()

    member x.Item (id : int) = 
        db.FindById(new LiteDB.BsonValue(id))


type LocationCollection private () =
    inherit Utils.XivDataSource()

    let colName = "LocationCollection"
    let exists = Utils.Db.CollectionExists(colName)
    let db = Utils.Db.GetCollection<StringRecord>(colName)
    let allowedLocation = Collections.Generic.HashSet<byte>([|0uy; 1uy; 2uy; 6uy; 13uy; 14uy; 15uy;|])

    do
        if not exists then
            //build from scratch
            let db = Utils.Db.GetCollection<StringRecord>(colName)
            printfn "Building LocationCollection"
            let col = new XivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            let sht = col.GetSelectedSheet("TerritoryType", [|"PlaceName"; "TerritoryIntendedUse"|])
            seq {
                for row in sht do
                    let row = row.Value
                    let isAllowed = allowedLocation.Contains(row.As<byte>("TerritoryIntendedUse"))
                    let name  = row.AsRow("PlaceName").As<string>("Name")
                    if isAllowed && (not <| String.IsNullOrWhiteSpace(name)) then
                        yield {Id = 0; Value = row.AsRow("PlaceName").As<string>("Name")}
            } |> db.InsertBulk |> ignore
            GC.Collect()

    static let instance = new LocationCollection()
    static member Instance = instance

    member x.Count = db.Count()

    member x.Item (id : int) = 
        db.FindById(new LiteDB.BsonValue(id))