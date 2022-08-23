namespace rec KPX.FsCqHttp.Utils.TextResponse

open System
open System.Collections.Generic
open System.Text

open KPX.FsCqHttp
open KPX.FsCqHttp.Utils.TextResponse.Internals


type TextTable(?retType: ResponseType) =
    let items = ResizeArray<TableItem>()

    let dParams = new DrawParameters()

    member val PreferResponseType = defaultArg retType PreferImage with get, set

    member x.Clear() = items.Clear()

    member x.Count = items.Count

    member x.Items = items :> IReadOnlyList<_>

    // yield  string          一行单列
    // yield  TableCell       一行单列

    // yield seq<seq<TableCell>> 多行多列
    // yield seq<TableCell []>   多行多列
    // yield seq<TableCell list> 多行多列

    member x.Yield(tableItem: TableItem) =
        items.Add(tableItem)
        x

    member x.Yield(tableItems: TableItem list) =
        items.AddRange(tableItems)
        x

    /// 添加单行单列
    member x.Yield(line: string) =
        if line.Contains('\n') then
            invalidOp "TableCell不允许包含多行文本，请使用相关指令拆分"

        items.Add(TableItem.Line <| TableCell(line))
        x

    /// 添加单行单列
    member x.Yield(cell: TableCell) =
        items.Add(TableItem.Line cell)
        x

    /// 多行多列
    member x.Yield(rows: seq<seq<TableCell>>) =
        for row in rows do
            items.Add(row |> Seq.toArray |> TableItem.Row)

        x

    /// 多行多列
    member x.Yield(rows: seq<TableCell list>) =
        for row in rows do
            items.Add(row |> Seq.toArray |> TableItem.Row)

        x

    /// 多行多列
    member x.Yield(rows: seq<TableCell []>) =
        for row in rows do
            items.Add(row |> Seq.toArray |> TableItem.Row)

        x

    /// 添加子表格
    member x.Yield(table: TextTable) =
        items.Add(TableItem.Table(table.Items))
        x

    member x.Yield(_: unit) = x

    member x.Zero() = x

    member x.Combine(_, _) = x

    member x.Delay(func) = func ()

    (*
    // 原因不明用不了
    [<CustomOperation("forceText")>]
    member _.ForceText(x : TextTable) =
        x.PreferResponseType <- ForceText
        x

    [<CustomOperation("preferImage")>]
    member _.PreferImage(x : TextTable) =
        x.PreferResponseType <- PreferImage
        x

    [<CustomOperation("forceImage")>]
    member _.ForceImage(x : TextTable) =
        x.PreferResponseType <- ForceImage
        x
    *)

    member x.Response(args) =
        (x :> Handler.ICommandResponse).Response(args)

    interface Handler.ICommandResponse with
        member x.Response(args) =
            let canSendImage =
                lazy
                    (args
                        .ApiCaller
                        .CallApi<Api.System.CanSendImage>()
                        .Can)

            let returnImage =
                match x.PreferResponseType with
                | ForceText -> false
                | PreferImage when Config.ImageIgnoreSendCheck -> true
                | PreferImage -> canSendImage.Force()
                | ForceImage -> true

            if x.Count <> 0 then
                if returnImage then
                    let message = Message.Message()
                    use render = new TableImageRender(items, dParams)
                    use img = render.RenderImage()
                    message.Add(img)
                    args.Reply(message)
                else
                    let sizeLimit = Config.TextLengthLimit - 100
                    let sb = StringBuilder()
                    let render = TableTextRender(items, dParams)

                    for line in render.GetLines() do
                        sb.AppendLine(line) |> ignore

                        if sb.Length > sizeLimit then
                            // 删除结尾换行符
                            sb.Length <- sb.Length - Environment.NewLine.Length
                            args.Reply(sb.ToString())
                            sb.Clear() |> ignore

                    if sb.Length <> 0 then
                        // 删除结尾换行符
                        sb.Length <- sb.Length - Environment.NewLine.Length
                        args.Reply(sb.ToString())
