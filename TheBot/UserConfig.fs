module UserConfig
open System
open LiteDB

let private FsMapper = LiteDB.FSharp.FSharpBsonMapper()

let private Db =
    let dbFile = @"../static/userconfig.db"
    new LiteDB.LiteDatabase(dbFile, FsMapper)

[<RequireQualifiedAccess>]
type ConfigOwner = 
    | Group of uint64
    | User of uint64
    | Discuss of uint64

[<CLIMutable>]
type Config = 
    { Id : int
      Owner : ConfigOwner
      Config : Collections.Generic.Dictionary<string, string> }

    static member DefaultOf(owner) = 
        { Id = 0
          Owner = owner
          Config = new Collections.Generic.Dictionary<string, string>()}

and ConfigManager private () = 
    let col = Db.GetCollection<Config>()

    do
        col.EnsureIndex("Owner", true) |> ignore

    static let instance = new ConfigManager()
    static member Instance = instance

    member _.ConfigExists(owner : ConfigOwner) = 
        
        col.Exists(Query.EQ("owner", Db.Mapper.ToDocument(owner)))

    /// 如果不存在的话会返回个默认的
    member _.GetConfig(owner : ConfigOwner) = 
        let ret = col.FindOne(Query.EQ("Owner", Db.Mapper.ToDocument(owner)))
        if isNull (box ret) then
            Config.DefaultOf(owner)
        else
            ret

    member x.ClearConfig(owner : ConfigOwner) = 
        let cfg = x.GetConfig(owner)
        cfg.Config.Clear()
        x.SaveConfig(cfg)

    member x.SaveConfig(cfg : Config) = 
        col.Upsert(cfg)