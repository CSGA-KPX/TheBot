module DiceModule
open System
open System.Security.Cryptography
open KPX.TheBot.WebSocket
open KPX.TheBot.WebSocket.Instance

type DiceModule() = 
    inherit HandlerModuleBase()

    let md5 = MD5.Create()
    let utf8 = Text.Encoding.UTF8
    let getDateStr () = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8.0)).ToString("yyyyMMdd")
    let strToPct (str : string) = 
        // 101u是质数可能更均匀，目前用100凑合，再加1避免0
        let num = BitConverter.ToUInt32(md5.ComputeHash(str |> utf8.GetBytes), 0) % 100u |> int32
        num + 1

    override x.MessageHandler _ arg =
        let str = arg.Data.Message.ToString()
        match str.ToLowerInvariant() with
        | s when s.StartsWith("#c") ->
            let sw = new IO.StringWriter()
            sw.WriteLine("选项 1d100")
            let choices =
                s.Split(' ').[1..]
                |> Array.map (fun c ->
                    let str = sprintf "%s %s %i" c (getDateStr()) (arg.Data.UserId)
                    (c, strToPct(str)))
                |> Array.sortBy (fun (_, n) -> n)
            let sum = choices |> Array.sumBy (fun (_, n) -> n) |> float
            for (c,n) in choices do 
                sw.WriteLine("{0} {1}", c, n)
            x.QuickMessageReply(arg, sw.ToString())
        | s when s.StartsWith("#jrrp") -> 
            let date = getDateStr()
            let jrrp = strToPct(sprintf "%s%i" date (arg.Data.Sender.UserId))
            x.QuickMessageReply(arg, sprintf "%s今日人品值是%i" (arg.Data.Sender.NickName) jrrp)
        | _ -> ()