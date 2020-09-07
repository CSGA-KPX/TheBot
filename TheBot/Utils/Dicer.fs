module TheBot.Utils.Dicer

open System
open KPX.FsCqHttp.DataType

let private cstOffset = TimeSpan.FromHours(8.0)
let GetCstTime() = DateTimeOffset.UtcNow.ToOffset(cstOffset)

type SeedOption =
    | SeedDate
    | SeedRandom
    | SeedCustom of string

    member x.GetSeedString() =
        match x with
        | SeedDate -> GetCstTime().ToString("yyyyMMdd")
        | SeedRandom -> Guid.NewGuid().ToString()
        | SeedCustom s -> s

    static member GetSeedString(a : SeedOption []) = a |> Array.fold (fun str x -> str + (x.GetSeedString())) ""

    static member SeedByUserDay(msg : Event.Message.MessageEvent) =
        [| SeedDate
           SeedCustom(msg.UserId.ToString()) |]

    static member SeedByAtUserDay(msg : Event.Message.MessageEvent) =
        [| SeedDate
           SeedCustom
               (let at = msg.Message.GetAts()
                if at.Length = 0 then raise <| InvalidOperationException("没有用户被At！")
                else at.[0].ToString()) |]

type Dicer(initSeed : byte []) as x =
    static let utf8 = Text.Encoding.UTF8
    static let md5 = System.Security.Cryptography.MD5.Create()

    let mutable hash = initSeed

    let refreshSeed() =
        if x.AutoRefreshSeed then hash <- md5.ComputeHash(hash)


    let hashToDice (hash, faceNum) =
        let num = BitConverter.ToUInt32(hash, 0) % faceNum |> int32
        num + 1


    new(seed : SeedOption []) =
        let initSeed =
            seed
            |> SeedOption.GetSeedString
            |> utf8.GetBytes
            |> md5.ComputeHash
        Dicer(initSeed)

    new(seed : SeedOption) = Dicer(Array.singleton seed)


    member private x.GetHash() =
        refreshSeed()
        hash

    member val AutoRefreshSeed = true with get, set

    /// 返回Base64编码后的初始种子
    member x.InitialSeed = Convert.ToBase64String(initSeed)

    member x.GetRandom(faceNum, str : string) = 
        refreshSeed()
        let seed = Array.append (x.GetHash()) (utf8.GetBytes(str))
        let hash = md5.ComputeHash(seed)
        hashToDice (hash, faceNum)

    /// 获得一个随机数
    member x.GetRandom(faceNum) =
        refreshSeed()
        hashToDice (x.GetHash(), faceNum)

    /// 获得一组随机数
    member x.GetRandomArray(faceNum, count) =
        Array.init count (fun _ -> x.GetRandom(faceNum))

    /// 获得一组随机数，不重复
    member x.GetRandomArrayUnique(faceNum, count) =
        let tmp = Collections.Generic.HashSet<int>()
        while tmp.Count <> count do
            tmp.Add(x.GetRandom(faceNum)) |> ignore
        let ret = Array.zeroCreate<int> tmp.Count
        tmp.CopyTo(ret)
        ret

    /// 从数组获得随机项
    member x.GetRandomItem(srcArr : 'T []) =
        let idx = x.GetRandom(srcArr.Length |> uint32) - 1
        srcArr.[idx]

    /// 从数组获得一组随机项，不重复
    member x.GetRandomItems(srcArr : 'T [], count) =
        [| let faceNum = srcArr.Length |> uint32
           for i in x.GetRandomArrayUnique(faceNum, count) do
               yield srcArr.[i - 1] |]