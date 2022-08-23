namespace KPX.DicePlugin.TRpgModule.TRpgCharacterCard

open System.Collections.Generic

open KPX.FsCqHttp
open KPX.FsCqHttp.Utils.TextResponse

open KPX.DicePlugin.TRpgModule.Coc7


[<CLIMutable>]
type CharacterCard =
    { Id: int64
      UserId: uint64
      ChrName: string
      Props: Dictionary<string, int> }

    static member DefaultOf(uid: UserId) =
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
        TextTable() {
            //forceImage

            let colCount = 3

            $"所有者:%i{x.UserId}"
            $"角色名:%s{x.ChrName}"

            AsCols [ for _ = 0 to colCount - 1 do
                         Literal "属性"
                         Literal "值" { rightAlign } ]

            let ordered =
                seq {
                    // 复制字典
                    let clone = Dictionary<_, _>(x.Props)
                    // 基础和衍生属性
                    for key in Coc7AttrDisplayOrder do
                        if clone.ContainsKey(key) then
                            let value = clone.[key]
                            yield key, value
                            clone.Remove(key) |> ignore
                    // 技能, 如果不是默认值就不管了
                    for kv in clone do
                        let key = kv.Key
                        let value = kv.Value

                        if DefaultSkillValues.ContainsKey(key) then
                            if DefaultSkillValues.[key] <> value then
                                yield key, value
                        else
                            // 比如自定义技能
                            yield key, value
                }

            AsCols [ for row in Seq.chunkBySize colCount ordered do
                         for name, value in row do
                             Literal name
                             Integer value ]
        }
