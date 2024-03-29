namespace KPX.EvePlugin.Data.SolarSystems

open System
open System.IO

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb


[<CLIMutable>]
type SolarSystem = { Id: int; Name: string }

type SolarSystemCollection private () =
    inherit CachedTableCollection<SolarSystem>()

    static let instance = SolarSystemCollection()

    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex(LiteDB.BsonExpression.Create("Name")) |> ignore

        seq {
            use archive = KPX.EvePlugin.Data.Utils.GetEveDataArchive()

            use f = archive.GetEntry("SolarSystem.tsv").Open()

            use r = new StreamReader(f)

            while r.Peek() <> -1 do
                let line = r.ReadLine()

                if not <| String.IsNullOrWhiteSpace(line) then
                    let a = line.Split('\t')
                    let sid, sname = a.[0] |> int, a.[1]
                    yield { Id = sid; Name = sname }
        }
        |> x.DbCollection.InsertBulk
        |> ignore

    member x.TryGetBySolarSystem(id: int) = x.DbCollection.TryFindById(id)

    member x.GetBySolarSystem(id: int) =
        x.PassOrRaise(x.DbCollection.TryFindById(id), "找不到星系id:{0}", id)

    member x.TryGetBySolarSystem(name: string) =
        let bson = LiteDB.BsonValue(name)

        let ret = x.DbCollection.FindOne(LiteDB.Query.EQ("Name", bson))

        if isNull (box ret) then
            None
        else
            Some(ret)

    member x.GetBySolarSystem(name: string) =
        x.PassOrRaise(x.TryGetBySolarSystem(name), "找不到星系:{0}", id)
