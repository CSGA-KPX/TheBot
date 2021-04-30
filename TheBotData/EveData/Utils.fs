module KPX.TheBot.Data.EveData.Utils

open System
open System.IO


let internal GetEveDataArchive () =
    let path = Path.Combine(KPX.TheBot.Data.Common.Resource.StaticDataPath, "EVEData.zip")
    new Compression.ZipArchive(File.OpenRead(path), Compression.ZipArchiveMode.Read)

let inline pct (i : int) = (float i) / 100.0

[<RequireQualifiedAccess>]
type PriceFetchMode =
    | Sell
    | SellWithTax
    | Buy
    | BuyWithTax
    /// 游戏内部加权价格
    /// Adjusted has modifications to prevent manipulation. 
    | AdjustedPrice
    /// 游戏内部平均价格
    /// Average is a average over the last 28 days, over the entirety of new eden.
    | AveragePrice
    /// 游戏内部硬编码的价格
    | BasePrice

    override x.ToString() =
        match x with
        | BasePrice -> "BasePrice"
        | Sell -> "税前卖出"
        | SellWithTax -> "税后卖出"
        | Buy -> "税前收购"
        | BuyWithTax -> "税后收购"
        | AdjustedPrice -> "内部加权"
        | AveragePrice -> "内部平均"

[<Literal>]
/// 普通矿
let OreNames =
    "凡晶石,灼烧岩,干焦岩,斜长岩,奥贝尔石,水硼砂,杰斯贝矿,希莫非特,同位原矿,片麻岩,黑赭石,灰岩,克洛基石,双多特石,艾克诺岩,基腹断岩"

[<Literal>]
/// 冰矿
let IceNames =
    "白釉冰,冰晶矿,粗蓝冰,电冰体,富清冰,光滑聚合冰,黑闪冰,加里多斯冰矿,聚合冰体,蓝冰矿,朴白釉冰,清冰锥"

[<Literal>]
/// 卫星矿石
let MoonNames =
    "沸石,钾盐,沥青,柯石英,磷钇矿,独居石,铈铌钙钛矿,硅铍钇矿,菱镉矿,砷铂矿,钒铅矿,铬铁矿,钴酸盐,黑稀金矿,榍石,白钨矿,钒钾铀矿,锆石,铯榴石,朱砂"

[<Literal>]
/// 矿物
let MineralNames =
    "三钛合金,类晶体胶矿,类银超金属,同位聚合体,超新星诺克石,晶状石英核岩,超噬矿,莫尔石"

[<Literal>]
/// 导管矿石
let TriglavianOreNames = "拉克岩,贝兹岩,塔拉岩"
