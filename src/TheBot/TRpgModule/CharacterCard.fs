namespace KPX.TheBot.Module.TRpgModule.TRpgCharacterCard

open System.Collections.Generic

open KPX.FsCqHttp
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.TRpgModule.Coc7


[<CLIMutable>]
type CharacterCard =
    { Id : int64
      UserId : uint64
      ChrName : string
      Props : Dictionary<string, int> }

    static member DefaultOf(uid : UserId) =
        { Id = 0L
          UserId = uid.Value
          // 保证默认名称不会重复
          ChrName = System.Guid.NewGuid().ToString("N")
          Props = Dictionary<_, _>() }

    member x.PropExists(pn) =
        x.Props.ContainsKey(MapCoc7SkillName(pn))

    member x.Item
        with get pn = x.Props.[MapCoc7SkillName(pn)]
        and set pn v = x.Props.[MapCoc7SkillName(pn)] <- v

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
        tt.AddPreTable $"所有者:%i{x.UserId}"
        tt.AddPreTable $"角色名:%s{x.ChrName}"

        for row in Seq.chunkBySize colCount x.Props do
            tt.AddRowFill(
                [| for item in row do
                       yield box <| item.Key
                       yield box <| item.Value |]
            )

        tt
