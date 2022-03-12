namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic


module ClassJobs =
    let BattleClassJobs, CraftGatherJobs =
        let col = OfficalDistroData.GetCollection()

        let nameMapping = Dictionary<string, string>()

        for row in col.ClassJob.TypedRows do
            let key = row.Abbreviation.AsString()
            let name = row.``Name{English}``.AsString()
            nameMapping.[key] <- name

        // 用已知的中文名称替换
        for row in ChinaDistroData.GetCollection().ClassJob.TypedRows do
            let key = row.Abbreviation.AsString()
            let name = row.Name.AsString()

            if not <| String.IsNullOrWhiteSpace(name) then
                nameMapping.[key] <- name

        let header = col.ClassJobCategory.Header.Headers
        // 35 能工巧匠 大地使者
        // 110 战斗精英 魔法导师 特职专用
        let battle =
            Seq.zip header (col.ClassJobCategory.GetItem(110).RawData)
            |> Seq.choose (fun (hdr, value) ->
                if value = "True" then
                    Some(nameMapping.[hdr.ColumnName])
                else
                    None)
            |> Seq.cache

        let cg =
            Seq.zip header (col.ClassJobCategory.GetItem(35).RawData)
            |> Seq.choose (fun (hdr, value) ->
                if value = "True" then
                    Some(nameMapping.[hdr.ColumnName])
                else
                    None)
            |> Seq.cache

        (battle, cg)
