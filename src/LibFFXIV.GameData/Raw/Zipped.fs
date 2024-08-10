namespace LibFFXIV.GameData.Raw

open System
open System.IO.Compression

open LibFFXIV.GameData.Raw


[<Sealed>]
type ZippedXivCollection(lang, zip: ZipArchive, ?pathPrefix: string) =
    inherit XivCollection(lang)

    let mutable csvFileBOM = true
    let mutable csvHeaderRows = -1

    let entriesCache =
        seq {
            for e in zip.Entries do
                yield e.FullName, e
        }
        |> readOnlyDict

    let prefix = defaultArg pathPrefix ""

    let getFileName name =
        let woLang = prefix + String.Join(".", name, "csv")
        let whLang = prefix + String.Join(".", name, lang.ToString(), "csv")

        if entriesCache.ContainsKey(woLang) then woLang
        elif entriesCache.ContainsKey(whLang) then whLang
        else failwithf $"找不到表%s{name} : %s{woLang}/%s{whLang}"

    let getHeader (csv: seq<string[]>) (name: string) =

        let headers = csv |> Seq.take csvHeaderRows |> Seq.toArray

        let mutable tempArray =
            try
                let origin = headers |> Array.find (fun line -> line.[0] = "key")
                let columnName = headers |> Array.find (fun line -> line.[0] = "#")
                let typeName = headers |> Array.find (fun line -> line.[0] = "int32")

                Array.map3
                    (fun a b c ->
                        { XivHeaderItem.OrignalKeyName = a
                          XivHeaderItem.ColumnName = b
                          XivHeaderItem.TypeName = c })
                    origin
                    columnName
                    typeName
            with e ->
                failwithf $"%A{e} csvFileBOM={csvFileBOM} csvHeaderRows={csvHeaderRows} src=%A{headers}"

        let path = $"{prefix}Definitions/{IO.Path.GetFileName(name)}.json"

        if entriesCache.ContainsKey(path) then
            // wipe all info except 'key'
            for i = 1 to tempArray.Length - 1 do
                tempArray.[i] <-
                    { tempArray.[i] with
                         // -1 to match key,0,1,2,3 offset
                         ColumnName = $"RAW_{i - 1}"
                         // no type info provided in json
                         TypeName = "UNKNOWN-JSON" }

            // rewrite columnName
            use stream = entriesCache.[path].Open()

            let data =
                let defs = SaintCoinach.SaintCoinachParser.ParseJson(stream)
                SaintCoinach.SaintCoinachParser.GenerateSheetColumns(defs.Definitions)

            for (idx, name, t) in data.Cols do
                // some definition has more than actual columns
                if idx < tempArray.Length then
                    tempArray.[idx] <-
                        { tempArray.[idx] with
                            ColumnName = name
                            TypeName = t }

        XivHeader(tempArray)

    do
        // 检测压缩包内csv的常见特征

        let sample =
            // 应该足够长以避免和其他文件混淆
            let sampleName = "ContentFinderConditionTransient.csv"
            let kv = entriesCache |> Seq.find (fun kv -> kv.Key.Contains(sampleName))
            kv.Value

        // 确认BOM
        csvFileBOM <-
            use fs = sample.Open()
            use r = new IO.BinaryReader(fs)

            let bom = Text.Encoding.UTF8.GetPreamble()
            let bytes = r.ReadBytes(bom.Length)

            bom = bytes

        // 确认header行数
        let headers =
            use fs = sample.Open()
            use r = new IO.StreamReader(fs, Text.Encoding.ASCII)

            [| for _ = 1 to 10 do
                   // 读10行当样本
                   r.ReadLine() |]
            |> Seq.takeWhile (fun line -> not <| Char.IsDigit(line.[0]))
            |> Seq.toArray

        if headers.Length > 4 then
            failwithf "Header length is longer than expacted"

        csvHeaderRows <- headers.Length

    override x.GetSheetUncached name =
        let csv =
            seq {
                use fs = entriesCache.[getFileName name].Open()

                // CsvReader 会重置给出的Encoding，所以只能在这里把BOM提前读掉
                if csvFileBOM then
                    for i = 1 to Text.Encoding.UTF8.GetPreamble().Length do
                        fs.ReadByte() |> ignore

                use csv = new CsvParser.CsvReader(fs, Text.Encoding.UTF8)

                while csv.MoveNext() do
                    yield csv.Current |> Seq.toArray
            }

        let header = getHeader csv name
        let sheet = XivSheet(name, x, header)

        csv
        |> Seq.skip csvHeaderRows
        |> Seq.map (fun fields -> XivRow(sheet, fields))
        |> sheet.SetRowSource

        sheet

    override x.GetAllSheetNames() =
        entriesCache.Keys
        |> Seq.filter (fun path -> path.EndsWith(".csv"))
        |> Seq.map (fun path -> path.[0 .. path.IndexOf(".") - 1].Replace(prefix, ""))

    override x.SheetExists name =
        try
            getFileName name |> ignore
            true
        with _ ->
            false

    override x.Dispose() = base.Dispose()
