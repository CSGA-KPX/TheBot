[<AutoOpen>]
module KPX.FsCqHttp.Utils.TextTable.Extensions


type KPX.FsCqHttp.Utils.TextResponse.TextResponse with
    member x.Write(tt : TextTable) =
        if x.DoSendImage then tt.ColumnPaddingChar <- ' '

        for line in tt.ToLines() do
            x.WriteLine(line)
