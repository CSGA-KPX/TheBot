﻿namespace KPX.TheBot.Module.TRpgModule.DailySan

open System.Collections.Concurrent

open KPX.FsCqHttp.Handler

open KPX.TheBot.Utils.Dicer

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression

open KPX.TheBot.Module.TRpgModule.Coc7


type DailySanCacheItem = { Key : string; San : int }

type DailySanCacheCollection private () =
    let cache =
        ConcurrentDictionary<uint64, DailySanCacheItem>()

    member x.SetValue(cmdArg : CommandEventArgs, newVal : int) =
        let uid = cmdArg.MessageEvent.UserId
        let current = cache.[uid.Value]
        cache.TryUpdate(uid.Value, {current with San = newVal}, current) |> ignore

    member x.GetValue(cmdArg : CommandEventArgs) =
        let seed =
            SeedOption.SeedByUserDay(cmdArg.MessageEvent)

        let uid = cmdArg.MessageEvent.UserId
        let seedString = SeedOption.GetSeedString(seed)

        let succ, item = cache.TryGetValue(uid.Value)

        // 不存在或者不匹配都需要更新
        if not succ || (item.Key <> seedString) then
            let de = seed |> Dicer |> DiceExpression

            let initSan =
                Coc7AttrExpr
                |> Array.map (fun (attr, expr) -> attr, de.Eval(expr).Sum |> int)
                |> Array.find (fun (attr, _) -> attr = "意志")
                |> snd

            let newItem = { Key = seedString; San = initSan }

            cache.AddOrUpdate(uid.Value, newItem, (fun _ _ -> newItem))
            |> ignore

        cache.[uid.Value].San

    static member val Instance = DailySanCacheCollection()