module TheBot.Module.EatModule.Utils

open System

open TheBot.Utils.Dicer


let private rm = TheBot.Utils.EmbeddedResource.GetResourceManager("Eat")
let private emptyChars = [| ' '; '\t'; '\r'; '\n' |]

let private readChoice (name : string) =
    rm.GetString(name)
      .Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
      
[<RequireQualifiedAccess>]
type EatFormat = 
    | All
    | Head
    | HeadWithoutNumber
    | YesOrNo

type EatChoices(array : string [], dicer : Dicer) = 
    static let formatPair (pair : string * int) = 
        sprintf "%s(%i)" (fst pair) (snd pair)
    static let goodCutoff = 5
    static let   noCutOff = 96
    let mapped = 
        array
        |> Array.map (fun x -> x, dicer.GetRandom(100u, x))
        |> Array.sortBy (snd)

    member x.ToString(f : EatFormat) = 
        use sw = new IO.StringWriter()
        match f with
        | EatFormat.All ->
            mapped |> Array.iter (fun p -> sw.Write(formatPair p))
        | EatFormat.Head ->
            sw.Write(formatPair (mapped |> Array.head))
        | EatFormat.HeadWithoutNumber ->
            sw.Write(snd (mapped |> Array.head))
        | EatFormat.YesOrNo -> 
            let g = mapped |> Array.filter (fun (_, s) -> s <= goodCutoff)
            let n = mapped |> Array.filter (fun (_, s) -> s >=   noCutOff) |> Array.sortByDescending (snd)
            sw.Write(("宜：{0}\r\n", String.Join(" ", g |> Array.map (formatPair))))
            sw.Write(("忌：{0}\r\n", String.Join(" ", n |> Array.map (formatPair))))
        sw.ToString()

let private breakfast = readChoice("早加餐") |> Array.distinct
let private dinner = readChoice("中晚餐") |> Array.distinct
let private hotpot_soup = readChoice("火锅底料") |> Array.distinct
let private hotpot_sauce = readChoice("火锅蘸料") |> Array.distinct
let private hotpot_dish = readChoice("火锅配菜") |> Array.distinct
let ng = readChoice("别吃") |> Array.distinct

let whenToEat (dicer : Dicer, str : string) = 
    let types = [|"早餐"; "午餐"; "晚餐"; "加餐"|]
    let ret = seq { for t in types do 
                        let str = sprintf "%s吃%s" t str
                        let d = dicer.GetRandom(100u, str)
                        let ret = 
                            match d with
                            | _ when d  =100 -> "黄连素备好"
                            | _ when d >= 96 -> "上秤看看"
                            | _ when d >= 76 -> "算了吧"
                            | _ when d >= 51 -> "不推荐"
                            | _ when d >= 26 -> "也不是不行"
                            | _ when d >=  6 -> "还好"
                            | _ when d >=  1 -> "好主意"
                            | _ -> 
                                failwith "你说啥来着？"
                        yield sprintf "%s : %s(%i)" str ret d }
    String.Join("\r\n", ret)

let private mealsFunc prefix array (dicer : Dicer) = 
    let luck = dicer.GetRandom(100u, "吃"+prefix)

    if luck >= 96 then
        dicer.GetRandomItem(ng)
    else
        let mapped = 
            array
            |> Array.map (fun x -> x, dicer.GetRandom(100u, prefix + "吃" + x))
        let eat = 
            mapped
            |> Array.filter (fun (_, c) -> c <= 5 )
            |> Array.sortBy (snd)
            |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

        let notEat = 
            mapped
            |> Array.filter (fun (_, c) -> c >= 96 )
            |> Array.sortByDescending (snd)
            |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

        sprintf "宜：%s\r\n忌：%s" (String.Join(" ", eat)) (String.Join(" ", notEat))

let private hotpotFunc (dicer : Dicer) = 
    let sw = Text.StringBuilder()
    sw.AppendLine(whenToEat(dicer, "火锅")) |> ignore
    sw.AppendLine("") |> ignore
    let soup =
        hotpot_soup
        |> Array.map (fun x -> x, dicer.GetRandom(100u, "火锅吃"+x))
        |> Array.sortBy (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

    let sauce =
        hotpot_sauce
        |> Array.map (fun x -> x, dicer.GetRandom(100u, "火锅吃"+x))
        |> Array.sortBy (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

    let dish = hotpot_dish |> Array.map (fun x -> x, dicer.GetRandom(100u, "火锅吃"+x))
    let dish_good =
        dish
        |> Array.filter (fun (_, c) -> c <= 10 )
        |> Array.sortBy (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )
    let dish_bad = 
        dish
        |> Array.filter (fun (_, c) -> c >= 91 )
        |> Array.sortByDescending (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

    sw.AppendFormat("锅底：{0}\r\n", String.Join(" ", soup))
        .AppendFormat("蘸料：{0}\r\n", String.Join(" ", sauce))
        .AppendFormat("　宜：{0}\r\n", String.Join(" ", dish_good))
        .AppendFormat("　忌：{0}", String.Join(" ", dish_bad))
        .ToString()

let private saizeriya = 
    [|
        for row in rm.GetString("萨莉亚").Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries) do
            let s = row.Split([|'：'|], StringSplitOptions.RemoveEmptyEntries)
            let name = s.[0]
            let c = s.[1].Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
            yield name, c
    |]

let private saizeriyaFunc (dicer : Dicer) = 
    let sw = new IO.StringWriter()
    sw.WriteLine(whenToEat(dicer, "萨莉亚"))
    sw.WriteLine()
    for (name, c) in saizeriya do
        let mapped = 
            c
            |> Array.map (fun x -> x, dicer.GetRandom(100u, "萨莉亚吃" + x))
            |> Array.filter (fun (_, c) -> c <= 50 )
            |> Array.sortBy (snd)
            |> Array.truncate 5
            |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )
        if mapped.Length <> 0 then
            sw.Write(sprintf "%s：%s" name (String.Join(" ", mapped)))
    sw.ToString()

let eatAlias = 
    let map = 
        [|
            "早餐", [| "早"; "早饭" |]
            "午餐", [| "中"; "中饭"; "午" |]
            "晚餐", [| "晚"; "晚饭" |]
            "加餐", [| "加"; "夜宵" |]
            "萨莉亚", [|"萨利亚"|]
        |]
    seq {
        for (key, aliases) in map do
            for alias in aliases do 
                yield alias, key
    }
    |> readOnlyDict

let eatFuncs : Collections.Generic.IReadOnlyDictionary<string, Dicer -> string> = 
    [|
        "早餐", mealsFunc "早餐" breakfast
        "加餐", mealsFunc "加餐" breakfast
        "晚餐", mealsFunc "晚餐" dinner
        "午餐", mealsFunc "午餐" dinner
        "火锅", hotpotFunc
        "萨莉亚", saizeriyaFunc
    |]
    |> readOnlyDict

