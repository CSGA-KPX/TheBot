namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data

open Newtonsoft.Json.Linq


// 使用量很大，内存缓存
type ItemPatchDifference private () =
    static let patchIdDefine =
        seq {
            let res = ResxManager("XivPlugin.XivStrings")

            for line in res.GetLinesWithoutComment("PatchId") do
                let s = line.Split(' ')
                int s.[0], PatchNumber(int s.[1])
        }
        |> readOnlyDict


    static let itemToPatchId =
        use archive =
            let s = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-patchdiff.zip")
            new IO.Compression.ZipArchive(s, IO.Compression.ZipArchiveMode.Read)

        use t = archive.GetEntry("Item.json").Open()
        let json = (new IO.StreamReader(t)).ReadToEnd()

        JObject
            .Parse(json)
            .ToObject<Dictionary<int, int>>()

    static member ToPatchNumber(itemId: int) =
        let succ, patchId = itemToPatchId.TryGetValue(itemId)

        if succ then
            patchIdDefine.[patchId]
        else
            PatchNumber.Patch_Invalid
