module TheBot.Utils

open System
open System.Text.RegularExpressions
open System.Collections.Generic
open KPX.FsCqHttp.DataType


let cstOffset = TimeSpan.FromHours(8.0)

type SeedOption =
    | SeedDate
    | SeedRandom
    | SeedCustom of string

    member x.GetSeedString() =
        match x with
        | SeedDate -> DateTimeOffset.UtcNow.ToOffset(cstOffset).ToString("yyyyMMdd")
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


    /// Init using SeedOption.SeedRandom
    new() = Dicer(Array.singleton SeedOption.SeedRandom)


    member private x.GetHash() =
        refreshSeed()
        hash

    member val AutoRefreshSeed = true with get, set

    /// 返回Base64编码后的初始种子
    member x.InitialSeed = Convert.ToBase64String(initSeed)

    member x.GetRandomFromString(str : string, faceNum) =
        refreshSeed()
        let seed = Array.append (x.GetHash()) (utf8.GetBytes(str))
        let hash = md5.ComputeHash(seed)
        hashToDice (hash, faceNum)

    /// 获得一个随机数
    member x.GetRandom(faceNum) =
        refreshSeed()
        hashToDice (x.GetHash(), faceNum)

    /// 获得一组随机数，不重复
    member x.GetRandomArray(faceNum, count) =
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
           for i in x.GetRandomArray(faceNum, count) do
               yield srcArr.[i - 1] |]


type TextTable(cols : int) =
    let col = Array.init cols (fun _ -> List<string>())
    static let fullWidthSpace = '　'

    static let charLen (c) =
        // 007E是ASCII中最后一个可显示字符
        if c <= '~' then 1
        else 2

    static let strDispLen (str : string) =
        str.ToCharArray()
        |> Array.sumBy charLen

    static member FromHeader(header : Object []) =
        let x = TextTable(header.Length)
        x.AddRow(header)
        x

    member x.AddRow([<ParamArray>] fields : Object []) =
        if fields.Length <> col.Length then
            raise <| ArgumentException(sprintf "列数不一致 需求:%i, 提供:%i" col.Length fields.Length)
        fields
        |> Array.iteri (fun i o ->
            let str =
                match o with
                | :? string as str -> str
                | :? int32 as i -> System.String.Format("{0:N0}", i)
                | :? uint32 as i -> System.String.Format("{0:N0}", i)
                | :? float as f ->
                    let fmt =
                        if (f % 1.0) <> 0.0 then "{0:N2}"
                        else "{0:N0}"
                    System.String.Format(fmt, f)
                | _ -> o.ToString()
            col.[i].Add(str))

    override x.ToString() =
        let spacing = 1
        let sb = Text.StringBuilder("")
        if col.[0].Count <> 0 then
            let maxLens =
                col
                |> Array.map (fun l ->
                    l
                    |> Seq.map (strDispLen)
                    |> Seq.max)

            for i = 0 to col.[0].Count - 1 do
                for c = 0 to col.Length - 1 do
                    let str = col.[c].[i]
                    let len = maxLens.[c]
                    let pad = (maxLens.[c] - strDispLen (str)) / 2 + 1 + str.Length
                    sb.Append(str.PadRight(pad, fullWidthSpace)) |> ignore
                sb.AppendLine("") |> ignore
        sb.ToString()
