namespace KPX.TheBot.Module.AppShareConverter.ConverterModule

open System
open System.IO

open KPX.FsCqHttp.Message.Sections

open KPX.FsCqHttp.Handler

open KPX.TheBot.Data.Common.Network


type ConverterModule() =
    inherit HandlerModuleBase()

    let logOutput =
        let path = "../static/ShareAppLogger.log"
        let file = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read)
        new StreamWriter(file)

    let writeLock = obj ()

    let writeLog (sec : MessageSection) =
        lock
            writeLock
            (fun () ->
                logOutput.WriteLine("section : {0}", sec.TypeName)

                for kv in sec.Values do
                    logOutput.WriteLine("	 {0} = {1}", kv.Key, kv.Value)

                logOutput.WriteLine("----------------------------------")
                logOutput.Flush())


    override x.OnMessage = Some x.HandleMessage

    member x.HandleMessage(msg) =
        for sec in msg.Event.Message do 
            match sec with 
            | :? XmlSection as xml ->
                writeLog (sec)
                if msg.Event.IsPrivate then x.HandleXmlSection(msg, xml)
            | :? JsonSection as json -> 
                writeLog (sec)
                if msg.Event.IsPrivate then x.HandleJsonSection(msg, json)
            | _ -> ()

    member private x.ReplayMessage(e : CqMessageEventArgs, msg : string) = 
        if e.Event.IsPrivate then
            e.QuickMessageReply(msg)

    member private x.HandleXmlSection(_ : CqMessageEventArgs, _ : XmlSection) = 
        ()

    member private x.HandleJsonSection(msg : CqMessageEventArgs, json : JsonSection) = 
        let obj = json.GetObject()
        let appName = obj.GetValue("app").ToObject<string>()
        match appName with
        | "com.tencent.structmsg" ->
            let title = 
                obj.SelectToken("$.meta.news.title", false)
                |> Option.ofObj
                |> Option.map (fun token -> token.ToObject<string>())

            let url = 
                obj.SelectToken("$.meta.news.jumpUrl", false)
                |> Option.ofObj
                |> Option.map (fun token -> token.ToObject<string>())

            if title.IsSome && url.IsSome then
                x.ReplayMessage(msg, sprintf "%s\r%s" title.Value url.Value)

        | "com.tencent.miniapp_01" ->
            let prompt = obj.GetValue("prompt").ToObject<string>()
            match prompt with
            | "[QQ小程序]哔哩哔哩"
            | "[QQ小程序]热门微博" ->
                let desc =
                    obj.SelectToken("$.meta.detail_1.desc", false)
                    |> Option.ofObj
                    |> Option.map (fun token -> token.ToObject<string>())
                let qqUrl =
                    obj.SelectToken("$.meta.detail_1.qqdocurl", false)
                    |> Option.ofObj
                    |> Option.map (fun token -> token.ToObject<string>())
                
                if desc.IsSome && qqUrl.IsSome then
                    let url = qqUrl.Value.Substring(0, qqUrl.Value.IndexOf("?"))
                    x.ReplayMessage(msg, sprintf "%s\r%s" desc.Value url)
            | _ -> ()
        | _ -> ()

    interface IDisposable with
        member x.Dispose() = logOutput.Dispose()