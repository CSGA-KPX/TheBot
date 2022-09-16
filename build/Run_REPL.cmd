(*
@echo off
call ZConfig.cmd
pushd bin
cls
dotnet fsi --use:"%~f0"
goto :EOF
*)

// DO NOT USE NON-ASCII CHARS!

#I "bin/"
#I @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\6.0.8\"

#r "Newtonsoft.Json.dll"
#r "McMaster.NETCore.Plugins.dll"
#r "NLog.dll"
#r "FsCqHttp.dll"
#r "TheBot.dll"
#r "SkiaSharp.dll"

//#r "System.Windows.Forms"
//#r "System.Windows.Forms.Primitives.dll"
//#r "System.Drawing.Common"


open System
open System.IO
open System.Reflection
open System.Diagnostics
open System.Collections.Generic
open System.Data
open System.Drawing
open System.Linq
open System.Text
open System.Threading.Tasks
//open System.Windows.Forms

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance
open KPX.FsCqHttp.Testing

open KPX.TheBot.Host
open KPX.TheBot.Host.Data

let discover =
    let binPath = __SOURCE_DIRECTORY__ + "/bin/"

    NLog.LogManager.LoadConfiguration(binPath + "NLog.config")
    |> ignore

    Environment.CurrentDirectory <- Path.Join(binPath)

    let discover = HostedModuleDiscover()
    discover.ScanPlugins(IO.Path.Combine(binPath, "plugins"))
    discover.ScanAssembly(Assembly.GetExecutingAssembly())
    discover.ScanAssembly(typeof<KPX.TheBot.Host.Data.DataAgent>.Assembly)
    discover.AddModule(KPX.TheBot.Module.DataCacheModule.DataCacheModule(discover))

    discover

let ctx = 
    let userId = 
        Environment.GetEnvironmentVariable("REPL_UserId")
        |> Option.ofObj<string>
        |> Option.map uint64
        |> Option.defaultValue 10000UL
        |> UserId
        
    let userName = 
        Environment.GetEnvironmentVariable("REPL_UserName")
        |> Option.ofObj<string>
        |> Option.defaultValue "²âÊÔ»ú"
        
    TestContext(discover, userId, userName)

let logger = NLog.LogManager.GetLogger("REPL")

while true do
    printf "Command> "
    let text = Console.ReadLine()
    let msg = ctx.InvokeCommand(text)

    for seg in msg do
        if seg.TypeName = "text" then
            Console.Out.Write(seg.Values.["text"])
        else
            Console.Out.Write($"[{seg.TypeName}]")
            
    Console.WriteLine()

    let imgs = msg.GetSections<Message.Sections.ImageSection>()

    for img in imgs do
        if img.File.StartsWith("base64") then
            let bin = Convert.FromBase64String(img.File.[img.File.IndexOf("//") + 2 ..])
            let tmp = Path.GetTempFileName()
            File.WriteAllBytes(tmp, bin)

            let psi = ProcessStartInfo("snipaste")
            psi.ArgumentList.Add("paste")
            psi.ArgumentList.Add("--files")
            psi.ArgumentList.Add(tmp)
            Process.Start(psi) |> ignore
        else
            logger.Fatal($"Invalid image:{img.File}")