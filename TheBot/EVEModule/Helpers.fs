module TheBot.Module.EveModule.Utils.Helpers

open System
open System.Net.Http

[<Literal>]
let EveSellTax = 6

[<Literal>]
let EveBuyTax = 4

let hc = 
    let hc = new HttpClient()
    hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot") |> ignore
    hc


/// EVE小数进位
///
/// d 输入数字
///
/// n 保留有效位数
let RoundFloat (d : float) (n : int) = 
    if d = 0.0 then 0.0
    else
        let scale = 10.0 ** ((d |> abs |> log10 |> floor) + 1.0)
        scale * Math.Round(d / scale, n)

let HumanReadableFloat (d : float) = 
    if d = 0.0 then "--"
    else
        let s = 10.0 ** ((d |> abs |> log10 |> floor) + 1.0)
        let l = log10 s |> floor |> int
        let (scale, postfix) = 
            if l >= 9 then
                8.0, "亿"
            else
                0.0, ""
            (*
            match l |> int with
            | 0 | 1 | 2 | 3 | 4
                 ->  0.0, ""
            | 5  ->  4.0, "万"
            | 6  ->  5.0, "十万"
            | 7  ->  6.0, "百万"
            | 8  ->  7.0, "千万"
            | _  ->  8.0, "亿"*)

        String.Format("{0:N2}{1}", d / 10.0 ** scale, postfix)

[<AbstractClass>]
type CachedCollection<'Key, 'Value>() as x = 
    let cache = Collections.Concurrent.ConcurrentDictionary<'Key, 'Value>()
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)

    /// 获取一个值，不通过缓存
    abstract FetchItem : 'Key -> 'Value
    abstract GetKey    : 'Value -> 'Key
    abstract IsExpired : 'Value -> bool

    member internal x.Logger = logger

    member x.Clear() = cache.Clear()

    /// 获取一个值，不通过缓存，然后写入缓存
    member x.Force(key : 'Key) = 
        let oldVal = cache.GetOrAdd(key, fun key -> x.FetchItem(key))
        let newVal = x.FetchItem(key)
        cache.TryUpdate(key, newVal, oldVal) |> ignore
        newVal

    member x.Item(key : 'Key) =
        let item = cache.GetOrAdd(key, fun key -> x.FetchItem(key))
        if x.IsExpired(item) then
            let newVal = x.FetchItem(key)
            let succ = cache.TryUpdate(key, newVal, item)
            x.Logger.Info(sprintf "updated : %A" succ)
            if succ then newVal else item
         else
            item