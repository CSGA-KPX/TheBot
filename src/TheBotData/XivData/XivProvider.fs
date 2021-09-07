module KPX.TheBot.Data.XivData.XivProvider

open System.IO
open System.IO.Compression

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Data.Common.Resource


type TypedXivCollection = XivCollectionProvider<XivTPSample, "none", "ffxiv-datamining-cn-master/">

let mutable private xivArchive : ZipArchive option = None

let mutable private xivCollection : TypedXivCollection option = None

let UpdateXivCollection () =
    if xivArchive.IsSome then xivArchive.Value.Dispose()

    let archivePath =
        Path.Combine(StaticDataPath, "ffxiv-datamining-cn-master.zip")

    xivArchive <-
        Some
        <| new ZipArchive(File.OpenRead(archivePath), ZipArchiveMode.Read)

    xivCollection <-
        Some
        <| new TypedXivCollection(XivLanguage.None, xivArchive.Value, "ffxiv-datamining-cn-master/")

/// 全局的中文FF14数据库
let XivCollectionChs =
    if xivCollection.IsNone then UpdateXivCollection()
    xivCollection.Value
