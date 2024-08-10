namespace LibFFXIV.GameData

open System


[<RequireQualifiedAccess>]
type XivLanguage =
    | None
    | Japanese
    | English
    | German
    | French
    | ChineseSimplified
    | ChineseTraditional
    | Korean

    /// Convert to lang postfix in csv
    override x.ToString () =
        match x with
        | None -> ""
        | Japanese -> "ja"
        | English -> "en"
        | German -> "de"
        | French -> "fr"
        | Korean -> "kr"
        | ChineseSimplified -> "chs"
        | ChineseTraditional -> "cht"

    /// Parse lang postfix to XivLanguage
    static member FromString(lang : string) = 
        match lang.ToLowerInvariant() with
        | "" | "none" -> None
        | "ja" -> Japanese
        | "en" -> English
        | "de" -> German
        | "fr" -> French
        | "kr" -> Korean
        | "chs" -> ChineseSimplified
        | "cht" -> ChineseTraditional
        | _ -> invalidArg (nameof lang) $"Unknown language name : %s{lang}"


[<Struct>]
/// Primary key type for XivKey
type XivKey =
    { /// Key index for most sheets
      Main : int
      /// Alternative index for other sheets
      /// 
      /// As GilShopItem has 262144.0 ~ 262144.19
      Alt : int }

    /// Create XivKey from Main index.
    static member FromKey k = { Main = k; Alt = 0 }

    /// Parse XivKey from index string.
    static member FromString (str : string) =
        let v = str.Split('.')
        let k = v.[0] |> Int32.Parse

        let a =
            if v.Length = 2 then v.[1] |> Int32.Parse else 0

        { Main = k; Alt = a }

[<Struct>]
/// Holds row reference to target sheet.
///
/// Delays sheet creation to avoid deadlocks.
type XivSheetReference = { Key : int; Sheet : string }
