namespace KPX.TheBot.Host.DataCache

open System
open System.Reflection
open System.Collections.Generic

open KPX.TheBot.Host
open KPX.TheBot.Host.DataCache


module private Utils =
    /// <summary>
    ///
    /// </summary>
    /// <param name="modules"></param>
    let solveDependency (modules: IInitializationInfo []) =
        // Find all start nodes
        let start, other = modules |> Array.partition (fun m -> m.Depends.Length = 0)

        let output = Queue(start)
        let resolved = Dictionary<Type, _>()
        let unsolved = Dictionary<Type, _>()

        let left = other |> Array.map (fun x -> x.GetType(), x) |> dict
        // Add resolved
        for m in start do
            resolved.Add(m.GetType(), m)

        let rec doJob (m: IInitializationInfo) =
            unsolved.Add(m.GetType(), m)

            for dep in m.Depends do
                if not <| resolved.ContainsKey(dep) then
                    if unsolved.ContainsKey(dep) then
                        failwithf "CR!"

                    if not <| left.ContainsKey(dep) then
                        failwithf "Not found!"

                    doJob left.[dep]

            resolved.Add(m.GetType(), m)
            unsolved.Remove(m.GetType()) |> ignore
            output.Enqueue(m)

        for m in other do
            doJob m

        output.ToArray()

    let isSubclassOfRawGeneric (generic: Type, toCheck: Type) =
        let rec check (t: Type) =
            if t = typeof<Object> then
                false
            elif isNull t then
                false
            elif t.IsGenericType && t.GetGenericTypeDefinition().Name = generic.Name then
                true
            else
                check t.BaseType

        check toCheck

module BotDataInitializer =

    let clearAndShrinkCache () =
        let db = Data.DataAgent.GetCacheDatabase()

        for name in db.GetCollectionNames() |> Seq.cache do
            db.DropCollection(name) |> ignore

        db.Rebuild() |> ignore

    let buildAllCache (discover: HostedModuleDiscover) =
        let cols =
            discover.CacheCollections
            |> Seq.map
                (fun t ->
                    let p = t.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)

                    if isNull p then
                        failwithf $"{t}.Instance is Null!"

                    p.GetValue(null) :?> IInitializationInfo)
            |> Seq.toArray
            |> Utils.solveDependency

        let gt = typeof<CachedTableCollection<_, _>>

        for col in cols do
            let t = col.GetType()

            let isCollection = Utils.isSubclassOfRawGeneric (gt, t)

            printfn $"正在处理：%s{t.FullName}"

            if isCollection then
                t
                    .GetMethod("InitializeCollection")
                    .Invoke(col, null)
                |> ignore

        GC.Collect()
        printfn "处理完成"

    let rebuildAllCache (discover) =
        clearAndShrinkCache ()
        buildAllCache (discover)

    let runDataTests (discover: HostedModuleDiscover) =
        for testType in discover.CacheCollectionTests do
            if testType.IsSubclassOf(typeof<DataTest>) then
                printfn $"执行测试类：%s{testType.FullName}"

                (Activator.CreateInstance(testType) :?> DataTest)
                    .RunTest()
            else
                let p = testType.GetProperty("Instance", BindingFlags.Public ||| BindingFlags.Static)

                if isNull p then
                    failwithf $"{testType}.Instance is Null!"

                printfn $"执行测试接口：%s{testType.FullName}"
                (p.GetValue(null) :?> IDataTest).RunTest()
