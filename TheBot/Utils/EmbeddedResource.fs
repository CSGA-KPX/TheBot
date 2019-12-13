module TheBot.Utils.EmbeddedResource

open System
open System.Reflection

let GetResourceManager(str) = 
    Resources.ResourceManager("TheBot."+str, Assembly.GetExecutingAssembly())