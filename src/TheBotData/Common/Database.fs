namespace rec KPX.TheBot.Data.Common.Database

open System
open System.IO
open System.IO.Compression
open System.Collections.Generic
open System.Reflection

open LiteDB

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Data.Common.Resource


type TypedXivCollection = XivCollectionProvider<XivTPSample, "none", "ffxiv-datamining-cn-master/">

[<AutoOpen>]
module Helpers =
    let private dbCache = Dictionary<string, LiteDatabase>()

    type ILiteCollection<'T> with
        member x.SafeFindById(id : obj) =
            let ret = x.FindById(BsonValue(id))

            if isNull (box ret) then
                let msg = $"不能在%s{x.Name}中找到%A{id}"
                raise <| KeyNotFoundException(msg)

            ret

        member x.TryFindById(id : obj) =
            let ret = x.FindById(BsonValue(id))
            if isNull (box ret) then None else Some ret
            
        member x.TryFindOne(query : Query) =
            let ret = x.FindOne(query)
            if isNull (box ret) then None else Some ret
            
        member x.TryFindOne(expr : BsonExpression) =
            let ret = x.FindOne(expr)
            if isNull (box ret) then None else Some ret

    let getLiteDB (name : string) =
        if not <| dbCache.ContainsKey(name) then
            let path = GetStaticFile(name)
            let dbFile = $"Filename=%s{path};"
            let db = new LiteDatabase(dbFile)
            dbCache.Add(name, db)

        dbCache.[name]

    [<Literal>]
    let internal DefaultDB = "BotDataCache.db"

    do
        BsonMapper.Global.EmptyStringToNull <- false
        BsonMapper.Global.EnumAsInteger <- true

type IInitializationInfo =
    abstract Depends : Type []

[<AbstractClass>]
type BotDataCollection<'Key, 'Item>(dbName) as x =

    let colName = x.GetType().Name

    /// 调用InitializeCollection时的依赖项，
    /// 对在TheBotData外定义的项目无效
    abstract Depends : Type []

    member val Logger = NLog.LogManager.GetLogger $"%s{dbName}:%s{colName}"

    /// 获取数据库集合供复杂操作
    member x.DbCollection =
        getLiteDB(dbName).GetCollection<'Item>(colName)

    /// 清空当前集合，不释放空间
    member x.Clear() = x.DbCollection.DeleteAll() |> ignore

    member x.Count() = x.DbCollection.Count()

    /// 辅助方法：如果input为Some，返回值。如果为None，根据fmt和args生成KeyNotFoundException
    member internal x.PassOrRaise(input : option<'T>, fmt : string, [<ParamArray>] args : obj []) =
        if input.IsNone then
            raise
            <| KeyNotFoundException(String.Format(fmt, args))

        input.Value

    interface Collections.IEnumerable with
        member x.GetEnumerator() =
            x.DbCollection.FindAll().GetEnumerator() :> Collections.IEnumerator


    interface IEnumerable<'Item> with
        member x.GetEnumerator() =
            x.DbCollection.FindAll().GetEnumerator()

    interface IInitializationInfo with
        member x.Depends = x.Depends

[<AbstractClass>]
type CachedItemCollection<'Key, 'Item>(dbName) =
    inherit BotDataCollection<'Key, 'Item>(dbName)

    /// 获取一个'Value，不经过不写入缓存
    abstract DoFetchItem : 'Key -> 'Item

    abstract IsExpired : 'Item -> bool

    /// 强制获得一个'Item，然后写入缓存
    member x.FetchItem(key : 'Key) =
        let item = x.DoFetchItem(key)
        x.DbCollection.Upsert(item) |> ignore
        item

    /// 获得一个'Item，如果有缓存优先拿缓存
    member x.GetItem(key : 'Key) =
        let ret =
            x.DbCollection.TryFindById(BsonValue(key))

        if ret.IsNone || x.IsExpired(ret.Value) then
            x.FetchItem(key)
        else
            ret.Value

[<CLIMutable>]
type TableUpdateTime =
    { [<BsonId(false)>]
      Id : string
      Updated : DateTimeOffset }

[<AbstractClass>]
type CachedTableCollection<'Key, 'Item>(dbName) =
    inherit BotDataCollection<'Key, 'Item>(dbName)

    let updateLock = obj ()

    abstract IsExpired : bool

    /// 处理数据并添加到数据库，建议在事务内处理
    abstract InitializeCollection : unit -> unit

    member x.CheckUpdate() =
        lock
            updateLock
            (fun () ->
                if x.IsExpired then
                    x.Clear()
                    x.InitializeCollection()
                    x.RegisterCollectionUpdate())

    member x.RegisterCollectionUpdate() =
        BotDataInitializer.RegisterCollectionUpdate(x.GetType().Name)

    member x.GetLastUpdateTime() =
        BotDataInitializer.GetCollectionUpdateTime(x.GetType().Name)

type BotDataInitializer private () =

    static let updateCol =
        getLiteDB(DefaultDB)
            .GetCollection<TableUpdateTime>()

    static let mutable xivArchive : ZipArchive option = None

    static let mutable xivCollection : TypedXivCollection option = None

    static member FreshXivCollection() =
        if xivArchive.IsSome then xivArchive.Value.Dispose()

        let archivePath =
            Path.Combine(StaticDataPath, "ffxiv-datamining-cn-master.zip")

        xivArchive <-
            Some
            <| new ZipArchive(File.OpenRead(archivePath), ZipArchiveMode.Read)

        xivCollection <-
            Some
            <| new TypedXivCollection(
                XivLanguage.None,
                xivArchive.Value,
                "ffxiv-datamining-cn-master/"
            )

    /// 获得一个全局的中文FF14数据库
    static member internal XivCollectionChs =
        if xivCollection.IsNone then
            BotDataInitializer.FreshXivCollection()

        xivCollection.Value

    /// 记录CachedTableCollection<>的更新时间
    static member RegisterCollectionUpdate(name : string) =
        let record =
            { Id = name
              Updated = DateTimeOffset.Now }

        updateCol.Upsert(record) |> ignore

    static member GetCollectionUpdateTime(name : string) =
        let ret = updateCol.FindById(BsonValue(name))

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

        let left =
            other
            |> Array.map (fun x -> x.GetType(), x)
            |> dict
        // Add resolved
        for m in start do
            resolved.Add(m.GetType(), m)

        let rec doJob (m : IInitializationInfo) =
            unsolved.Add(m.GetType(), m)

            for dep in m.Depends do
                if not <| resolved.ContainsKey(dep) then
                    if unsolved.ContainsKey(dep) then failwithf "CR!"
                    if not <| left.ContainsKey(dep) then failwithf "Not found!"
                    doJob left.[dep]

            resolved.Add(m.GetType(), m)
            unsolved.Remove(m.GetType()) |> ignore
            output.Enqueue(m)

        for m in other do
            doJob m

        output.ToArray()

    static member private IsSubclassOfRawGeneric(generic : Type, toCheck : Type) =
        let rec check (t : Type) =
            if t = typeof<Object> then
                false
            elif isNull t then
                false
            elif t.IsGenericType
                 && t.GetGenericTypeDefinition().Name = generic.Name then
                true
            else
                check t.BaseType

        check toCheck

    /// 初始化该THeBotData定义的所有数据集，
    /// 对在TheBotData外定义的项目无效
    static member InitializeAllCollections() =
        Assembly.GetExecutingAssembly().GetTypes()
        |> Array.filter
            (fun t ->
                (typeof<IInitializationInfo>.IsAssignableFrom t
                 && (not t.IsAbstract)))
        |> Array.map
            (fun t ->
                let p =
                    t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)

                if isNull p then failwithf $"{t}.Instance is Null!"
                p.GetValue(null) :?> IInitializationInfo)
        |> BotDataInitializer.SolveDependency
        |> Array.iter
            (fun o ->
                let t = o.GetType()
                let gt = typeof<CachedTableCollection<_, _>>

                let isCollection =
                    BotDataInitializer.IsSubclassOfRawGeneric(gt, t)

                printfn $"正在处理：%s{t.FullName}"

                if isCollection then
                    t
                        .GetMethod("InitializeCollection")
                        .Invoke(o, null)
                    |> ignore

                    GC.Collect())

        printfn "处理完成"

    /// 删除所有数据，不释放空间
    static member ClearCache() =
        let db = getLiteDB DefaultDB
        // 避免删除以后影响之后的序列
        for name in db.GetCollectionNames() |> Seq.cache do
            db.DropCollection(name) |> ignore

    /// 整理数据库文件，释放多余空间
    static member ShrinkCache() =
        getLiteDB(DefaultDB).Rebuild() |> ignore
