namespace KPX.TheBot.Host.Utils.Dicer

open System
open System.Collections.Generic

open KPX.FsCqHttp
open KPX.FsCqHttp.Event
open KPX.FsCqHttp.Message


type DiceSeed =
    { Date: DateTimeOffset
      UserId: UserId
      Strings: ResizeArray<string> }

    member internal x.GetBytes() =
        use ms = new IO.MemoryStream()
        use sw = new IO.StreamWriter(ms, AutoFlush = true)

        sw.Write(x.Date.ToOffset(TimeSpan.FromHours(8.0)).ToString("yyyyMMdd"))
        sw.Write(x.UserId.Value)

        for item in x.Strings do
            sw.Write(item)

        ms.ToArray()

    static member SeedByUserDay(msg: MessageEvent) =
        { Date = DateTimeOffset.Now
          UserId = msg.UserId
          Strings = ResizeArray<_>() }

    static member SeedByAtUserDay(msg: MessageEvent) =
        let user =
            match msg.Message.TryGetAt() with
            | None -> raise <| InvalidOperationException("没有用户被At！")
            | Some AtUserType.All -> raise <| InvalidOperationException("At全员无效！")
            | Some(AtUserType.User uid) -> uid

        { Date = DateTimeOffset.Now
          UserId = user
          Strings = ResizeArray<_>() }

    static member SeedByRandom() =
        let strs = ResizeArray<string>()
        strs.Add(Guid.NewGuid().ToString())

        { Date = DateTimeOffset.Now
          UserId = UserId.Zero
          Strings = strs }

type private DRng(seed: DiceSeed) =
    static let utf8 = Text.Encoding.UTF8

    // 因为数据量很小，Md5和xxHash速度都差不多
    // 而且DieHarder和rngtest测试结果也差不多
    // 没有其他问题还是固定用Md5了
    let hash = Security.Cryptography.MD5.Create() :> Security.Cryptography.HashAlgorithm

    let mutable frozen = false

    let mutable seed = seed.GetBytes() |> hash.ComputeHash

    let iterate () =
        if not frozen then
            seed <- hash.ComputeHash(seed)

    /// 指示该DRng是否继续衍生
    member x.Freeze() = frozen <- true

    member x.IsFrozen = frozen

    member x.GetInt32() = BitConverter.ToInt32(x.GetBytes(), 0)
    member x.GetUInt32() = BitConverter.ToUInt32(x.GetBytes(), 0)
    member x.GetInt64() = BitConverter.ToInt64(x.GetBytes(), 0)
    member x.GetUInt64() = BitConverter.ToUInt64(x.GetBytes(), 0)

    member x.GetInt32(str) =
        BitConverter.ToInt32(x.GetBytes(str), 0)

    member x.GetUInt32(str) =
        BitConverter.ToUInt32(x.GetBytes(str), 0)

    member x.GetInt64(str) =
        BitConverter.ToInt64(x.GetBytes(str), 0)

    member x.GetUInt64(str) =
        BitConverter.ToUInt64(x.GetBytes(str), 0)

    member private x.GetBytes() =
        if frozen then
            seed
        else
            iterate ()
            seed

    member private x.GetBytes(str: string) =
        if frozen then
            Array.append seed (utf8.GetBytes(str)) |> hash.ComputeHash
        else
            iterate ()

            Array.append seed (utf8.GetBytes(str)) |> hash.ComputeHash

type Dicer(seed: DiceSeed) =
    let drng = DRng(seed)

    /// 通用的随机骰子
    static member RandomDicer = Dicer(DiceSeed.SeedByRandom())

    member x.Freeze() = drng.Freeze()

    member x.IsFrozen = drng.IsFrozen

    member x.GetInteger(min: uint32, max: uint32) =
        if (max - min) = UInt32.MaxValue then
            drng.GetUInt32()
        else
            drng.GetUInt32() % (max - min + 1u) + min

    member x.GetInteger(min: uint64, max: uint64) =
        if (max - min) = UInt64.MaxValue then
            drng.GetUInt64()
        else
            drng.GetUInt64() % (max - min + 1UL) + min

    member x.GetInteger(min: int, max: int) =
        let max = max - min
        (x.GetInteger(0u, uint32 max) |> int) + min

    member x.GetInteger(min: int64, max: int64) =
        let max = max - min
        (x.GetInteger(0UL, uint64 max) |> int64) + min

    member x.GetInteger(min: uint32, max: uint32, str: string) =
        if (max - min) = UInt32.MaxValue then
            drng.GetUInt32(str)
        else
            drng.GetUInt32(str) % (max - min + 1u) + min

    member x.GetInteger(min: uint64, max: uint64, str: string) =
        if (max - min) = UInt64.MaxValue then
            drng.GetUInt64(str)
        else
            drng.GetUInt64(str) % (max - min + 1UL) + min

    member x.GetInteger(min: int, max: int, str: string) =
        let max = max - min
        (x.GetInteger(0u, uint32 max, str) |> int) + min

    member x.GetInteger(min: int64, max: int64, str: string) =
        let max = max - min

        (x.GetInteger(0UL, uint64 max, str) |> int64) + min

    member x.GetNatural(upper) = x.GetInteger(0, upper)
    member x.GetNatural(upper) = x.GetInteger(0L, upper)
    member x.GetNatural(upper) = x.GetInteger(0u, upper)
    member x.GetNatural(upper) = x.GetInteger(0UL, upper)

    member x.GetPositive(upper) = x.GetInteger(1, upper)
    member x.GetPositive(upper) = x.GetInteger(1L, upper)
    member x.GetPositive(upper) = x.GetInteger(1u, upper)
    member x.GetPositive(upper) = x.GetInteger(1UL, upper)

    member x.GetNatural(upper, str) = x.GetInteger(0, upper, str)
    member x.GetNatural(upper, str) = x.GetInteger(0L, upper, str)
    member x.GetNatural(upper, str) = x.GetInteger(0u, upper, str)
    member x.GetNatural(upper, str) = x.GetInteger(0UL, upper, str)

    member x.GetPositive(upper, str) = x.GetInteger(1, upper, str)
    member x.GetPositive(upper, str) = x.GetInteger(1L, upper, str)
    member x.GetPositive(upper, str) = x.GetInteger(1u, upper, str)
    member x.GetPositive(upper, str) = x.GetInteger(1UL, upper, str)

    member x.GetIntegerArray(lower: int, upper: int, count: int, unique: bool) =
        if count < 0 then
            invalidArg "count" "数量不能小于0"

        if unique && (upper - lower + 1) < count then
            invalidArg "count" "获取唯一数大于可能数"

        let ret =
            if unique then
                HashSet<int>(count) :> ICollection<_>
            else
                ResizeArray<int>(count) :> ICollection<_>

        while ret.Count <> count do
            ret.Add(x.GetInteger(lower, upper))

        let r = Array.zeroCreate<int> count
        ret.CopyTo(r, 0)
        r

    member x.GetNaturalArray(upper: int, count: int, ?unique: bool) =
        x.GetIntegerArray(0, upper, count, defaultArg unique false)

    member x.GetPositiveArray(upper: int, count: int, ?unique: bool) =
        x.GetIntegerArray(1, upper, count, defaultArg unique false)

    member x.GetArrayItem(items: 'T[]) =
        let idx = x.GetNatural(items.Length - 1)
        items.[idx]

    member x.GetArrayItem(items: 'T[], count, ?unique: bool) =
        x.GetNaturalArray(items.Length - 1, count, defaultArg unique false)
        |> Array.map (fun idx -> items.[idx])
