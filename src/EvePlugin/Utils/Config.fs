namespace KPX.EvePlugin.Utils.Config

open KPX.FsCqHttp.Utils.UserOption

open KPX.EvePlugin.Data.EveType
open KPX.EvePlugin.Data.Utils
open KPX.EvePlugin.Data.Process

open KPX.TheBot.Host.DataModel.Recipe


type EveConfigParser() as x =
    inherit CommandOption()

    let ime = OptionCellSimple(x, "ime", 2)
    let dme = OptionCellSimple(x, "dme", 10)
    let sci = OptionCellSimple(x, "sci", 5)
    let tax = OptionCellSimple(x, "tax", 10)

    let p = OptionCell(x, "p")
    let r = OptionCell(x, "r")
    let buy = OptionCell(x, "buy")

    member x.SetDefaultInputMe(value) = ime.Default <- value

    member x.InputMe = ime.Value

    member x.DerivationMe = dme.Value

    member x.SystemCostIndex = sci.Value

    member x.StructureTax = tax.Value

    member x.ExpandReaction = r.IsDefined

    member x.ExpandPlanet = p.IsDefined

    member x.MaterialPriceMode =
        if buy.IsDefined then
            PriceFetchMode.BuyWithTax
        else
            PriceFetchMode.Sell

    /// 自动获取输入材料效率
    /// 如果ime被设置，返回ime
    /// 如果为设置，根据蓝图metaGroupId返回默认效率
    member x.GetImeAuto(item: EveType) =
        if ime.IsDefined then
            x.InputMe
        else
            match item.MetaGroupId with
            | 1
            | 54 -> 10 // T1装备建筑默认10
            | 2
            | 14
            | 53 -> 2 // T2/T3装备 建筑默认2
            | _ -> 0 // 其他默认0

    member val RunRounding = ProcessRunRounding.RoundUp with get, set

    /// 用dme覆写ime
    member cfg.GetShiftedConfig() =
        { new IEveCalculatorConfig with
            member x.InputMe = cfg.DerivationMe
            member x.DerivationMe = cfg.DerivationMe
            member x.ExpandPlanet = cfg.ExpandPlanet
            member x.ExpandReaction = cfg.ExpandReaction
            member x.RunRounding = cfg.RunRounding }

    interface IEveCalculatorConfig with
        member x.InputMe = x.InputMe
        member x.DerivationMe = x.DerivationMe
        member x.ExpandPlanet = x.ExpandPlanet
        member x.ExpandReaction = x.ExpandReaction
        member x.RunRounding = x.RunRounding
