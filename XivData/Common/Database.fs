namespace BotData.Common.Database
// 此命名空间提供一个统一的数据库接口供数据缓存使用

open System
open System.Collections.Generic
open System.Reflection

open LibFFXIV.GameData.Raw

type IInitializationInfo = 
    abstract Depends : Type []

[<AutoOpen>]
module private DataBase =
    // 警告：不要把数据库作为static let放入泛型类
    // static let不在每种泛型中共享
    let logger = NLog.LogManager.GetLogger("LiteDB")
    let FsMapper = LiteDB.FSharp.FSharpBsonMapper()
    let Db =
        let dbFile = @"Filename=../static/BotDataCache.db; Journal=true; Mode=Exclusive;"
        let db = new LiteDB.LiteDatabase(dbFile, FsMapper)
        db.Log.add_Logging(fun str -> logger.Trace(str))
        db

[<AbstractClass>]
type BotDataCollection<'Key, 'Value>() as x = 

    let col = Db.GetCollection<'Value>(x.GetType().Name)
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)

    /// 声明依赖项
    abstract Depends : Type []

    member internal x.Logger = logger

    /// 清空当前集合，不释放空间
    member x.Clear() = col.Delete(LiteDB.Query.All()) |> ignore

    member x.Count() = col.Count()

    /// 辅助方法：如果input为Some，返回值。如果为None，根据fmt和args生成KeyNotFoundException
    member internal x.PassOrRaise(input : option<'T>, fmt : string, [<ParamArray>] args : obj []) = 
        if input.IsNone then
            raise <| KeyNotFoundException(String.Format(fmt, args))
        input.Value

    member internal x.GetByKey(key : 'Key) = 
        x.PassOrRaise(x.TryGetByKey(key), "BotDataCollection内部错误：找不到键{0}", key)

    member internal x.TryGetByKey(key : 'Key) = 
        let ret = col.FindById(LiteDB.BsonValue(key))
        if isNull (box ret) then None
        else Some ret

    /// 获取数据库集合供复杂操作
    member internal x.DbCollection = col

    interface Collections.IEnumerable with
        member x.GetEnumerator() = col.FindAll().GetEnumerator() :> Collections.IEnumerator


    interface Collections.Generic.IEnumerable<'Value> with
        member x.GetEnumerator() = col.FindAll().GetEnumerator()

    interface IInitializationInfo with
        member x.Depends = x.Depends

[<AbstractClass>]
type CachedItemCollection<'Key, 'Value>() = 
    inherit BotDataCollection<'Key, 'Value>()

    /// 获取一个'Value，不经过不写入缓存
    abstract FetchItem : 'Key   -> 'Value

    abstract IsExpired : 'Value -> bool

    /// 强制获得一个'Value，然后写入缓存
    member x.Force(key) = 
        let item = x.FetchItem(key)
        x.DbCollection.Upsert(item) |> ignore
        item

    member x.Item(key) =
        let item = x.TryGetByKey(key)
        if item.IsNone || x.IsExpired(item.Value)
        then x.Force(key)
        else item.Value

[<CLIMutable>]
type TableUpdateTime = 
    {
        [<LiteDB.BsonId(false)>]
        Id : string
        Updated : DateTimeOffset
    }

[<AbstractClass>]
type CachedTableCollection<'Key, 'Value>() = 
    inherit BotDataCollection<'Key, 'Value>()

    abstract IsExpired : bool

    /// 处理数据并添加到数据库，建议在事务内处理
    abstract InitializeCollection : unit -> unit

    member x.CheckUpdate() = 
        if x.IsExpired then 
            x.Clear()
            x.InitializeCollection()
            x.RegisterCollectionUpdate()

    member x.RegisterCollectionUpdate() = 
        BotDataInitializer.RegisterCollectionUpdate(x.GetType().Name)

    member x.GetLastUpdateTime() = 
        BotDataInitializer.GetCollectionUpdateTime(x.GetType().Name)

and BotDataInitializer private () = 
    static let StaticData = Dictionary<string, obj>()

    static let updateCol = Db.GetCollection<TableUpdateTime>()

    static member ClearStaticData() = StaticData.Clear()

    /// 获得一个全局的中文FF14数据库
    /// 
    /// 会载入加载器缓存，在InitializeCollection外使用时需要调用ClearStaticData清除缓存
    static member GetXivCollectionChs() = 
        let succ, col = StaticData.TryGetValue("XIV_COL_CHS")
        if succ then
            let col = col :?> IXivCollection
            col.ClearCache()
            col
        else
            let col = EmbeddedXivCollection(XivLanguage.ChineseSimplified) :> IXivCollection
            StaticData.Add("XIV_COL_CHS", col)
            col

    /// 记录CachedTableCollection<>的更新时间
    static member RegisterCollectionUpdate(name : string) = 
        let record = 
            {
                Id = name
                Updated = DateTimeOffset.Now
            }
        updateCol.Upsert(record) |> ignore

    static member GetCollectionUpdateTime(name : string ) =
        let ret = updateCol.FindById(LiteDB.BsonValue(name))
        if isNull (box ret) then
            DateTimeOffset.MinValue
        else
            ret.Updated

    /// 输入BotDataCollection数组，按照依赖顺序排序
    static member private SolveDependency(modules : IInitializationInfo []) = 
        // Find all start nodes
        let start, other = 
            modules
            |> Array.partition (fun m -> m.Depends.Length = 0)

        let output = Queue(start)
        let resolved = Dictionary<Type, _>()
        let unsolved = Dictionary<Type, _>()
        let left     = other |> Array.map (fun x -> x.GetType(), x) |> dict
        // Add resolved
        for m in start do resolved.Add(m.GetType(), m)

        let rec doJob(m : IInitializationInfo) = 
            unsolved.Add(m.GetType(), m)
            for dep in m.Depends do 
                if not <| resolved.ContainsKey(dep) then
                    if unsolved.ContainsKey(dep) then failwithf "CR!"
                    if not <| left.ContainsKey(dep) then failwithf "Not found!"
                    doJob(left.[dep])
            resolved.Add(m.GetType(), m)
            unsolved.Remove(m.GetType()) |> ignore
            output.Enqueue(m)

        for m in other do doJob(m)

        output.ToArray()
    
    // NOT TESTED
    static member private IsSubclassOfRawGeneric(generic : Type, toCheck : Type) = 
        let rec check (t : Type) = 
            if t = typeof<Object> then false
            elif isNull t then false
            elif t.IsGenericType && t.GetGenericTypeDefinition().Name = generic.Name then true
            else check(t.BaseType)
        check(toCheck)
        
    /// 初始化该Assembly定义的所有数据集
    static member InitializeAllCollections() = 
        BotDataInitializer.ClearStaticData()
        Assembly.GetExecutingAssembly().GetTypes()
        |> Array.filter (fun t -> (typeof<IInitializationInfo>.IsAssignableFrom(t) && (not (t.IsAbstract))))
        |> Array.map (fun t -> 
            let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)
            p.GetValue(null) :?> IInitializationInfo)
        |> BotDataInitializer.SolveDependency
        |> Array.iter (fun o ->
            let t = o.GetType()
            let gt = typeof<CachedTableCollection<_, _>>
            let isCollection = BotDataInitializer.IsSubclassOfRawGeneric(gt, t)
            printfn "正在处理：%s  -->  %A" t.Name isCollection
            if isCollection then
                t.GetMethod("InitializeCollection").Invoke(o, null) |> ignore
                GC.Collect()
            )
        BotDataInitializer.ClearStaticData()

    /// 删除所有数据，不释放空间
    static member ClearCache() = 
        for name in Db.GetCollectionNames() |> Seq.toArray do
            Db.DropCollection(name) |> ignore

    /// 整理数据库文件，释放多余空间
    static member ShrinkCache() = 
        Db.Shrink() |> ignore