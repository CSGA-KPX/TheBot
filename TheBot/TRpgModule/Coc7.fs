module KPX.TheBot.Module.TRpgModule.Coc7

open System
open System.Collections.Concurrent

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Handler

open KPX.TheBot.Utils.Dicer

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression


let Coc7AttrExpr =
    [| "力量", "3D6*5"
       "体质", "3D6*5"
       "体型", "(2D6+6)*5"
       "敏捷", "3D6*5"
       "外貌", "3D6*5"
       "智力", "(2D6+6)*5"
       "意志", "3D6*5"
       "教育", "(2D6+6)*5"
       "幸运", "3D6*5" |]

type DailySanCacheItem = { Key : string; San : int }

type DailySanCacheCollection private () =
    let cache =
        ConcurrentDictionary<uint64, DailySanCacheItem>()

    member x.SetValue(cmdArg : CommandEventArgs, newVal : int) =
        let uid = cmdArg.MessageEvent.UserId
        let current = cache.[uid]
        cache.TryUpdate(uid, {current with San = newVal}, current) |> ignore

    member x.GetValue(cmdArg : CommandEventArgs) =
        let seed =
            SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        let uid = cmdArg.MessageEvent.UserId
        let seedString = SeedOption.GetSeedString(seed)

        let succ, item = cache.TryGetValue(uid)

        // 不存在或者不匹配都需要更新
        if not succ || (item.Key <> seedString) then
            let de = seed |> Dicer |> DiceExpression

            let initSan =
                Coc7AttrExpr
                |> Array.map (fun (attr, expr) -> attr, de.Eval(expr).Sum |> int)
                |> Array.find (fun (attr, _) -> attr = "意志")
                |> snd

            let newItem = { Key = seedString; San = initSan }

            cache.AddOrUpdate(uid, newItem, (fun _ _ -> newItem))
            |> ignore

        cache.[uid].San

    static member val Instance = DailySanCacheCollection()
