module BotData.Common.Network

open System.Net.Http

let hc = 
    let hc = new HttpClient()
    hc.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "https://github.com/CSGA-KPX/TheBot") |> ignore
    hc