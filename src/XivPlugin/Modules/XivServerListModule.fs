namespace KPX.XivPlugin.Modules.ServerListModule

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.XivPlugin.Data


type ServerListModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod("#ffsrv", "检查Bot可用的FF14服务器/大区名称", "")>]
    member x.HandleFFCmdHelp(_: CommandEventArgs) =
        TextTable(ForceImage) {
            let colsNum = 5 // 每行5个名字

            Literal "陆行鸟特指Choboco服务器，查询大区请使用鸟区/陆行鸟区/鸟" { bold }
            Literal "可用大区及缩写有：" { bold }

            [ for chunk in World.DataCenters |> Seq.chunkBySize colsNum do
                  chunk |> Array.map Literal ]

            Literal "可用服务器及缩写有：" { bold }

            [ for chunk in World.WorldNames |> Seq.chunkBySize colsNum do
                  chunk |> Array.map Literal ]

        }

    [<TestFixture>]
    member x.TestFFSrv() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#ffsrv")
