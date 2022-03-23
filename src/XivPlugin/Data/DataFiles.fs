namespace KPX.XivPlugin.Data

open System
open System.IO.Compression

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Host.Data


[<RequireQualifiedAccess>]
module ChinaDistroData =
    //from https://github.com/thewakingsands/ffxiv-datamining-cn

    [<Literal>]
    let private SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-cn-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-cn-master/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix>

    let private instance: WeakReference<TypedXivCollection> =
        WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

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
    //from https://github.com/MansonGit/ffxiv-datamining-jp

    [<Literal>]
    let SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-ja-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-jp-main/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix>

    let private instance: WeakReference<TypedXivCollection> =
        WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

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
