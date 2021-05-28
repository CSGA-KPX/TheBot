namespace TheBot.EVEModule.Modules.EveInvModule

open System

open KPX.FsCqHttp.Handler
//open KPX.FsCqHttp.Testing

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

//open KPX.TheBot.Data.EveData
open KPX.TheBot.Data.CommonModule.Recipe
open KPX.TheBot.Data.EveData.EveType

open KPX.TheBot.Module.EveModule.Utils.Config
open KPX.TheBot.Module.EveModule.Utils.UserInventory


/// 包装一个类型以实现强类型检查
type InvKeyOpt(cb) =
    // 默认值必须是不可能存在的值，比如空格
    inherit OptionCellSimple<string>(cb, "id", "\r\m")

type EveInvModule() =
    inherit CommandHandlerBase()
    
    let PositiveOrPad (f : float) =
        if f > 0.0 then HumanReadableInteger f else PaddingRight
        
    /// 返回库存信息 id*库存
    member private x.GetInv(keyOpt : InvKeyOpt) =
        let i = InventoryCollection.Instance

        if keyOpt.IsDefined then
            let key = keyOpt.Value
            let ret = i.TryGet(key)
            if ret.IsSome then ret.Value else i.Create(key)
        else
            i.Create()

    /// 解析消息正文，生产材料信息
    member private x.ReadCommandBody(cmdArg : CommandEventArgs, ?acc : _) =
        let ns =
            Globalization.NumberStyles.AllowThousands

        let ic =
            Globalization.CultureInfo.InvariantCulture

        let acc =
            defaultArg acc (ItemAccumulator<EveType>())

        for line in cmdArg.MsgBodyLines do
            match line.Split("	", 2, StringSplitOptions.RemoveEmptyEntries) with
            | [||] -> () // 忽略空行
            | [| name |] -> acc.Update(EveTypeCollection.Instance.GetByName(name))
            | [| name; q |] ->
                let succ, quantity = Int32.TryParse(q, ns, ic)
                if not succ then cmdArg.Abort(InputError, "格式非法，{0}不是数字", q)

                let item =
                    EveTypeCollection.Instance.GetByName(name)

                acc.Update(item, quantity |> float)
            | _ -> cmdArg.Abort(InputError, "格式非法，应为'道具名	数量'")

        acc

    [<CommandHandlerMethod("#eveinv", "记录材料数据供#er和#err使用", "", IsHidden = true)>]
    member x.HandleEveInv(cmdArg : CommandEventArgs) =
        let opt = EveConfigParser()
        let keyOpt = opt.RegisterOption(InvKeyOpt(opt))
        opt.Parse(cmdArg)

        let guid, acc = x.GetInv(keyOpt)

        acc.Clear()

        let tt = TextTable("材料", RightAlignCell "数量")

        let materials =
            x.ReadCommandBody(cmdArg, acc).AsMaterials()
            |> Array.sortBy (fun i -> i.Item.MarketGroupId)

        for m in materials do
            tt.AddRow(m.Item.Name, HumanReadableInteger m.Quantity)

        tt.AddPreTable("此前数据已经清除")
        tt.AddPreTable("会保留到下次机器人重启前")
        cmdArg.Reply $"录入到： id:%s{guid}"
        using (cmdArg.OpenResponse(opt.ResponseType)) (fun ret -> ret.Write(tt))

    [<CommandHandlerMethod("#eveinvcal", "输入材料信息相加，然后与目标库存相减", "", IsHidden = true)>]
    member x.HandleInvCal(cmdArg : CommandEventArgs) =
        let opt = EveConfigParser()
        let keyOpt = opt.RegisterOption(InvKeyOpt(opt))
        opt.Parse(cmdArg)

        let _, inv = x.GetInv(keyOpt)
        let list = x.ReadCommandBody(cmdArg)

        let tt =
            TextTable("物品", RightAlignCell "数量", RightAlignCell "已有", RightAlignCell "缺少")

        for m in list do
            let had =
                if inv.Contains(m.Item) then
                    inv.Get(m.Item)
                else
                    0.0

            let lack = m.Quantity - had
            let hadStr = PositiveOrPad had
            let lackStr = PositiveOrPad lack
            
            tt.AddRow(m.Item.Name, HumanReadableInteger m.Quantity, hadStr, lackStr)

        using (cmdArg.OpenResponse(opt.ResponseType)) (fun ret -> ret.Write(tt))
