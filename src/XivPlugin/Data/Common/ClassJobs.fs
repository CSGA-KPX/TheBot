namespace KPX.XivPlugin.Data

open System
open System.Collections.Generic


module ClassJobs =
    let BattleClassJobs =
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
        let instanceJobs = col.ClassJobCategory.GetItem(110)

        Seq.zip header instanceJobs.RawData
        |> Seq.choose (fun (hdr, value) ->
            if value = "True" then
                Some(nameMapping.[hdr.ColumnName])
            else
                None)
        |> Seq.cache
