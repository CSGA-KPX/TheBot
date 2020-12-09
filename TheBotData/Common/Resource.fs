module BotData.Common.Resource
// 用于资源文件的访问

open System
open System.Reflection
open System.IO.Compression

let GetResourceStream(resName) = 
    let assembly = Assembly.GetExecutingAssembly()
    assembly.GetManifestResourceStream(resName)

let GetResZipFile(resName, fileName) = 
    let stream = GetResourceStream(resName)
    let archive = new ZipArchive(stream, ZipArchiveMode.Read)
    archive.GetEntry(fileName).Open()

let GetResourceManager(str) = 
    Resources.ResourceManager("BotData."+str, Assembly.GetExecutingAssembly())