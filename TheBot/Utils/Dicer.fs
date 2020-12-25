module KPX.TheBot.Utils.Dicer

open System
open KPX.FsCqHttp.Event

let private cstOffset = TimeSpan.FromHours(8.0)

let GetCstTime () =
    DateTimeOffset.UtcNow.ToOffset(cstOffset)

type SeedOption =
    | SeedDate
    | SeedRandom
    | SeedCustom of string

    member x.GetSeedString() =
        match x with
        | SeedDate -> GetCstTime().ToString("yyyyMMdd")
        | SeedRandom -> Guid.NewGuid().ToString()
        | SeedCustom s -> s

    static member GetSeedString(a : SeedOption []) =
        a
        |> Array.fold (fun str x -> str + (x.GetSeedString())) ""

    static member SeedByUserDay(msg : MessageEvent) =
        [| SeedDate
           SeedCustom(msg.UserId.ToString()) |]

    static member SeedByAtUserDay(msg : MessageEvent) =
        [| SeedDate
           SeedCustom(
               let at = msg.Message.TryGetAt()
               if at.IsNone then raise <| InvalidOperationException("没有用户被At！") else at.Value.ToString()
           ) |]

[<AbstractClass>]
/// 确定性随机数生成器
type private DRng(seed : byte []) =
    static let utf8 = Text.Encoding.UTF8

    let hash =
        Security.Cryptography.MD5.Create() :> Security.Cryptography.HashAlgorithm

    /// 用于计算hash的算法
    /// 此类型不能跨线程使用
    member x.HashAlgorithm = hash

    /// 获取随机字节，不小于8字节
    abstract GetBytes : unit -> byte []

    /// 初始种子值
    member x.InitialSeed = seed

    member x.NextInt32() = BitConverter.ToInt32(x.GetBytes(), 0)
    member x.NextUInt32() = BitConverter.ToUInt32(x.GetBytes(), 0)

    member x.NextInt64() = BitConverter.ToInt64(x.GetBytes(), 0)
    member x.NextUInt64() = BitConverter.ToUInt64(x.GetBytes(), 0)

    member x.StringToUInt32(str : string) =
        let hash =
            Array.append (x.GetBytes()) (utf8.GetBytes(str))
            |> x.HashAlgorithm.ComputeHash

        BitConverter.ToUInt32(hash, 0)

type private ConstantDRng(seed) =
    inherit DRng(seed)

    override x.GetBytes() = x.InitialSeed

type private HashBasedDRng(seed) =
    inherit DRng(seed)

    let mutable hash = seed

    let sync = obj ()

    override x.GetBytes() =
        lock
            sync
            (fun () ->
                hash <- x.HashAlgorithm.ComputeHash(hash)
                hash)


type Dicer private (rng : DRng) =
    static let utf8 = Text.Encoding.UTF8
    static let randomDicer = new Dicer(SeedRandom)

    new(seed : SeedOption []) =
        let initSeed =
            seed
            |> SeedOption.GetSeedString
            |> utf8.GetBytes
            |> HashBasedDRng

        Dicer(initSeed)

    new(seed : SeedOption) = Dicer(Array.singleton seed)

    /// 通用的随机骰子
    static member RandomDicer = randomDicer

    /// 生成一个新的骰子，其种子值不变
    member x.Freeze() = rng.GetBytes() |> ConstantDRng |> Dicer

    /// 该Dicer是否返回恒定值(ConstantDRng)
    member x.IsFreezed = rng :? ConstantDRng

    /// 获得一个[1, faceNum]内的随机数
    member x.GetRandom(faceNum : uint32) = rng.NextUInt32() % faceNum + 1u |> int32

    /// 将字符串str转换为[1, faceNum]内的随机数
    member x.GetRandom(faceNum : uint32, str : string) =
        let ret = rng.StringToUInt32(str)
        ret % faceNum + 1u |> int32

    /// 获得一组[1, faceNum]内的随机数，可能重复
    member x.GetRandomArray(faceNum, count) =
        Array.init count (fun _ -> x.GetRandom(faceNum))

    /// 获得一组[1, faceNum]内的随机数，不重复
    member x.GetRandomArrayUnique(faceNum, count) =
        let tmp = Collections.Generic.HashSet<int>()

        if count > (int faceNum) then raise <| ArgumentOutOfRangeException("不应超过可能数")

        while tmp.Count <> count do
            tmp.Add(x.GetRandom(faceNum)) |> ignore

        let ret = Array.zeroCreate<int> tmp.Count
        tmp.CopyTo(ret)
        ret

    /// 根据索引从数组获得随机项
    member x.GetRandomItem(srcArr : 'T []) =
        let idx = x.GetRandom(srcArr.Length |> uint32) - 1
        srcArr.[idx]

    /// 根据索引从数组获得随机项，不重复
    member x.GetRandomItems(srcArr : 'T [], count) =
        [| let faceNum = srcArr.Length |> uint32

           for i in x.GetRandomArrayUnique(faceNum, count) do
               yield srcArr.[i - 1] |]
