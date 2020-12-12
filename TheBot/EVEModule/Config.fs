namespace TheBot.Module.EveModule.Utils.Config

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open BotData.EveData.Utils

type EveConfigParser() as x = 
    inherit UserOptionParser()

    do
        x.RegisterOption("ime", "2")
        x.RegisterOption("dme", "10")
        x.RegisterOption("sci", "3")
        x.RegisterOption("tax", "10")
        x.RegisterOption("p", "false")
        x.RegisterOption("r", "false")
        x.RegisterOption("buy", "")
        x.RegisterOption("debug", "")
        x.RegisterOption("text", "")
        x.RegisterOption("selltax", "6")
        x.RegisterOption("buytax", "4")

    /// 是否强制文本输出。具体输出方式取决于酷Q
    member x.IsImageOutput = 
        if x.IsDefined("text") then ForceText else PreferImage

    member x.IsDebug = x.IsDefined("debug")

    member x.InputMe = x.GetValue<int>("ime")

    member x.DerivativetMe = x.GetValue<int>("dme")

    member x.SystemCostIndex = x.GetValue<int>("sci")

    member x.StructureTax = x.GetValue<int>("tax")

    member x.ExpandReaction = x.IsDefined("r")

    member x.ExpandPlanet = x.IsDefined("p")

    member x.MaterialPriceMode =
        if x.IsDefined("buy") then
            PriceFetchMode.BuyWithTax
        else
            PriceFetchMode.Sell

    interface BotData.EveData.Process.IEveCalculatorConfig with
        member x.InputME = x.InputMe
        member x.DerivedME = x.DerivativetMe
        member x.ExpandPlanet = x.ExpandPlanet
        member x.ExpandReaction = x.ExpandReaction