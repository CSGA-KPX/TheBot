module TheBot.Utils.EmbeddedResource

open System
open System.Reflection

let GetResourceManager (str) =
    Resources.ResourceManager("TheBot.Resources." + str, Assembly.GetExecutingAssembly())

let GetResFileStream (filename) =
    let resName = "TheBot.Resources." + filename
    let assembly = Assembly.GetExecutingAssembly()
    assembly.GetManifestResourceStream(resName)
