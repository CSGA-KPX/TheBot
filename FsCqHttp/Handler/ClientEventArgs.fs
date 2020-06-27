namespace KPX.FsCqHttp.Handler

open System
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Text

open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Api

open Newtonsoft.Json.Linq

type ClientEventArgs(api : IApiCallProvider, obj : JObject) =
    inherit EventArgs()

    member val SelfId = obj.["self_id"].Value<uint64>()

    member val Event = Event.EventUnion.From(obj)

    member x.RawEvent = obj

    member x.ApiCaller = api

    /// 获取一个回复流
    /// 每次执行都是新对象
    member x.OpenResponse() = new TextResponse(x)

    /// 获取一个回复流，设置输出格式
    /// 每次执行都是新对象
    member x.OpenResponse(preferImage : bool) =
        let o = x.OpenResponse()
        o.PreferImageOutput <- preferImage
        o

    member x.SendResponse(r : Response.EventResponse) =
        if r <> Response.EmptyResponse then
            let rep = SystemApi.QuickOperation(obj.ToString(Newtonsoft.Json.Formatting.None))
            rep.Reply <- r
            api.CallApi(rep) |> ignore

    member x.QuickMessageReply(msg : Message.Message, ?atUser : bool) = 
        let atUser = defaultArg atUser false
        match x.Event with
        | _ when msg.ToString().Length > 3000 -> x.QuickMessageReply("字数太多了，请优化命令或者向管理员汇报bug", true)
        | Event.EventUnion.Message ctx ->
            match ctx with
            | _ when ctx.IsDiscuss -> x.SendResponse(Response.DiscusMessageResponse(msg, atUser))
            | _ when ctx.IsGroup -> x.SendResponse(Response.GroupMessageResponse(msg, atUser, false, false, false, 0))
            | _ when ctx.IsPrivate -> x.SendResponse(Response.PrivateMessageResponse(msg))
            | _ -> raise <| InvalidOperationException("")
        | _ -> raise <| InvalidOperationException("")

    member x.QuickMessageReply(msg : string, ?atUser : bool) =
        x.QuickMessageReply(Message.Message.TextMessage(msg.Trim()), defaultArg atUser false)

and TextResponse(arg : ClientEventArgs) = 
    inherit TextWriter()

    let mutable isUsed = false

    let sizeLimit = 2900
    let buf = Queue<string>()
    let sb = StringBuilder()
    
    member x.IsUsed = isUsed

    /// 获取或设置一个值，指示是否优先通过图像回复
    member val PreferImageOutput = false with get, set

    override x.Write(c:char) = 
        if not isUsed then
            isUsed <- true
        sb.Append(c) |> ignore

    override x.WriteLine() = 
        buf.Enqueue(sb.ToString())
        sb.Clear() |> ignore

    // .NET自带的是另一套算法，这里强制走Write(string)
    override x.WriteLine(str : string) = 
        x.Write(str)
        x.WriteLine()

    override x.Encoding = Encoding.Default

    override x.ToString() = 
        String.Join(x.NewLine, buf)

    member private x.FlushTextMessage() = 
        if sb.Length <> 0 then
            x.WriteLine()

        let pages = 
            [|
                let sb = StringBuilder()

                while buf.Count <> 0 do
                    let peek = buf.Dequeue()
                    if sb.Length + peek.Length > sizeLimit then
                        yield sb.ToString()
                        sb.Clear() |> ignore
                    sb.AppendLine(peek) |> ignore

                if sb.Length <> 0 then
                    yield sb.ToString()
            |]
        for page in pages do 
            arg.QuickMessageReply(page, false)

    member private x.CanSendImage() = 
        arg.ApiCaller.CallApi<SystemApi.CanSendImage>().Can

    member private x.FlushImageMessage() = 
        let DrawLines(lines : string []) = 
            let font = new Font("YaHei Mono", 10.0f)
            let fullSize = 
                use bmp = new Bitmap(1, 1)
                use draw= Graphics.FromImage(bmp)
                let calc =
                    lines
                    |> Array.map (fun str -> draw.MeasureString(str, font))
                let maxWidth = calc |> Array.maxBy (fun x -> x.Width)
                let sumHeight= calc |> Array.sumBy (fun x -> x.Height)
                SizeF(maxWidth.Width + 10.0f , sumHeight)

            let img = new Bitmap(int fullSize.Width, int fullSize.Height)
            use draw = Graphics.FromImage(img)
            use tb   = new SolidBrush(Color.Black)
            draw.Clear(Color.White)

            let mutable lineMode = false
            let mutable lastPos = PointF()
            for lineNo = 0 to lines.Length - 1 do 
                let str = lines.[lineNo]
                let bgColor = 
                    lineMode <- not lineMode
                    if lineMode then Brushes.White else Brushes.LightGray
                let line = draw.MeasureString(str, font)
                
                draw.FillRectangle(bgColor, RectangleF(lastPos, SizeF(fullSize.Width, line.Height)))

                draw.DrawString(lines.[lineNo], font, tb, PointF(lastPos.X, lastPos.Y))
                lastPos <- PointF(0.0f, lastPos.Y + line.Height)
            img
        
        let lines = 
            if sb.Length <> 0 then
                x.WriteLine()
            buf
            |> Seq.filter (fun line -> not <| String.IsNullOrWhiteSpace(line))
            |> Seq.toArray

        if lines.Length <> 0 then
            use img = DrawLines(lines)
            arg.QuickMessageReply(Message.Message.ImageMessage(img))

    override x.Flush() = 
        if x.PreferImageOutput && x.CanSendImage() then
            x.FlushImageMessage()
        else
            x.FlushTextMessage()

    interface IDisposable with
        member x.Dispose() = 
            base.Dispose()
            if isUsed then
                x.Flush()
            