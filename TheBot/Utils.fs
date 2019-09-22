module Utils
open System
open KPX.FsCqHttp.DataType

let private CSTOffset = TimeSpan.FromHours(8.0)

type SeedOption = 
    | SeedDateTime
    | SeedDate
    | SeedUserId
    | SeedAtUserId
    | SeedGroupId
    | SeedRandom

    member x.GetSeedString(msg : Event.Message.MessageEvent) = 
        match x with
        | SeedDateTime -> DateTimeOffset.UtcNow.ToOffset(CSTOffset).ToString()
        | SeedDate     -> DateTimeOffset.UtcNow.ToOffset(CSTOffset).ToString("yyyyMMdd")
        | SeedUserId   -> msg.UserId.ToString()
        | SeedGroupId  -> if msg.GroupId = 0UL then "" else msg.GroupId.ToString()
        | SeedRandom   -> Guid.NewGuid().ToString()
        | SeedAtUserId ->
            let at = msg.Message.GetAts()
            if at.Length = 0 then
                raise <| InvalidOperationException("没有用户被At！")
            else
                at.[0].ToString()

    static member GetSeedString(a : SeedOption [], msg : Event.Message.MessageEvent) =
        a
        |> Array.fold (fun str x -> str + (x.GetSeedString(msg))) ""

    static member SeedByUserDay = 
        [|
            SeedDate
            SeedUserId
        |]

    static member SeedByAtUserDay = 
        [|
            SeedDate
            SeedAtUserId
        |]
type Dicer (seed : SeedOption [], msg : Event.Message.MessageEvent) as x =
    let utf8 = Text.Encoding.UTF8
    let mutable hash = utf8.GetBytes(SeedOption.GetSeedString(seed, msg))
    let md5  = System.Security.Cryptography.MD5.Create()
    
    let refreshSeed () = 
        if x.AutoRefreshSeed then
            hash <- md5.ComputeHash(hash)

    let hashToDice (hash, faceNum) = 
        let num = BitConverter.ToUInt32(hash, 0) % faceNum |> int32
        num + 1

    member private x.GetHash() = refreshSeed(); hash

    member val AutoRefreshSeed = true with get, set

    member x.GetRandomFromString(str : string, faceNum) = 
        refreshSeed()
        let seed = Array.append (x.GetHash()) (utf8.GetBytes(str))
        let hash = md5.ComputeHash(seed)
        hashToDice(hash, faceNum)

    /// 获得一个随机数
    member x.GetRandom(faceNum) = 
        refreshSeed()
        hashToDice(x.GetHash(), faceNum)

    /// 获得一组随机数，不重复
    member x.GetRandomArray(faceNum, count) =
        let tmp = new Collections.Generic.HashSet<int>()
        while tmp.Count <> count do
            tmp.Add(x.GetRandom(faceNum)) |> ignore
        let ret= Array.zeroCreate<int> tmp.Count
        tmp.CopyTo(ret)
        ret
    
    /// 从数组获得随机项
    member x.GetRandomItem(srcArr : 'T []) = 
        let idx = x.GetRandom(srcArr.Length |> uint32) - 1
        srcArr.[idx]

    /// 从数组获得一组随机项，不重复
    member x.GetRandomItems(srcArr : 'T [], count) = 
        [|
            let faceNum = srcArr.Length |> uint32
            for i in x.GetRandomArray(faceNum, count) do 
                yield srcArr.[i-1]
        |]