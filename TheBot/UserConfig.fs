module UserConfig
open System
open LiteDB
open LiteDB.FSharp.Extensions

let private FsMapper = LiteDB.FSharp.FSharpBsonMapper()

let private Db =
    let dbFile = @"../static/userconfig.db"
    new LiteDB.LiteDatabase(dbFile, FsMapper)

[<RequireQualifiedAccess>]
type ConfigOwner = 
    | Group of uint64
    | User of uint64
    | Discuss of uint64
    | System

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
    let colName = "UserConfig"
    let col = Db.GetCollection<Config>(colName)

    do
        col.EnsureIndex("Owner", true) |> ignore

    static let instance = new ConfigManager()
    static member Instance = instance

    /// 根据消息信息返回配置
    /// 用户配置->群配置->默认用户配置
    member x.GetConfig(msg : KPX.FsCqHttp.DataType.Event.Message.MessageEvent) = 
        [|
            if msg.IsDiscuss then yield ConfigOwner.Discuss msg.DiscussId
            if msg.IsGroup then yield ConfigOwner.Group msg.GroupId
            yield ConfigOwner.User (msg.UserId |> uint64)
        |]
        |> Array.tryPick (fun o -> col.tryFindOne(<@ fun x -> x.Owner = o @>))
        |> Option.defaultWith (fun () -> Config.DefaultOf(ConfigOwner.User (msg.UserId |> uint64)))
        
    /// 如果不存在的话会返回个默认的
    member _.GetConfig(owner : ConfigOwner) = 
        let ret = col.tryFindOne <@ fun x -> x.Owner = owner @>
        if ret.IsNone then
            Config.DefaultOf(owner)
        else
            ret.Value

    member x.ClearConfig(owner : ConfigOwner) = 
        let cfg = x.GetConfig(owner)
        cfg.Config.Clear()
        x.SaveConfig(cfg)

    member x.ClearAllConfig() = 
        Db.DropCollection(colName) |> ignore

    member x.SaveConfig(cfg : Config) = 
        col.Upsert(cfg) |> ignore


[<AbstractClass>]
type ConfigItem<'T>(owner : ConfigOwner) = 
    
    member x.Owner = owner

    abstract ConfigKey : string

    default x.ConfigKey = x.GetType().Name

    abstract Default : string

    abstract DeserializeData : string -> 'T

    abstract SerializeData : 'T -> string

    member x.Load() = 
        let cfg = ConfigManager.Instance.GetConfig(owner)
        if not <| cfg.Config.ContainsKey(x.ConfigKey) then
            cfg.Config.Add(x.ConfigKey, x.Default)
        cfg.Config.[x.ConfigKey] |> x.DeserializeData

    member x.Save(value : 'T) = 
        let cfg = ConfigManager.Instance.GetConfig(owner)
        cfg.Config.[x.ConfigKey] <- x.SerializeData(value)
        ConfigManager.Instance.SaveConfig(cfg)
