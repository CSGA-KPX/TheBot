[<AutoOpen>]
module KPX.TheBot.Host.Network

open System.Net.Http


let HttpClient =
    let flags = System.Net.DecompressionMethods.GZip ||| System.Net.DecompressionMethods.Deflate

    let hch = new HttpClientHandler(AutomaticDecompression = flags)

    let hc = new HttpClient(hch)
    hc.DefaultRequestHeaders.Connection.Add("keep-alive")

    hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot")
    |> ignore

    hc
