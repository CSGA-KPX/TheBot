namespace KPX.XivPlugin.Data

open System

open LibFFXIV.GameData
open LibFFXIV.GameData.Provided

open KPX.TheBot.Host.Data


[<RequireQualifiedAccess>]
module ChinaDistroData =

    [<Literal>]
    let private SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-cn-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-cn-master/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix, "", "cn">

    let private instance: WeakReference<TypedXivCollection> =
        WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

    let GetCollection () =
        let succ, col = instance.TryGetTarget()

        if not succ then
            let stream = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-cn-master.zip")
            let col = new TypedXivCollection(XivLanguage.None, stream, Prefix)
            instance.SetTarget(col)
            col
        else
            col

[<RequireQualifiedAccess>]
module OfficalDistroData =

    [<Literal>]
    let SampleFile = __SOURCE_DIRECTORY__ + "/../../../datafiles/ffxiv-datamining-ja-master.zip"

    [<Literal>]
    let private Prefix = "ffxiv-datamining-hexcode-ja-main/"

    type TypedXivCollection = XivCollectionProvider<SampleFile, "none", Prefix, "", "ja">

    let private instance: WeakReference<TypedXivCollection> =
        WeakReference<_>(Unchecked.defaultof<TypedXivCollection>)

    let GetCollection () =
        let succ, col = instance.TryGetTarget()

        if not succ then
            let stream = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-ja-master.zip")
            let col = new TypedXivCollection(XivLanguage.None, stream, Prefix)
            instance.SetTarget(col)
            col
        else
            col
