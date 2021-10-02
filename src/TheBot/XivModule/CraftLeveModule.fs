namespace KPX.TheBot.Module.XivModule.MarketModule

open System

open KPX.FsCqHttp.Handler

open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.XivData
open KPX.TheBot.Data.XivData.Shops

open KPX.TheBot.Module.XivModule.Utils


(*
[<AbstractClass>]
type XivMarketModule() =
    inherit CommandHandlerBase()

    let rm = Recipe.XivRecipeManager.Instance
    let itemCol = ItemCollection.Instance
    let gilShop = GilShopCollection.Instance
    let xivExpr = XivExpression.XivExpression()

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

    /// 给物品名备注上NPC价格
    let tryLookupNpcPrice (item : XivItem) =
        let ret = gilShop.TryLookupByItem(item)

        if ret.IsSome then
            $"%s{item.Name}(%i{ret.Value.Ask})"
        else
            item.Name

    /// 没写完！
    [<CommandHandlerMethod("#理符", "计算制作理符利润（只查询70级以上的基础材料）", "#理符 [职业名] [服务器名]", Disabled = true)>]
    member x.HandleCraftLeve(cmdArg : CommandEventArgs) =
        let opt = CommandUtils.XivOption()
        opt.Parse(cmdArg.HeaderArgs)

        let leves =
            opt.NonOptionStrings
            |> Seq.tryHead
            |> Option.map ClassJobMapping.ClassJobMappingCollection.Instance.TrySearchByName
            |> Option.flatten
            |> Option.map CraftLeve.CraftLeveInfoCollection.Instance.GetByClassJob

        if leves.IsNone then cmdArg.Abort(InputError, "未设置职业或职业无效")

        let leves =
            leves.Value
            |> Array.filter (fun leve -> leve.Level >= 60)
            |> Array.sortBy (fun leve -> leve.Level)

        //let tt =TextTable("名称", "等级", "制作价格", "金币奖励", "利润率", "最旧更新")

        for leve in leves do
            for item in leve.Items do
                let quantity = ByItem item.Quantity
                let item = itemCol.GetByItemId(item.Item)
                // 生产理符都能搓
                let materials = rm.TryGetRecipeRec(item, quantity).Value
                materials |> ignore //屏蔽警告用
                ()

            ()

    [<TestFixture>]
    member x.TestXivLeveCraft() = ()
*)
