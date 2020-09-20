namespace BotData.XivData.Mentor

open System

open BotData.Common.Database

[<CLIMutable>]
type StringRecord =
    { Id : int
      Value : string }

    static member FromString(str) =
        { Id = 0
          Value = str }

type ShouldOrAvoidCollection private () =
    inherit CachedTableCollection<int, StringRecord>()

    static let instance = ShouldOrAvoidCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        let col = BotDataInitializer.GetXivCollectionChs()
        let sht = col.GetSheet("ContentFinderCondition", [| "Name"; "MentorRoulette" |])
        seq {
            for row in sht do
                if row.As<bool>("MentorRoulette") then
                    yield { Id = 0
                            Value = row.As<string>("Name") }
            yield! "中途参战，红色划水，蓝色carry，绿色擦屁股，辱骂毒豆芽，辱骂假火，副职导随".Split('，') |> Array.map (StringRecord.FromString)
        }
        |> db.InsertBulk
        |> ignore

    member x.GetByIndex(id : int) = x.TryGetByKey(id).Value

type LocationCollection private () =
    inherit CachedTableCollection<int, StringRecord>()

    static let allowedLocation = Collections.Generic.HashSet<byte>([| 0uy; 1uy; 2uy; 6uy; 13uy; 14uy; 15uy |])

    static let instance = LocationCollection()
    static member Instance = instance

    override x.Depends = Array.empty

    override x.IsExpired = false

    override x.InitializeCollection() =
        let db = x.DbCollection
        printfn "Building LocationCollection"
        let col = BotDataInitializer.GetXivCollectionChs()
        let sht = col.GetSheet("TerritoryType", [| "PlaceName"; "TerritoryIntendedUse" |])
        seq {
            for row in sht do
                let isAllowed = allowedLocation.Contains(row.As<byte>("TerritoryIntendedUse"))
                let name = row.AsRow("PlaceName").As<string> ("Name")
                if isAllowed && (not <| String.IsNullOrWhiteSpace(name)) then
                    yield { Id = 0
                            Value = row.AsRow("PlaceName").As<string> ("Name") }
        }
        |> db.InsertBulk
        |> ignore
        GC.Collect()

    member x.GetByIndex(id : int) = x.TryGetByKey(id).Value