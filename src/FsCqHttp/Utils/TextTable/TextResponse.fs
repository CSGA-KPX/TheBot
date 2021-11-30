namespace KPX.FsCqHttp.Utils.TextResponse

open System
open System.IO
open System.Text

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler

/// 对TextTable的简单包装。提供自动输出，错误控制等功能
type TextResponse(args: CqMessageEventArgs, respType: ResponseType) =
    inherit TextWriter()

    let tt = TextTable(PreferResponseType = respType)

    let sb = StringBuilder()

    member x.Table = tt

    override x.NewLine = Config.NewLine

    override x.Encoding = Encoding.Default

    override x.Write(c: char) = sb.Append(c) |> ignore

    /// 添加空行
    override x.WriteLine() = tt.Yield(String.Empty) |> ignore

    override x.Write(str: string) =
        for line in str.Split([| "\r\n"; "\r"; "\n" |], StringSplitOptions.None) do
            tt.Yield(line) |> ignore

    // .NET自带的是另一套算法，这里强制走Write(string)
    override x.WriteLine(str: string) =
        x.Write(str)
        x.CommitCurrentLine()

    [<Obsolete>] /// F#使用此指令容易出现错误，已禁用
    override x.Write(_: obj) =
        invalidOp<unit> "已禁用Write(object)，请手动调用Write(object.ToString())。"

    [<Obsolete>] /// F#使用此指令容易出现错误，已禁用
    override x.WriteLine(_: obj) =
        invalidOp<unit> "已禁用WriteLine(object)，请手动调用WriteLine(object.ToString())。"

    member private x.CommitCurrentLine() =
        if sb.Length <> 0 then
            tt.Yield(sb.ToString()) |> ignore
            sb.Clear() |> ignore

    [<Obsolete>]
    /// 如果只输出表格请直接使用TextTable或TextResponse.Table
    member x.Write(table: TextTable) =
        x.CommitCurrentLine()
        tt.Yield(table) |> ignore

    /// 中断执行过程，中断文本输出
    member x.Abort(level: ErrorLevel, fmt: string, [<ParamArray>] fmtArgs: obj []) =
        x.CommitCurrentLine()
        tt.Clear()
        args.Abort(level, fmt, fmtArgs)

    override x.Flush() = x.CommitCurrentLine()

    member x.Response() = (x :> ICommandResponse).Response(args)

    interface ICommandResponse with
        member x.Response(args) = (tt :> ICommandResponse).Response(args)

    interface IDisposable with
        member x.Dispose() =
            base.Dispose()
            x.Response()
