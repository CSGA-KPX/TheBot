namespace rec KPX.TheBot.Data.Common.Database

open System
open System.Collections.Generic
open System.Reflection

open KPX.TheBot.Data.Common.Testing


type BotDataInitializer private () =

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

    static member RunTests() =
        for t in Assembly.GetExecutingAssembly().GetTypes() do
            let hasTest =
                (typeof<IDataTest>.IsAssignableFrom t
                 && (not t.IsAbstract))

            if hasTest then
                if t.IsSubclassOf(typeof<DataTest>) then
                    printfn $"执行测试类：%s{t.FullName}"

                    (Activator.CreateInstance(t) :?> DataTest)
                        .RunTest()
                else
                    let p =
                        t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)

                    if isNull p then failwithf $"{t}.Instance is Null!"
                    printfn $"执行测试接口：%s{t.FullName}"
                    (p.GetValue(null) :?> IDataTest).RunTest()
