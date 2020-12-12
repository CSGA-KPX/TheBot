module BotData.Common.Resource
// 用于资源文件的访问

open System
open System.Reflection
open System.IO.Compression

let private ResourcePrefix = "TheBotData."

let __GetResourceStream(resName) = 
    let assembly = Assembly.GetExecutingAssembly()
    assembly.GetManifestResourceStream(resName)

let __GetResZipFile(resName, fileName) = 
    let stream = __GetResourceStream(resName)
    let archive = new ZipArchive(stream, ZipArchiveMode.Read)
    archive.GetEntry(fileName).Open()

let GetResourceManager(str) = 
    Resources.ResourceManager(ResourcePrefix+str, Assembly.GetExecutingAssembly())

