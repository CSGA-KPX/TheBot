namespace KPX.BioPlugin.Data

open System
open System.IO

open KPX.TheBot.Host.Data
open KPX.TheBot.Host.DataCache
open KPX.TheBot.Host.DataCache.LiteDb

open BioFSharp


[<CLIMutable>]
type TRCNInfo =
    { Id: int
      CloneId : string
      TargetSequence: string
      VectorId: string
      TargetRegion: string
      TaxonId: int }

    member x.GenerateOligo() =
        let target = x.TargetSequence |> BioSeq.ofNucleotideString
        let sense = target |> BioSeq.toString
        let anti = target |> BioSeq.reverseComplement |> BioSeq.toString
        let fwd = $"CCGG{sense}CTCGAG{anti}TTTTTG"
        let rev = $"AATTCAAAAA{sense}CTCGAG{anti}"
        {|Forward = fwd; Reverse = rev|}

[<Struct>]
type Taxon =
    | Human
    | Mouse

    member x.TaxonId =
        match x with
        | Human -> 9606
        | Mouse -> 10090

type ShRnaCollection private () =
    inherit CachedTableCollection<string, TRCNInfo>()

    static let instance = ShRnaCollection()
    static member Instance = instance

    override x.IsExpired = false

    override x.Depends = Array.empty

    override x.InitializeCollection() =
        x.DbCollection.EnsureIndex("CloneId", false) |> ignore

        use s = EmbeddedResource.GetResFileStream("BioPlugin.trc_public.05Apr11.zip")
        use archive = new Compression.ZipArchive(s, Compression.ZipArchiveMode.Read)
        use fs = archive.GetEntry("trc_public.05Apr11.txt").Open()

        let mutable csvConfig = CsvParser.CsvReader.Config()
        csvConfig.ColumnSeparator <- '\t'
        csvConfig.WithQuotes <- false

        use csv = new CsvParser.CsvReader(fs, Text.Encoding.UTF8, csvConfig)

        seq {
            // 跳过表头
            csv.MoveNext() |> ignore

            while csv.MoveNext() do
                let line = csv.Current

                if line.Count > 15 then
                    let cloneId = line[4]
                    let targetSeq = line[5]
                    let vectorId = line[7]
                    let targetRegion = line[9]
                    let taxonId = line[11]

                    if taxonId = "9606" || taxonId = "10090" then

                        { Id = 0
                          CloneId = cloneId
                          TargetSequence = targetSeq
                          VectorId = vectorId
                          TargetRegion = targetRegion
                          TaxonId = taxonId |> int}
        }
        |> x.DbCollection.InsertBulk
        |> ignore

        GC.Collect()

    member x.TryFindByTRCNId(id: string) =
        x.DbCollection.TryFindOne(LiteDB.Query.EQ("CloneId", id))

    member x.FindByGene(symbol: string, taxon: Taxon) =
        let symbolEq = LiteDB.Query.EQ("GeneSymbol", symbol)
        let taxonEq = LiteDB.Query.EQ("TaxonId", taxon.TaxonId)

        x.DbCollection.Find(LiteDB.Query.And(symbolEq, taxonEq)) |> Seq.toArray
