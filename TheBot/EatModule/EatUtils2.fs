module KPX.TheBot.Module.EatModule.EatUtils2

open System

open KPX.TheBot.Utils.Dicer
open KPX.TheBot.Utils.EmbeddedResource

[<AutoOpen>]
module private Data =
    let private mgr = StringResource("Eat")
    let breakfast = mgr.GetWords("早加餐") |> Array.distinct
    let dinner = mgr.GetWords("中晚餐") |> Array.distinct
    let hotpot_soup = mgr.GetWords("火锅底料") |> Array.distinct
    let hotpot_sauce = mgr.GetWords("火锅蘸料") |> Array.distinct
    let hotpot_dish = mgr.GetWords("火锅配菜") |> Array.distinct
    let DinnerTypes = [| "早餐"; "午餐"; "晚餐"; "加餐" |]

[<Struct>]
type MappedOption =
    { Option : string
      Value : int }

    /// 转换为字符串
    ///
    /// true "%s(%i)"
    ///
    /// false %s
    member x.ToString(withValue) =
        if withValue then sprintf "%s(%i)" x.Option x.Value else x.Option

    /// 转换为带有值的字符串形式
    override x.ToString() = x.ToString(true)

type EatChoices(options : string [], dicer : Dicer) =
    static let DiceSides = 100u

    let dicer = dicer.Freeze() // 确保不会跟顺序有关

    let mapped =
        options
        |> Array.map
            (fun opt ->
                { Option = opt
                  Value = dicer.GetRandom(DiceSides, opt) })
        |> Array.sortBy (fun opt -> opt.Value)

    member x.MappedOptions = mapped |> Seq.readonly

    /// 获取所有小于等于threadhold的选项
    member x.GetGoodOptions(threadhold) = 
        mapped
        |> Seq.filter (fun opt -> opt.Value <= threadhold)

    /// 获取所有大于等于threadhold的选项(默认51)
    member x.GetBadOptions(threadhold) = 
        mapped
        |> Seq.filter (fun opt -> opt.Value >= threadhold)