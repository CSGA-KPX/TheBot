module KPX.TheBot.Data.Common.Resource

open System
open System.Reflection


[<Literal>]
let StaticDataPath = "../staticData/"

[<Literal>]
let private ResourcePrefix = "TheBotData."

let GetResourceManager (str) =
    Resources.ResourceManager(ResourcePrefix + str, Assembly.GetExecutingAssembly())
