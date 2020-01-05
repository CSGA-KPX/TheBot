﻿module TheBot.Utils.Config
open System
open LiteDB
open LiteDB.FSharp.Extensions
open Newtonsoft.Json

type Int64JsonConverter() = 
    inherit JsonConverter<uint64>()

    override x.WriteJson(writer : JsonWriter, value : uint64, serializer: JsonSerializer) = 
        writer.WriteValue(value |> string)

    override x.ReadJson(reader : JsonReader, objType : Type, ext : uint64, hasExt : bool,  serializer : JsonSerializer) =
        reader.Value
        |> string
        |> uint64

let private Db =
    FSharp.FSharpBsonMapper.UseCustomJsonConverters([|Int64JsonConverter(); FSharp.FSharpJsonConverter();|])
    let FsMapper = FSharp.FSharpBsonMapper()
    let dbFile = @"../static/thebot_config.db"
    new LiteDatabase(dbFile, FsMapper)

[<RequireQualifiedAccess>]
type ConfigOwner = 
    | Group of uint64
    | User of uint64
    | Discuss of uint64
    | System

[<CLIMutable>]
type ConfigItem = 
    {
        [<BsonId(AutoId = false)>]
        Id : string
        Value : string
    }

type ConfigManager (owner : ConfigOwner) = 
    static let col = Db.GetCollection<ConfigItem>()

    member x.Get<'T>(name : string, defVal : 'T) = 
        let id = sprintf "%s:%O" name owner
        col.TryFindById(BsonValue(id))
        |> Option.map (fun item -> JsonConvert.DeserializeObject<'T>(item.Value))
        |> Option.defaultValue defVal

    member x.Put(name : string, value : 'T) = 
        let id = sprintf "%s:%O" name owner
        let obj = {Id = id; Value = JsonConvert.SerializeObject(value)}
        col.Upsert(obj) |> ignore