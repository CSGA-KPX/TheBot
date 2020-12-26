module KPX.TheBot.Utils.Config

open System

open LiteDB

open Newtonsoft.Json


type Int64JsonConverter() =
    inherit JsonConverter<uint64>()

    override x.WriteJson(writer : JsonWriter, value : uint64, _ : JsonSerializer) =
        writer.WriteValue(value |> string)

    override x.ReadJson(reader : JsonReader, _ : Type, _ : uint64, _ : bool, _ : JsonSerializer) =
        reader.Value |> string |> uint64

let private Db =
    KPX.TheBot.Data.Common.Database.DataBase.getLiteDB ("thebot_config.db")

[<RequireQualifiedAccess>]
type ConfigOwner =
    | Group of uint64
    | User of uint64
    | Discuss of uint64
    | System

[<CLIMutable>]
type ConfigItem =
    { [<BsonId(AutoId = false)>]
      Id : string
      Value : string }

type ConfigManager(owner : ConfigOwner) =
    static let col = Db.GetCollection<ConfigItem>()
    static let sysCfg = ConfigManager(ConfigOwner.System)

    member x.Get<'T>(name : string, defVal : 'T) =
        let id = sprintf "%s:%O" name owner
        let ret = col.FindById(BsonValue(id))

        if isNull (box ret) then defVal else JsonConvert.DeserializeObject<'T>(ret.Value)

    member x.Put(name : string, value : 'T) =
        let id = sprintf "%s:%O" name owner

        let obj =
            { Id = id
              Value = JsonConvert.SerializeObject(value) }

        col.Upsert(obj) |> ignore

    /// 全局配置
    static member SystemConfig = sysCfg
