module KPX.TheBot.Data.Common.Resource

open System
open System.IO
open System.Reflection


[<Literal>]
let StaticDataPath = "../staticData/"

[<Literal>]
let StaticFilePath = "../static/"

[<Literal>]
let private ResourcePrefix = "TheBotData."

[<Literal>]
let XivTPSample = __SOURCE_DIRECTORY__ + "/../../../build/staticData/ffxiv-datamining-cn-master.zip"

let GetResourceManager (str) =
    Resources.ResourceManager(ResourcePrefix + str, Assembly.GetExecutingAssembly())

let GetStaticFile(args : string) =
    Path.Join(StaticFilePath.AsSpan(), args.AsSpan())