namespace KPX.TheBot.Utils.Dicer

open System
open System.Collections.Generic

open KPX.FsCqHttp.Event


type SeedOption =
    | SeedDate
    | SeedRandom
    | SeedCustom of string

    override x.ToString() =
        match x with
        | SeedDate ->
            DateTimeOffset
                .Now
                .ToOffset(TimeSpan.FromHours(8.0))
                .ToString("yyyyMMdd")
        | SeedRandom -> Guid.NewGuid().ToString()
        | SeedCustom s -> s

    static member GetSeedString(seeds : seq<SeedOption>) = String.Join("|", seeds)

    static member SeedByUserDay(msg : MessageEvent) =
        [| SeedDate
           SeedCustom(msg.UserId.ToString()) |]

    static member SeedByAtUserDay(msg : MessageEvent) =
        [| SeedDate
           SeedCustom(
               let at = msg.Message.TryGetAt()

               if at.IsNone then
                   raise <| InvalidOperationException("没有用户被At！")
               else
                   at.Value.ToString()
           ) |]

type private DRng(seeds : seq<SeedOption>) =
    static let utf8 = Text.Encoding.UTF8

    let hash =
        Security.Cryptography.MD5.Create() :> Security.Cryptography.HashAlgorithm

    let mutable frozen = false

    let mutable seed =
        SeedOption.GetSeedString(seeds)
        |> utf8.GetBytes
        |> hash.ComputeHash

    let iterate () =
        if not frozen then seed <- hash.ComputeHash(seed)

    /// 指示该DRng是否继续衍生
    member x.Freeze() = frozen <- true

    member x.IsFrozen = frozen

    member x.GetInt32() = BitConverter.ToInt32(x.GetBytes(), 0)
    member x.GetUInt32() = BitConverter.ToUInt32(x.GetBytes(), 0)
    member x.GetInt64() = BitConverter.ToInt64(x.GetBytes(), 0)
    member x.GetUInt64() = BitConverter.ToUInt64(x.GetBytes(), 0)

    member x.GetInt32(str) = BitConverter.ToInt32(x.GetBytes(str), 0)
    member x.GetUInt32(str) = BitConverter.ToUInt32(x.GetBytes(str), 0)
    member x.GetInt64(str) = BitConverter.ToInt64(x.GetBytes(str), 0)
    member x.GetUInt64(str) = BitConverter.ToUInt64(x.GetBytes(str), 0)

    member private x.GetBytes() = 
        if frozen then
            seed
        else
            iterate()
            seed

    member private x.GetBytes(str : string) =
        if frozen then
            Array.append seed (utf8.GetBytes(str))
            |> hash.ComputeHash
        else
            iterate()
            Array.append seed (utf8.GetBytes(str))
            |> hash.ComputeHash

type Dicer(seeds : seq<SeedOption>) =
    let drng = DRng(seeds)

    new(opt : SeedOption) = Dicer(Seq.singleton opt)

    /// 通用的随机骰子
    static member val RandomDicer = Dicer(SeedOption.SeedRandom)

    member x.Freeze() = drng.Freeze()

    member x.IsFrozen = drng.IsFrozen

    member x.GetInteger(min : uint32, max : uint32) =
        drng.GetUInt32() % (max - min + 1u) + min

    member x.GetInteger(min : uint64, max : uint64) =
        drng.GetUInt64() % (max - min + 1UL) + min

    member x.GetInteger(min : int, max : int) =
        let max = max - min
        (x.GetInteger(0u, uint32 max) |> int) + min

    member x.GetInteger(min : int64, max : int64) =
        let max = max - min
        (x.GetInteger(0UL, uint64 max) |> int64) + min

    member x.GetInteger(min : uint32, max : uint32, str : string) =
        drng.GetUInt32(str) % (max - min + 1u) + min

    member x.GetInteger(min : uint64, max : uint64, str : string) =
        drng.GetUInt64(str) % (max - min + 1UL) + min

    member x.GetInteger(min : int, max : int, str : string) =
        let max = max - min
        (x.GetInteger(0u, uint32 max, str) |> int) + min

    member x.GetInteger(min : int64, max : int64, str : string) =
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

    member x.GetIntegerArray(lower : int, upper : int, count : int, unique : bool) =
        if count < 0 then invalidArg "count" "数量不能小于0"
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

    member x.GetNaturalArray(upper : int, count : int, ?unique : bool) = 
        x.GetIntegerArray(0, upper, count, defaultArg unique false)

    member x.GetPositiveArray(upper : int, count : int, ?unique : bool) = 
        x.GetIntegerArray(1, upper, count, defaultArg unique false)

    member x.GetArrayItem(items : 'T []) = 
        let idx = x.GetNatural(items.Length - 1)
        items.[idx]

    member x.GetArrayItem(items : 'T [], count, ?unique : bool) = 
        x.GetNaturalArray(items.Length - 1, count, defaultArg unique false)
        |> Array.map (fun idx -> items.[idx])