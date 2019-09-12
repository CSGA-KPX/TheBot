module DiceModule
open System
open KPX.FsCqHttp.Instance.Base

type DiceModule() = 
    inherit HandlerModuleBase()

    override x.MessageHandler _ arg =
        let msg = arg.Data.AsMessageEvent
        let str = msg.Message.ToString()
        let dicer = new Utils.Dicer(Utils.SeedOption.SeedByUserDay, msg)
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#c") ->
            //让每一个选项不同顺序的情况下都一样
            dicer.AutoRefreshSeed <- false
            let sw = new IO.StringWriter()
            sw.WriteLine("选项 1d100")
            let choices =
                s.TrimEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries).[1..]
                |> Array.map (fun c ->
                    (c, dicer.GetRandomFromString(c, 100u)))
                |> Array.sortBy (fun (_, n) -> n)
            for (c,n) in choices do 
                sw.WriteLine("{0} {1}", c, n)
            arg.QuickMessageReply(sw.ToString())
        | s when s.StartsWith("#jrrp") -> 
            let jrrp = dicer.GetRandom(100u)
            arg.QuickMessageReply(sprintf "%s今日人品值是%i" (x.ToNicknameOrCard(msg)) jrrp)
        | _ -> ()