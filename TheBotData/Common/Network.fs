module BotData.Common.Network

open System.Net.Http

let hc =
    let flags =
        System.Net.DecompressionMethods.GZip
        ||| System.Net.DecompressionMethods.Deflate

    let hch =
        new HttpClientHandler(AutomaticDecompression = flags)

    let hc = new HttpClient(hch)
    hc.DefaultRequestHeaders.Connection.Add("keep-alive")

    hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot")
    |> ignore

    hc
