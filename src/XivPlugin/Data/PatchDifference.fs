namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic

open KPX.TheBot.Host.Data

open Newtonsoft.Json.Linq


[<CLIMutable>]
type PatchInfo = { ID: int; Version: string }

// 使用量很大，内存缓存
type ItemPatchDifference private () =
    static let logger = NLog.LogManager.GetLogger("ItemPatchDifference")

    static let itemToPatchId, patchIdDefine =
        use archive =
            let s = EmbeddedResource.GetResFileStream("XivPlugin.ffxiv-datamining-patchdiff.zip")
            new IO.Compression.ZipArchive(s, IO.Compression.ZipArchiveMode.Read)

        let itemMap =

            use t = archive.GetEntry("Item.json").Open()
            let json = (new IO.StreamReader(t)).ReadToEnd()

            JObject.Parse(json).ToObject<Dictionary<int, int>>()

        let idMap =
            use t = archive.GetEntry("PatchVersion.json").Open()
            let json = (new IO.StreamReader(t)).ReadToEnd()

            JArray.Parse(json).ToObject<PatchInfo[]>()
            |> Seq.map (fun x -> x.ID, PatchNumber.FromString(x.Version))
            |> readOnlyDict

        itemMap, idMap

    static member ToPatchNumber(itemId: int) =
        let succ, patchId = itemToPatchId.TryGetValue(itemId)

        if succ then
            let succ, patch = patchIdDefine.TryGetValue(patchId)
            if succ then patch else
                logger.Warn($"PatchId {patchId} is used but not defined in PatchVersion.json")
                PatchNumber.Patch_Invalid
        else
            PatchNumber.Patch_Invalid
