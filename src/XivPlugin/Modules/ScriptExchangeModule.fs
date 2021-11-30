namespace KPX.XivPlugin.Modules.ScriptExchangeModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Testing

open KPX.XivPlugin.Data
open KPX.XivPlugin.Data.Shops

open KPX.XivPlugin.Modules.Utils
open KPX.XivPlugin.Modules.Utils.MarketUtils


type ScriptExchangeModule() =
    inherit CommandHandlerBase()

    let itemCol = ItemCollection.Instance

    let universalis =
        UniversalisMarketCache.MarketInfoCollection.Instance

    let isNumber (str : string) =
        if str.Length <> 0 then
            String.forall Char.IsDigit str
        else
            false

    let strToItem (str : string) =
        if isNumber str then
            itemCol.TryGetByItemId(Convert.ToInt32(str))
        else
            itemCol.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    let sc = SpecialShopCollection.Instance

    member private x.ShowExchangeableItems() =
        TextTable(ForceImage) {
            let cols = 3

            CellBuilder() {
                literal "可交换道具："
                setBold
            }
            // 表头
            [ for _ = 1 to cols do
                  CellBuilder() {
                      literal "ID"
                      rightAlign
                  }

                  CellBuilder() { literal "名称" } ]

            [ for chunk in sc.AllCostItems()
                           |> Array.sortBy (fun item -> item.Id)
                           |> Array.chunkBySize cols do
                  [ for item in chunk do
                        CellBuilder() { literal item.Id }
                        CellBuilder() { literal item.Name } ] ]
        }

    member private x.ShowExchangeableProfit(cost : XivItem, world : World) =
        let ia = sc.SearchByCostItemId(cost.Id)

        if ia.Length = 0 then
            raise
            <| ModuleException(InputError, $"%s{cost.Name}不能兑换道具")

        TextTable(ForceImage) {
            $"兑换道具:%s{cost.Name} 土豆：%s{world.DataCenter}/%s{world.WorldName}"

            [ CellBuilder() { literal "兑换物品" }
              CellBuilder() {
                  literal "价格"
                  rightAlign
              }
              CellBuilder() {
                  literal "最低"
                  rightAlign
              }
              CellBuilder() {
                  literal "道具价值"
                  rightAlign
              }
              CellBuilder() {
                  literal "更新时间"
                  rightAlign
              } ]

            ia
            |> Array.map
                (fun info ->
                    let receive = itemCol.GetByItemId(info.ReceiveItem)

                    let market =
                        universalis
                            .GetMarketInfo(world, receive)
                            .GetListingAnalyzer()
                            .TakeVolume()

                    let updated = market.LastUpdateTime()

                    (updated,
                     [ CellBuilder() { literal receive.Name }
                       CellBuilder() { integer (market.StdEvPrice().Average) }
                       CellBuilder() { integer (market.MinPrice()) }
                       let costItemValue =
                           (market.StdEvPrice() * (float <| info.ReceiveCount)
                            / (float <| info.CostCount))
                               .Average

                       CellBuilder() { integer costItemValue }

                       if market.IsEmpty then
                           CellBuilder() { rightPad }
                       else
                           CellBuilder() { timeSpan updated } ]))
            |> Array.sortBy fst
            |> Array.map snd
        }


    [<CommandHandlerMethod("#ssc",
                           "计算部分道具兑换的价格",
                           "兑换所需道具的名称或ID，只处理1个
可以设置查询服务器，已有服务器见#ff14help")>]
    member x.HandleSSC(cmdArg : CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)


        if opt.NonOptionStrings.Count = 0 then
            x.ShowExchangeableItems()
        else
            let ret = strToItem opt.NonOptionStrings.[0]

            if ret.IsNone then
                cmdArg.Abort(InputError, $"找不到物品%s{opt.NonOptionStrings.[0]}")

            x.ShowExchangeableProfit(ret.Value, opt.World.Value)

    [<TestFixture>]
    member x.TestXivSSC() =
        let tc = TestContext(x)
        tc.ShouldNotThrow("#ssc")
        tc.ShouldNotThrow("#ssc 盐酸")