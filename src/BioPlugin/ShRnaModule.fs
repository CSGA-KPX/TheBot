namespace KPX.BioPlugin.ShRnaModule

open System
open System.Text.RegularExpressions

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Testing
open KPX.FsCqHttp.Utils.TextResponse

open KPX.BioPlugin


type EveLpStoreModule() =
    inherit CommandHandlerBase()

    let shrna = Data.ShRnaCollection.Instance

    let trcnRegex = Regex("^TRCN[0-9]+$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    [<CommandHandlerMethod("#shrna", "根据TRCN号生成shrna引物序列", "")>]
    member x.ShowSearchShRna(cmdArg: CommandEventArgs) =
        let trcnIds =
            seq {
                for line in cmdArg.AllLines do
                    yield! line.Split() |> Array.filter (fun chunk -> trcnRegex.IsMatch(chunk))
            }
            |> Seq.cache

        use ret = cmdArg.OpenResponse(ForceText)

        ret.Table {
            [ for id in trcnIds do
                  let ret = shrna.TryFindByTRCNId(id)

                  if ret.IsSome then
                      let oligo = ret.Value.GenerateOligo()
                      [ Literal $"{id}-F"; Literal oligo.Forward ]
                      [ Literal $"{id}-R"; Literal oligo.Reverse ]
                  else
                      [ Literal $"未找到{id}" ] ]
        }
        |> ignore
