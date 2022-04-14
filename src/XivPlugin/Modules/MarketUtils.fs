module KPX.XivPlugin.Modules.Utils.MarketUtils

open KPX.FsCqHttp.Utils.AliasMapper


let MateriaGrades = [| "壹型"; "贰型"; "叁型"; "肆型"; "伍型"; "陆型"; "柒型"; "捌型" |]

let MateriaAliasMapper =
    let mapper = AliasMapper()
    mapper.Add("雄略", "信念", "信", "DET")
    mapper.Add("神眼", "直击", "直", "DH")
    mapper.Add("武略", "暴击", "暴", "CIT")
    mapper.Add("战技", "技能速度", "技速")
    mapper.Add("咏唱", "咏唱速度", "咏速")
    mapper.Add("刚柔", "坚韧", "TEN")
    mapper.Add("信力", "信仰", "PIE")
    mapper.Add("魔匠", "制作力", "CP")
    mapper.Add("巨匠", "加工精度", "加工")
    mapper.Add("名匠", "作业精度", "作业")
    mapper.Add("器识", "采集力", "GP", "采集")
    mapper.Add("达识", "获得力", "获得")
    mapper.Add("博识", "鉴别力", "鉴别")
    mapper
