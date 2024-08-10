namespace LibFFXIV.GameData.Raw.SaintCoinach

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<AutoOpen>]
module private helper =
    type JObject with

        member x.TryToObject<'T>(propName: string) =
            if x.ContainsKey(propName) then
                Some <| x.[propName].ToObject<'T>()
            else
                None

type LinkCond = { Key: string; Value: int }

type ComplexLinkData =
    { Sheets: string[]
      Project: string option
      Key: string option
      Cond: LinkCond option }

type ConverterType =
    | Color
    | Generic
    | Icon
    | MultiRef of targets: string[]
    | Link of target: string
    | Tomestone
    | ComplexLink of links: ComplexLinkData[]

    member x.TargetType =
        match x with
        | Color -> "Color"
        | Icon -> "Image"
        | Link sht -> sht
        | Tomestone -> "Item"
        | MultiRef _
        | Generic
        | ComplexLink _ -> "Row"

    member x.TryDescribe() =
        match x with
        | Color -> "use Color.FromArgb, OR 0xFF000000 if needs alpha"
        | Icon -> "Icon not supported"
        | Link sht -> $"Links to {sht}"
        | Tomestone -> "lookup TomestonesItem.Tomestones, try Item if not exists."
        | Generic -> "Reference to key within same sheet"
        | MultiRef shts ->
            let shts = String.Join(",", shts)
            $"Reference sheets based on key and order: {shts}"
        | ComplexLink conds ->
            let sb = Text.StringBuilder("Complex Link:")

            for cond in conds do
                sb.Append("\r\n    ") |> ignore

                match cond.Cond with
                | Some link -> sb.Append($"When {link.Key} = {link.Value} ") |> ignore
                | None -> ()

                match cond.Key with
                | Some key -> sb.Append($"lookup {key} on ") |> ignore
                | None -> sb.Append($"lookup # on ") |> ignore

                match cond.Project with
                | Some key -> sb.Append($"and get value of {key} ") |> ignore
                | None -> ()

                sb.Append(String.Join(",", cond.Sheets)) |> ignore

            sb.ToString()

    static member FromJObject(o: JToken) : ConverterType option =
        if isNull o then
            None
        else
            let o = o :?> JObject

            let ret =
                match o.["type"].ToObject<string>() with
                | "color" -> Color
                | "generic" -> Generic
                | "icon" -> Icon
                | "tomestone" -> Tomestone
                | "multiref" -> MultiRef(o.["targets"].ToObject<string[]>())
                | "link" -> Link(o.["target"].ToObject<string>())
                | "complexlink" ->
                    [| let arr = o.["links"] :?> JArray

                       for item in arr do
                           let item = item :?> JObject

                           let sheets =
                               if item.ContainsKey("sheet") then
                                   item.["sheet"].ToObject<string>() |> Array.singleton
                               else
                                   item.["sheets"].ToObject<string[]>()

                           let project = item.TryToObject<string>("project")
                           let key = item.TryToObject<string>("key")
                           let cond = item.TryToObject<LinkCond>("when")

                           { Sheets = sheets
                             Project = project
                             Key = key
                             Cond = cond } |]
                    |> ComplexLink
                | value -> invalidArg "ConverterType" $"converterType={value}"

            Some ret

[<JsonConverter(typeof<DataDefintionConverter>)>]
type DataDefintion =
    | SimpleData of index: int * name: string * converter: Option<ConverterType>
    | GroupData of index: int * members: DataDefintion[]
    | RepeatData of index: int * count: int * def: DataDefintion

and DataDefintionConverter() =
    inherit JsonConverter<DataDefintion>()

    override x.WriteJson(_: JsonWriter, _: DataDefintion, _: JsonSerializer) =
        raise<unit> <| NotImplementedException()

    override x.ReadJson(r: JsonReader, _, _, _, _) =
        let o = JObject.Load(r)

        let index = o.TryToObject<int32>("index") |> Option.defaultValue (Int32.MinValue) |> ((+) 1) // offset 1 by primary key

        if o.ContainsKey("type") then
            match o.["type"].ToObject<string>() with
            | "repeat" ->
                let count = o.["count"].ToObject<int>()
                let def = o.["definition"].ToObject<DataDefintion>()
                RepeatData(index, count, def)
            | "group" -> GroupData(index, o.["members"].ToObject<DataDefintion[]>())
            | value -> invalidArg "DataDefintion" $"dataDefintion={value}"
        else
            let name = o.["name"].ToObject<string>()
            let converter = ConverterType.FromJObject(o.GetValue("converter"))
            SimpleData(index, name, converter)

type SheetDefinition =
    { Sheet: string
      DefaultColumn: string
      IsGenericReferenceTarget: bool
      Definitions: DataDefintion[] }

type FieldCommentInfo =
    { ColId: string
      Name: string
      Comment: string option }

[<Sealed>]
type SaintCoinachParser =
    static member ParseJson(s: Stream) =
        use r = new JsonTextReader(new StreamReader(s))
        JObject.Load(r).ToObject<SheetDefinition>()

    static member GenerateSheetColumns(defs: seq<DataDefintion>) =
        let out = ResizeArray<int * string * string>()
        let colIds = ResizeArray<string>([ "key" ])
        let colNames = ResizeArray<string>([ "#" ])
        let mutable currIdx = 0

        let rec dataWalker (data: DataDefintion) (postfix: string) (root: bool) =
            let rootId refId =
                if root then
                    if refId < 0 then currIdx <- 1 else currIdx <- refId

            match data with
            | SimpleData(idx, name, conv) ->
                rootId idx
                let t = conv |> Option.map (fun c -> c.TargetType) |> Option.defaultValue "UNKNOWN-JSON"

                out.Add(currIdx, name + postfix, t)
                colIds.Add((currIdx).ToString())
                colNames.Add(name + postfix)
                currIdx <- currIdx + 1
            | GroupData(idx, members) ->
                rootId idx

                for m in members do
                    dataWalker m postfix false
            | RepeatData(idx, count, def) ->
                rootId idx

                for i = 0 to count - 1 do
                    let postfix = $"[{i}]{postfix}"
                    dataWalker def postfix false

        for def in defs do
            dataWalker def "" true

        {| Ids = colIds
           Names = colNames
           Cols = out |}

    static member GenerateFieldComments(defs: seq<DataDefintion>) =
        let mutable currIdx = 0
        let cmtCache = Collections.Generic.Dictionary<string, FieldCommentInfo>()

        let rec dataWalker (data: DataDefintion) (postfix: string) (root: bool) =
            let rootId refId =
                if root then
                    if refId < 0 then currIdx <- 1 else currIdx <- refId

            match data with
            | SimpleData(idx, name, conv) ->
                rootId idx

                if not <| cmtCache.ContainsKey(name) then
                    cmtCache.Add(
                        name,
                        { ColId = (currIdx - 1).ToString()
                          Name = name// + postfix
                          Comment = conv |> Option.map (fun conv -> conv.TryDescribe()) }
                    )

                currIdx <- currIdx + 1
            | GroupData(idx, members) ->
                rootId idx

                for m in members do
                    dataWalker m postfix false
            | RepeatData(idx, count, def) ->
                rootId idx

                for i = 0 to count - 1 do
                    let postfix = $"[{i}]{postfix}"
                    dataWalker def postfix false

        for def in defs do
            dataWalker def "" true

        cmtCache.Values |> Seq.cast<FieldCommentInfo>
