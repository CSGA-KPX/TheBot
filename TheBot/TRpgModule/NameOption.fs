namespace KPX.TheBot.Module.TRpgModule

open System

open KPX.FsCqHttp.Utils.AliasMapper
open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Module.TRpgModule.Strings


type private NameTypeCell(cb : OptionImpl, name, def) =
    inherit OptionCell<string>(cb, name, def)

    override x.ConvertValue(lang) =
        match lang with
        | "中文" -> StringData.Key_ChsName
        | "英文" -> StringData.Key_EngName
        | "英中" -> StringData.Key_EngChsName
        | "日文" -> StringData.Key_JpnName
        | _ -> invalidArg (nameof lang) $"%s{lang}不是允许值"

type NameOption() as x =
    inherit OptionImpl()

    let nameTypeCell =
        NameTypeCell(x, "lang", StringData.Key_EngChsName)

    let countCell = OptionCellSimple<uint32>(x, "c", 5u)

    static let aliases =
        let am = AliasMapper()
        am.Add("中文", "cn", "zh", "chs", "中", "汉语")
        am.Add("英文", "en", "eng", "英", "英语")
        am.Add("英中", "enzh", "engchs", "英汉")
        am.Add("日文", "jp", "jpn", "日", "日语")
        am

    member x.NameLanguageKey = nameTypeCell.Value

    member x.NameCount = countCell.Value |> int

    override x.PreParse(args) =
        seq {
            for arg in args do
                let m = arg |> aliases.TryMap

                if m.IsSome then
                    yield $"lang:%s{m.Value}"
                else
                    let succ, i = Int32.TryParse(arg)
                    if succ then yield $"c:%i{i}" else yield arg
        }
