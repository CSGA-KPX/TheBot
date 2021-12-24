namespace KPX.XivPlugin.Data

open System
open System.IO.Compression

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Host.Data

open LiteDB


[<RequireQualifiedAccess>]
/// <summary>
/// 版本区
///
/// 根据地区标记版本不同的地区。
///</summary>
type VersionRegion =
    | China
    | Offical

    override x.ToString() =
        match x with
        | China -> "china"
        | Offical -> "offical"

    member x.BsonValue = BsonValue(x.ToString())

type VersionRegionInstructions() =
    inherit KPX.TheBot.Host.PluginPrerunInstruction()

    override x.RunInstructions() =
        let fromBsonValue (value: BsonValue) =
            match value.AsString with
            | "china" -> VersionRegion.China
            | "offical" -> VersionRegion.Offical
            | str -> invalidArg (nameof value) $"Version值不合法：当前为{str}"

        BsonMapper.Global.RegisterType<VersionRegion>((fun x -> x.BsonValue), fromBsonValue)
        |> ignore

[<RequireQualifiedAccess>]
module ChinaDistroData =
    //from https://github.com/thewakingsands/ffxiv-datamining-cn

    [<Literal>]
    let private SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-cn-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-cn-master/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix>

    let private instance: WeakReference<TypedXivCollection> = WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

    let GetCollection () =
        let succ, col = instance.TryGetTarget()

        if not succ then
            let stream = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-cn-master.zip")
            let archive = new ZipArchive(stream, ZipArchiveMode.Read)
            let col = new TypedXivCollection(XivLanguage.None, archive, Prefix)
            instance.SetTarget(col)
            col
        else
            col

[<RequireQualifiedAccess>]
module OfficalDistroData =
    //from https://github.com/xivapi/ffxiv-datamining

    [<Literal>]
    let SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-ja-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-jp-master/csv/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix>

    let private instance: WeakReference<TypedXivCollection> = WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

    let GetCollection () =
        let succ, col = instance.TryGetTarget()

        if not succ then
            let stream = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-ja-master.zip")
            let archive = new ZipArchive(stream, ZipArchiveMode.Read)
            let col = new TypedXivCollection(XivLanguage.None, archive, Prefix)
            instance.SetTarget(col)
            col
        else
            col
