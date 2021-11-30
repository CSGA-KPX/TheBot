module KPX.XivPlugin.Data.XivProvider

open System.IO.Compression

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Host.Data


[<Literal>]
let XivTPSample = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-cn-master.zip"

type TypedXivCollection = XivCollectionProvider<XivTPSample, "none", "ffxiv-datamining-cn-master/">

let mutable private xivCollection: TypedXivCollection option = None

// TODO: 也许我们需要WeakReference?
let UpdateXivCollection () =
    let stream = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-cn-master.zip")
    let archive = new ZipArchive(stream, ZipArchiveMode.Read)

    xivCollection <-
        Some
        <| new TypedXivCollection(XivLanguage.None, archive, "ffxiv-datamining-cn-master/")

/// 全局的中文FF14数据库
let XivCollectionChs =
    if xivCollection.IsNone then
        UpdateXivCollection()

    xivCollection.Value
