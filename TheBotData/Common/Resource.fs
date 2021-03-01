module KPX.TheBot.Data.Common.Resource

open System
open System.Reflection


let StaticDataPath = "../staticData/"

let private ResourcePrefix = "TheBotData."

let GetResourceManager (str) =
    Resources.ResourceManager(ResourcePrefix + str, Assembly.GetExecutingAssembly())
