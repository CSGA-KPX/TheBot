module KPX.TheBot.Data.Common.Resource

open System
open System.Reflection


[<Literal>]
let StaticDataPath = "../staticData/"

[<Literal>]
let private ResourcePrefix = "TheBotData."

[<Literal>]
let XivTPSample = __SOURCE_DIRECTORY__ + "/../../TheBot/bin/staticData/ffxiv-datamining-cn-master.zip"

let GetResourceManager (str) =
    Resources.ResourceManager(ResourcePrefix + str, Assembly.GetExecutingAssembly())
