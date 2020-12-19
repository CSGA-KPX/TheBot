module TheBot.Module.TRpgModule.TRpgCharacterCard

open System
open System.Collections.Generic

open KPX.FsCqHttp.Utils.TextTable

open TheBot.Module.TRpgModule.TRpgUtils

[<CLIMutable>]
type CharacterCard =
    { Id : int64
      UserId : uint64
      ChrName : string
      Props : Dictionary<string, int> }

    member x.Item
        with get pn = x.Props.[pn]
        and set pn v = x.Props.[pn] <- v

    override x.ToString() = x.ToTextTable().ToString()

    member x.ToTextTable() =
        let colCount = 3

        let hdrItem =
            [| LeftAlignCell "属性" |> box
               RightAlignCell "值" |> box |]

        let hdr =
            [| for _ = 0 to colCount - 1 do
                yield! hdrItem |]

        let tt = TextTable(hdr)
        tt.AddPreTable(sprintf "所有者:%i" x.UserId)
        tt.AddPreTable(sprintf "角色名:%s" x.ChrName)

        for row in Seq.chunkBySize colCount x.Props do
            tt.AddRowFill(
                [| for item in row do
                    yield box <| item.Key
                    yield box <| item.Value |]
            )

        tt

module Coc7 =
    /// Coc7中技能别名
    let SkillNameAlias =
        StringData.GetLines(StringData.Key_SkillAlias)
        |> Array.map
            (fun x ->
                let t = x.Split("|")
                let strFrom = t.[0]
                let strTo = t.[1]
                strFrom, strTo)
        |> readOnlyDict

    /// Coc7中技能及默认值
    let DefaultSkillValues =
        StringData.GetLines(StringData.Key_DefaultSkillValues)
        |> Array.map
            (fun x ->
                let t = x.Split("|")
                t.[0], (t.[1] |> int))
        |> readOnlyDict

    /// 将输入转换为Coc7技能名，处理别名等
    let MapCoc7SkillName (name : string) =
        if SkillNameAlias.ContainsKey(name) then SkillNameAlias.[name] else name
