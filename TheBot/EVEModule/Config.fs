namespace KPX.TheBot.Module.EveModule.Utils.Config

open KPX.FsCqHttp.Utils.UserOption

open KPX.TheBot.Data.EveData.Utils


type EveConfigParser() as x = 
    inherit OptionBase()

    let ime = OptionCellSimple(x, "ime", 2)
    let dme = OptionCellSimple(x, "dme", 10)
    let sci = OptionCellSimple(x, "sci", 4)
    let tax = OptionCellSimple(x, "tax", 10)

    let p = OptionCell(x, "p")
    let r = OptionCell(x, "r")
    let buy = OptionCell(x, "buy")

    member x.SetDefaultInputMe(value)= ime.Default <- value

    member x.InputMe = ime.Value

    member x.DerivativetMe = dme.Value

    member x.SystemCostIndex = sci.Value

    member x.StructureTax = tax.Value

    member x.ExpandReaction = r.IsDefined

    member x.ExpandPlanet = p.IsDefined

    member x.MaterialPriceMode =
        if buy.IsDefined then PriceFetchMode.BuyWithTax else PriceFetchMode.Sell

    interface KPX.TheBot.Data.EveData.Process.IEveCalculatorConfig with
        member x.InputME = x.InputMe
        member x.DerivedME = x.DerivativetMe
        member x.ExpandPlanet = x.ExpandPlanet
        member x.ExpandReaction = x.ExpandReaction