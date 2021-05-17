namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Text
open System.Text.RegularExpressions

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Api.System

open KPX.FsCqHttp.Handler


[<Sealed>]
type internal ImageHelper private () =

    /// 调整字符显示宽度。如果IsMatch=true则认为是1栏宽
    ///
    /// 需要设置RegexOptions.Compiled
    static let CharDisplayLengthAdj =
        Regex(@"\p{IsBasicLatin}|\p{IsGeneralPunctuation}|±|·", RegexOptions.Compiled)

    static let mutable tmpImg = Graphics.FromImage(new Bitmap(1, 1))

    static member val Font =
        new Font(
            KPX.FsCqHttp.Config.Output.ImageOutputFont,
            KPX.FsCqHttp.Config.Output.ImageOutputSize
        )

    static member val StringFormat =
        let sf =
            // 请勿使用StringFormat.GenericDefault
            new StringFormat(StringFormat.GenericTypographic)

        sf.FormatFlags <-
            sf.FormatFlags
            ||| StringFormatFlags.MeasureTrailingSpaces

        sf

    static member val private SingleColumnWidth =
        let str =
            string KPX.FsCqHttp.Config.Output.TextTable.FullWidthSpace

        let ret = ImageHelper.MeasureByGraphic(str)
        (int ret.Width) / 2

    static member MeasureByChar(str : string) =
        str.ToCharArray()
        |> Array.sumBy (fun c -> if CharDisplayLengthAdj.IsMatch(c.ToString()) then 1 else 2)

    static member MeasureByGraphic(str : string) : SizeF =
        tmpImg.MeasureString(str, ImageHelper.Font, 0, ImageHelper.StringFormat)

    /// 使用Output.TextTable.UseGraphicStringMeasure计算宽度。
    /// 返回宽度按栏数计算。
    static member MeasureWidthByConfig(str : string) =
        if KPX.FsCqHttp.Config.Output.TextTable.UseGraphicStringMeasure then
            let ret =
                ImageHelper.MeasureByGraphic(str).Width |> int

            let mutable width = ret / ImageHelper.SingleColumnWidth

            if (ret % ImageHelper.SingleColumnWidth) <> 0 then
                width <- width + 1

            if width = 0 && str.Length <> 0 then
                // 可能是上游Cairo和libgdiplus的bug
                // 对于含有颜文字的文字会计算错误
                // 需要用老式方法计算并且替换graphic
                width <- ImageHelper.MeasureByChar(str)
                tmpImg <- Graphics.FromImage(new Bitmap(1, 1))

            //printfn ">%s< -> %i" str width
            width
        else
            ImageHelper.MeasureByChar(str)

type ResponseType =
    | ForceImage
    | PreferImage
    | ForceText

type TextResponse(args : CqMessageEventArgs, respType : ResponseType) =
    inherit TextWriter()

    let mutable isUsed = false

    let sizeLimit =
        KPX.FsCqHttp.Config.Output.TextLengthLimit - 100

    let buf = Queue<string>()
    let sb = StringBuilder()

    let canSendImage =
        lazy (args.ApiCaller.CallApi<CanSendImage>().Can)

    member x.DoSendAsImage =
        match respType with
        | ForceText -> false
        | PreferImage when KPX.FsCqHttp.Config.Output.ForceImageAvailable -> true
        | PreferImage -> canSendImage.Force()
        | ForceImage -> true

    override x.NewLine = KPX.FsCqHttp.Config.Output.NewLine

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
               let newline = KPX.FsCqHttp.Config.Output.NewLine

               while buf.Count <> 0 do
                   let line = buf.Dequeue()
                   page.Add(line)
                   pageSize <- pageSize + line.Length

                   if pageSize > sizeLimit then
                       yield String.Join(newline, page)
                       page.Clear()
                       pageSize <- 0

               if page.Count <> 0 then yield String.Join(newline, page) |]

        for page in pages do
            args.Reply(page)

    member private x.FlushImageMessage() =
        let DrawLines (lines : string []) =
            let font = ImageHelper.Font
            let sf = ImageHelper.StringFormat

            let fullSize =
                let calc =
                    lines |> Array.map ImageHelper.MeasureByGraphic

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
            args.Reply(message)

    override x.Flush() =
        if x.DoSendAsImage then
            x.FlushImageMessage()
        else
            x.FlushTextMessage()

    interface IDisposable with
        member x.Dispose() =
            base.Dispose()
            if isUsed then x.Flush()