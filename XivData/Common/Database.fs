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
    let FsMapper = LiteDB.FSharp.FSharpBsonMapper()
    let Db =
        let dbFile = @"Filename=../static/BotDataCache.db; Journal=true; Cache Size=500; Mode=Exclusive"
        let db = new LiteDB.LiteDatabase(dbFile, FsMapper)
        db

[<AbstractClass>]
type BotDataCollection<'Key, 'Value>() as x = 

    let col = Db.GetCollection<'Value>(x.GetType().Name)

    /// 声明依赖项
    abstract Depends : Type []

    /// 清空当前集合，不释放空间
    member x.Clear() = col.Delete(LiteDB.Query.All())

    member x.Count() = col.Count()

    member x.GetByKey(key : 'Key) = col.FindById(LiteDB.BsonValue(key))

    member x.TryGetByKey(key : 'Key) = 
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


[<AbstractClass>]
type CachedTableCollection<'Key, 'Value>() = 
    inherit BotDataCollection<'Key, 'Value>()

    abstract IsExpired : bool

    /// 处理数据并添加到数据库，建议在事务内处理
    abstract InitializeCollection : unit -> unit

type BotDataInitializer private () = 
    static let StaticData = Dictionary<string, obj>()

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

    /// 获得一个全局的英文FF14数据库
    /// 
    /// 会载入加载器缓存，在InitializeCollection外使用时需要调用ClearStaticData清除缓存
    static member GetXivCollectionEng() = 
        let succ, col = StaticData.TryGetValue("XIV_COL_ENG")
        if succ then
            let col = col :?> IXivCollection
            col.ClearCache()
            col
        else
            let col = 
                let ss = 
                    let archive = 
                            let ResName = "BotData.ffxiv-datamining-master.zip"
                            let assembly = Reflection.Assembly.GetExecutingAssembly()
                            let stream = assembly.GetManifestResourceStream(ResName)
                            new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Read)
                    EmbeddedCsvStroage(archive, "ffxiv-datamining-master/csv/") :> ISheetStroage<seq<string []>>
                EmbeddedXivCollection(ss, XivLanguage.None) :> IXivCollection
            StaticData.Add("XIV_COL_ENG", col)
            col

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
                t.GetMethod("InitializeCollection").Invoke(o, null) |> ignore )
        BotDataInitializer.ClearStaticData()

    /// 删除所有数据，不释放空间
    static member ClearCache() = 
        for name in Db.GetCollectionNames() |> Seq.toArray do
            Db.DropCollection(name) |> ignore

    /// 整理数据库文件，释放多余空间
    static member ShrinkCache() = 
        Db.Shrink() |> ignore