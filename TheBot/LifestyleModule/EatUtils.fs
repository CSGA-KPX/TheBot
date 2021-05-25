module KPX.TheBot.Module.LifestyleModule.EatUtils

open System

open KPX.TheBot.Utils.Dicer
open KPX.TheBot.Utils.EmbeddedResource


[<AutoOpen>]
module private Data =
    let diceSides = 100u

    let private mgr = StringResource("Eat")
    let breakfast = mgr.GetWords("早加餐") |> Array.distinct
    let dinner = mgr.GetWords("中晚餐") |> Array.distinct
    let hotpot_soup = mgr.GetWords("火锅底料") |> Array.distinct
    let hotpot_sauce = mgr.GetWords("火锅蘸料") |> Array.distinct
    let hotpot_dish = mgr.GetWords("火锅配菜") |> Array.distinct

    let snacks =
        mgr.GetWordsWithoutComment("零食") |> Array.distinct

    let drinks =
        mgr.GetWordsWithoutComment("饮料") |> Array.distinct

    let dinnerTypes = [| "早餐"; "午餐"; "晚餐"; "加餐" |]

    let saizeriya =
        [| for row in mgr.GetLines("萨莉亚") do
            let s =
                row.Split([| '：' |], StringSplitOptions.RemoveEmptyEntries)

            let name = s.[0]

            let c =
                s.[1]
                    .Split(Array.empty<char>, StringSplitOptions.RemoveEmptyEntries)

            yield name, c |]

[<Struct>]
type MappedOption =
    { Original : string
      Mapped : string
      Value : int }

    member x.DescribeValue() =
        let d = x.Value

        match d with
        | _ when d = 100 -> "黄连素备好"
        | _ when d >= 96 -> "上秤看看"
        | _ when d >= 76 -> "算了吧"
        | _ when d >= 51 -> "不推荐"
        | _ when d >= 26 -> "也不是不行"
        | _ when d >= 6 -> "还好"
        | _ when d >= 1 -> "好主意"
        | _ -> raise <| ArgumentOutOfRangeException("value")

    /// 转换为带有值的字符串形式
    override x.ToString() = $"%s{x.Original}(%i{x.Value})"

    static member Create(dicer : Dicer, option : string) =
        { Original = option
          Mapped = option
          Value = dicer.GetPositive(diceSides, option) |> int}

    static member Create(dicer : Dicer, option : string, prefix) =
        let mapped = prefix + option

        { Original = option
          Mapped = mapped
          Value = dicer.GetPositive(diceSides, mapped) |> int}

type EatChoices(options : seq<string>, dicer : Dicer, ?prefix : string) =
    let prefix = defaultArg prefix ""

    let mapped =
        if not dicer.IsFrozen then invalidArg (nameof dicer) "Dicer没有固定"

        options
        |> Seq.map
            (fun opt ->
                if prefix = "" then MappedOption.Create(dicer, opt) else MappedOption.Create(dicer, opt, prefix))
        |> Seq.sortBy (fun opt -> opt.Value)
        |> Seq.cache

    member x.MappedOptions = mapped |> Seq.readonly

    /// 获取所有小于等于threshold的选项
    member x.GetGoodOptions(threshold) =
        mapped
        |> Seq.filter (fun opt -> opt.Value <= threshold)

    /// 获取所有大于等于threshold的选项(默认51)
    member x.GetBadOptions(threshold) =
        mapped
        |> Seq.filter (fun opt -> opt.Value >= threshold)
        |> Seq.rev

/// 根据早中晚加分别打分
///
/// 如果只有一个选项就给评价，多个选项只有打分
let scoreByMeals (dicer : Dicer) (options : string []) (ret : IO.TextWriter) =
    if not dicer.IsFrozen then invalidArg (nameof dicer) "Dicer没有固定"

    match options.Length with
    | 0 -> invalidArg (nameof options) "没有可选项"
    | 1 ->
        for t in dinnerTypes do
            let str = $"%s{t}吃%s{options.[0]}"
            let opt = MappedOption.Create(dicer, str)
            ret.WriteLine $"%s{str} : %s{opt.DescribeValue()}(%i{opt.Value})"
    | _ ->
        for t in dinnerTypes do
            let prefix = $"%s{t}吃"

            let mapped =
                EatChoices(options, dicer, prefix).MappedOptions
                |> Seq.map (fun x -> x.ToString())

            ret.WriteLine(sprintf "%s：%s" t (String.Join(" ", mapped)))

/// 对 prefix+吃+option打分
let private mealsFunc prefix options (dicer : Dicer) (ret : IO.TextWriter) =
    let mapped = EatChoices(options, dicer, prefix + "吃")

    let eat =
        mapped.GetGoodOptions(5)
        |> Seq.map (fun opt -> opt.ToString())

    let notEat =
        mapped.GetBadOptions(96)
        |> Seq.map (fun opt -> opt.ToString())

    ret.WriteLine("宜：{0}", String.Join(" ", eat))
    ret.WriteLine("忌：{0}", String.Join(" ", notEat))

let private hotpotFunc (dicer : Dicer) (ret : IO.TextWriter) =

    scoreByMeals dicer (Array.singleton "火锅") ret

    ret.WriteLine()

    let prefix = "火锅吃"

    let soup =
        EatChoices(hotpot_soup, dicer, prefix)
            .MappedOptions
        |> Seq.map (fun x -> x.ToString())

    let sauce =
        EatChoices(hotpot_sauce, dicer, prefix)
            .MappedOptions
        |> Seq.map (fun x -> x.ToString())

    let dish = EatChoices(hotpot_dish, dicer, prefix)

    let goodDish =
        dish.GetGoodOptions(5)
        |> Seq.map (fun x -> x.ToString())

    let badDish =
        dish.GetBadOptions(96)
        |> Seq.map (fun x -> x.ToString())

    ret.WriteLine("锅底：{0}", String.Join(" ", soup))
    ret.WriteLine("蘸料：{0}", String.Join(" ", sauce))
    ret.WriteLine("　宜：{0}", String.Join(" ", goodDish))
    ret.WriteLine("　忌：{0}", String.Join(" ", badDish))

let private saizeriyaFunc (dicer : Dicer) (ret : IO.TextWriter) =

    scoreByMeals dicer (Array.singleton "萨莉亚") ret

    ret.WriteLine()

    let prefix = "萨莉亚吃"

    for section, options in saizeriya do
        let opts =
            EatChoices(options, dicer, prefix)
                .GetGoodOptions(49)
            |> Seq.truncate 5
            |> Seq.map (fun x -> x.ToString())
            |> Seq.toArray

        if opts.Length <> 0 then ret.WriteLine("{0}：{1}", section, String.Join(" ", opts))

let eatAlias =
    let map =
        [| "早餐", [| "早"; "早饭" |]
           "午餐", [| "中"; "中饭"; "午" |]
           "晚餐", [| "晚"; "晚饭" |]
           "加餐", [| "加"; "夜宵" |]
           "萨莉亚", [| "萨利亚" |]
           "火锅", Array.empty
           "零食", Array.empty
           "饮料", Array.empty |]

    seq {
        for key, aliases in map do
            yield key, key

            for alias in aliases do
                yield alias, key
    }
    |> readOnlyDict

let eatFuncs : Collections.Generic.IReadOnlyDictionary<string, Dicer -> IO.TextWriter -> unit> =
    [| "早餐", mealsFunc "早餐" breakfast
       "加餐", mealsFunc "加餐" breakfast
       "晚餐", mealsFunc "晚餐" dinner
       "午餐", mealsFunc "午餐" dinner
       "#零食", mealsFunc "零食" snacks
       "#饮料", mealsFunc "饮料" drinks

       "火锅", hotpotFunc
       "萨莉亚", saizeriyaFunc |]
    |> readOnlyDict
