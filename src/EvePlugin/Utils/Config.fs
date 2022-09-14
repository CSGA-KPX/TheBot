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
