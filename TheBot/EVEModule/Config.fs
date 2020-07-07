namespace TheBot.Module.EveModule.Utils.Config

open BotData.EveData.Utils
open BotData.EveData.EveBlueprint

type EveConfigParser() as x = 
    inherit TheBot.Utils.UserOption.UserOptionParser()

    do
        x.RegisterOption("ime", "10")
        x.RegisterOption("dme", "10")
        x.RegisterOption("sci", "5")
        x.RegisterOption("tax", "10")
        x.RegisterOption("p", "false")
        x.RegisterOption("r", "false")
        x.RegisterOption("buy", "")
        x.RegisterOption("debug", "")
        x.RegisterOption("text", "")

    /// 是否强制文本输出。具体输出方式取决于酷Q
    member x.IsImageOutput = not <| x.IsDefined("text")

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

    /// 测试蓝图能否继续展开
    member x.BpCanExpand(bp : EveBlueprint) = 
        match bp.Type with
        | BlueprintType.Manufacturing -> true
        | BlueprintType.Planet -> x.ExpandPlanet
        | BlueprintType.Reaction -> x.ExpandReaction
        | _ -> failwithf "未知蓝图类型 %A" bp

    member x.CalculateManufacturingFee(cost : float, bpt : BlueprintType) = 
        match bpt with
        | BlueprintType.Planet -> 0.0
        | _ ->
            cost * (pct x.SystemCostIndex) * (100 + x.StructureTax |> pct)