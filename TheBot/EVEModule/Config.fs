namespace TheBot.Module.EveModule.Utils.Config

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

open BotData.EveData.Utils
open BotData.EveData.EveBlueprint

open TheBot.Module.EveModule.Utils.Data

type EveConfigParser() as x = 
    inherit UserOptionParser()

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

    /// 测试蓝图能否继续展开
    member x.BpCanExpand(bp : EveBlueprint) = 
        match bp.Type with
        | BlueprintType.Manufacturing -> true
        | BlueprintType.Planet -> 
            // 1042 = P1，屏蔽P0 -> P1生产过程
            x.ExpandPlanet && (DataBundle.Instance.GetItem(bp.ProductId).GroupId <> 1042)
        | BlueprintType.Reaction -> x.ExpandReaction
        | _ -> failwithf "未知蓝图类型 %A" bp