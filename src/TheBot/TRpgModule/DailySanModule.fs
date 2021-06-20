namespace KPX.TheBot.Module.TRpgModule.DailySanModule

open System.Collections.Concurrent

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.TheBot.Utils.Dicer

open KPX.TheBot.Module.DiceModule.Utils.DiceExpression

open KPX.TheBot.Module.TRpgModule.Coc7


type private DailySanCacheItem = { Key : string; San : int }

type private DailySanCacheCollection private () =
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
    
type DailySanModule() =
    inherit CommandHandlerBase()
    
    [<CommandHandlerMethod("#sc", "#coc7联动每日理智鉴定 #sc 成功/失败", "", IsHidden = true)>]
    member x.HandleSanCheck(cmdArg : CommandEventArgs) =
        let args = cmdArg.HeaderArgs // 参数检查

        if args.Length <> 1
           || (not <| args.[0].Contains("/")) then
            cmdArg.Abort(InputError, $"参数错误，请参考指令帮助 #help %s{cmdArg.CommandAttrib.Command}")

        let san = DailySanCacheCollection.Instance.GetValue(cmdArg)
        
        let succ, fail =
            let s = args.[0].Split("/")
            s.[0], s.[1]
            
        let de = DiceExpression()
        let check = de.Eval("1D100").Sum |> int
        
        let status = RollResult.Describe(check, san)
        let lose =
            match status with
            | RollResult.Critical ->
                DiceExpression.ForceMinDiceing.Eval(succ).Sum
            | RollResult.Fumble ->
                DiceExpression.ForceMaxDiceing.Eval(fail).Sum
            | _ when status.IsSuccess -> de.Eval(succ).Sum
            | _ -> de.Eval(fail).Sum
            |> int
        
        let finalSan = max 0 (san - lose)
        
        use ret = cmdArg.OpenResponse(ForceText)
        ret.WriteLine("1D100 = {0}：{1}", check, status)
        
        DailySanCacheCollection.Instance.SetValue(cmdArg, finalSan)
        ret.WriteLine("今日San值减少{0}点，当前剩余{1}点。", lose, finalSan)
        
    [<TestFixture>]
    member x.TestDailySc() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#sc 100/100")
        tc.ShouldNotThrow("#sc 1D10/1D100")