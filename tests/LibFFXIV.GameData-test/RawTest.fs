namespace LibFFXIV.GameData.Testing.Raw

open System
open System.IO
open System.IO.Compression

open LibFFXIV.GameData
open LibFFXIV.GameData.Raw

open NUnit.Framework
open FsUnit
open FsUnitTyped
open LibFFXIV.GameData.Testing.TestResource


[<TestFixture>]
type ItemCollectionText() =
    let file = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read)

    let a = new ZipArchive(file, ZipArchiveMode.Read)

    let col = new ZippedXivCollection(XivLanguage.None, a, archivePrefix)

    interface IDisposable with
        member x.Dispose() =
            //(col :> IDisposable).Dispose()
            a.Dispose()
            file.Dispose()

    [<Test>]
    member x.TestSeqRead() =
        for item in col.GetSheet("Item") do
            ()

    [<Test>]
    member x.ItemHeader() =
        let gil = col.GetSheet("Item").[1]
        gil.As<string>("Adjective") |> should equal "0"

        gil.As<string>("IsCollectable") |> should equal "False"

        gil.As<string>("IsGlamourous") |> should equal "False"

    [<Test>]
    member x.SelectedSheet() =
        let gil = col.GetSheet("Item").[1]

        gil.As<string>("Adjective") |> should equal "0"

        gil.As<string>("IsCollectable") |> should equal "False"

        gil.As<string>("IsGlamourous") |> should equal "False"

    [<Test>]
    member x.TypeConvert() =
        let gil = col.GetSheet("Item").[1]

        gil.As<string>("IsCollectable") |> should equal "False"

        gil.As<bool>("IsCollectable") |> should equal false

        gil.As<byte>("Rarity") |> should equal 1uy
        gil.As<sbyte>("Rarity") |> should equal 1y
        gil.As<int16>("Rarity") |> should equal 1s
        gil.As<uint16>("Rarity") |> should equal 1us
        gil.As<int32>("Rarity") |> should equal 1
        gil.As<uint32>("Rarity") |> should equal 1u
        gil.As<int64>("Rarity") |> should equal 1L
        gil.As<uint64>("Rarity") |> should equal 1UL

        gil.AsRow("ItemUICategory").Key.Main |> should equal 63

        gil.As<int16>("Rarity") |> should equal 1s
        gil.As<uint16>("Rarity") |> should equal 1us
        gil.As<int32>("Rarity") |> should equal 1
        gil.As<uint32>("Rarity") |> should equal 1u
        gil.As<int64>("Rarity") |> should equal 1L
        gil.As<uint64>("Rarity") |> should equal 1UL

        gil.AsRow("ItemUICategory").Key.Main |> should equal 63
