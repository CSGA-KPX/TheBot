module KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Text

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Handler

type ResponseType =
    | ForceImage
    | PreferImage
    | ForceText

    member internal x.CanSendImage(args : CqEventArgs) =
        let canSendImage =
            lazy (args.ApiCaller.CallApi<CanSendImage>().Can)

        match x with
        | ForceImage ->
            if not canSendImage.Value then raise <| InvalidOperationException("")
            true
        | ForceText -> false
        | PreferImage -> canSendImage.Value

type TextResponse(args : CqEventArgs, respType : ResponseType) =
    inherit TextWriter()

    let mutable isUsed = false

    let sizeLimit =
        KPX.FsCqHttp.Config.Output.TextLengthLimit - 100

    let buf = Queue<string>()
    let sb = StringBuilder()

    let resp = lazy (respType.CanSendImage(args))

    member x.DoSendImage = resp.Force()

    override x.Write(c : char) =
        if not isUsed then isUsed <- true
        sb.Append(c) |> ignore

    override x.WriteLine() =
        buf.Enqueue(sb.ToString())
        sb.Clear() |> ignore

    // .NET自带的是另一套算法，这里强制走Write(string)
    override x.WriteLine(str : string) =
        x.Write(str)
        x.WriteLine()

    override x.Encoding = Encoding.Default

    override x.ToString() = String.Join(x.NewLine, buf)

    [<Obsolete>] /// F#使用此指令容易出现错误，已禁用
    override x.Write(_ : obj) =
        invalidOp<unit> "已禁用Write(object)，请手动调用Write(object.ToString())。"

    [<Obsolete>] /// F#使用此指令容易出现错误，已禁用
    override x.WriteLine(_ : obj) =
        invalidOp<unit> "已禁用WriteLine(object)，请手动调用WriteLine(object.ToString())。"

    /// 中断执行过程，中断文本输出
    member x.AbortExecution(level : ErrorLevel, fmt : string, [<ParamArray>] fmtargs : obj []) =
        x.WriteLine()
        buf.Clear()
        isUsed <- false
        args.AbortExecution(level, fmt, fmtargs)

    member x.WriteEmptyLine() = x.WriteLine("\u3000")

    member private x.FlushTextMessage() =
        if sb.Length <> 0 then x.WriteLine()

        let pages =
            [| let mutable pageSize = 0
               let page = List<string>()

               while buf.Count <> 0 do
                   let line = buf.Dequeue()
                   page.Add(line)
                   pageSize <- pageSize + line.Length

                   if pageSize > sizeLimit then
                       yield String.Join("\r\n", page)
                       page.Clear()
                       pageSize <- 0

               if page.Count <> 0 then yield String.Join("\r\n", page) |]

        for page in pages do
            args.QuickMessageReply(page)

    member private x.FlushImageMessage() =
        let DrawLines (lines : string []) =
            use font =
                new Font(KPX.FsCqHttp.Config.Output.ImageOutputFont, KPX.FsCqHttp.Config.Output.ImageOutputSize)

            let sf =
                new StringFormat(StringFormat.GenericTypographic)

            sf.FormatFlags <-
                sf.FormatFlags
                ||| StringFormatFlags.MeasureTrailingSpaces

            let fullSize =
                use bmp = new Bitmap(1, 1)
                use draw = Graphics.FromImage(bmp)

                let calc =
                    lines
                    |> Array.map (fun str -> draw.MeasureString(str, font, 0, sf))

                let maxWidth = calc |> Array.maxBy (fun x -> x.Width)
                let sumHeight = calc |> Array.sumBy (fun x -> x.Height)
                SizeF(maxWidth.Width + 10.0f, sumHeight)

            let img =
                new Bitmap(int fullSize.Width, int fullSize.Height)

            use draw =
                Graphics.FromImage(img, TextRenderingHint = Text.TextRenderingHint.ClearTypeGridFit)

            use tb = new SolidBrush(Color.Black)
            draw.Clear(Color.White)

            let mutable lineMode = false
            let mutable lastPos = PointF()

            for str in lines do
                let bgColor =
                    lineMode <- not lineMode
                    if lineMode then Brushes.White else Brushes.LightGray

                let line = draw.MeasureString(str, font, 0, sf)

                draw.FillRectangle(bgColor, RectangleF(lastPos, SizeF(fullSize.Width, line.Height)))
                draw.DrawString(str, font, tb, PointF(lastPos.X, lastPos.Y), sf)
                lastPos <- PointF(0.0f, lastPos.Y + line.Height)

            img

        let lines =
            if sb.Length <> 0 then x.WriteLine()

            buf
            |> Seq.filter (fun line -> not <| String.IsNullOrEmpty(line))
            |> Seq.toArray

        if lines.Length <> 0 then
            use img = DrawLines(lines)
            let message = Message()
            message.Add(img)
            args.QuickMessageReply(message)

    override x.Flush() =
        if x.DoSendImage then x.FlushImageMessage() else x.FlushTextMessage()

    interface IDisposable with
        member x.Dispose() =
            base.Dispose()
            if isUsed then x.Flush()

type CqEventArgs with

    /// 生成一个强制文本输出的TextResponse
    member x.OpenResponse() = new TextResponse(x, ForceText)

    /// 生成一个按指定ResponseType输出的TextResponse
    member x.OpenResponse(respType : ResponseType) = new TextResponse(x, respType)
