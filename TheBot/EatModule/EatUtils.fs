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
        |> Array.map (fun x -> x,dicer.GetRandomFromString(x, 100u))
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

let private mealsFunc prefix array (dicer : Dicer) = 
    let luck = dicer.GetRandomFromString("吃"+prefix, 100u)

    if luck >= 96 then
        dicer.GetRandomItem(ng)
    else
        let mapped = 
            array
            |> Array.map (fun x -> x, dicer.GetRandomFromString(prefix + "吃" + x, 100u))
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
    let soup =
        hotpot_soup
        |> Array.map (fun x -> x, dicer.GetRandomFromString("火锅吃"+x, 100u))
        |> Array.sortBy (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

    let sauce =
        hotpot_sauce
        |> Array.map (fun x -> x, dicer.GetRandomFromString("火锅吃"+x, 100u))
        |> Array.sortBy (snd)
        |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

    let dish = hotpot_dish |> Array.map (fun x -> x, dicer.GetRandomFromString("火锅吃"+x, 100u))
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
        .AppendFormat("　忌：{0}\r\n", String.Join(" ", dish_bad))
        .ToString()

let private saizeriya = 
    [|
        for row in rm.GetString("萨莉亚").Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries) do
            let s = row.Split([|'：'|], StringSplitOptions.RemoveEmptyEntries)
            printfn "%A" s
            let name = s.[0]
            let c = s.[1].Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
            yield name, c
    |]

let private saizeriyaFunc (dicer : Dicer) = 
    let sw = new IO.StringWriter()
    for (name, c) in saizeriya do
        let mapped = 
            c
            |> Array.map (fun x -> x, dicer.GetRandomFromString("萨莉亚吃" + x, 100u))
            |> Array.filter (fun (_, c) -> c <= 50 )
            |> Array.sortBy (snd)
            |> Array.truncate 5
            |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )
        if mapped.Length <> 0 then
            sw.WriteLine(sprintf "%s：%s" name (String.Join(" ", mapped)))
    sw.ToString()

let eatFuncTable : (string * string * (Dicer -> string)) [] = 
    [|
        "早",    "早餐", mealsFunc "早餐" breakfast
        "加",    "加餐", mealsFunc "加餐" breakfast
        "晚",    "晚餐", mealsFunc "晚餐" dinner
        "午",    "午餐", mealsFunc "午餐" dinner

        "火锅",  "火锅", hotpotFunc

        "萨莉亚", "萨莉亚", saizeriyaFunc
        "萨利亚", "萨莉亚", saizeriyaFunc
    |]

