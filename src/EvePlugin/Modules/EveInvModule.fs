namespace KPX.EvePlugin.Modules.EveInvModule

open System

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Utils.TextResponse

open KPX.TheBot.Host.DataModel.Recipe
open KPX.EvePlugin.Data.EveType

open KPX.EvePlugin.Utils.Config
open KPX.EvePlugin.Utils.UserInventory


/// 包装一个类型以实现强类型检查
type InvKeyOpt(cb) =
    // 默认值必须是不可能存在的值，比如空格
    inherit OptionCellSimple<string>(cb, "id", "\r\m")

type EveInvModule() =
    inherit CommandHandlerBase()

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

        for line in cmdArg.BodyLines do
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
        opt.Parse(cmdArg.HeaderArgs)

        let guid, acc = x.GetInv(keyOpt)

        acc.Clear()

        let materials =
            x.ReadCommandBody(cmdArg, acc).AsMaterials()
            |> Array.sortBy (fun i -> i.Item.MarketGroupId)

        cmdArg.Reply $"录入到： id:%s{guid}"

        TextTable(ForceImage) {
            "数据会保留到机器人重启前"

            [ CellBuilder() { literal "材料" }
              CellBuilder() {
                  literal "数量"
                  rightAlign
              } ]

            [ for m in materials do
                  [ CellBuilder() { literal m.Item.Name }
                    CellBuilder() { integer m.Quantity } ] ]

        }

    [<CommandHandlerMethod("#eveinvcal", "输入材料信息相加，然后与目标库存相减", "", IsHidden = true)>]
    member x.HandleInvCal(cmdArg : CommandEventArgs) =
        let opt = EveConfigParser()
        let keyOpt = opt.RegisterOption(InvKeyOpt(opt))
        opt.Parse(cmdArg.HeaderArgs)

        let _, inv = x.GetInv(keyOpt)
        let list = x.ReadCommandBody(cmdArg)

        TextTable() {
            let positiveOrPad (f : float) =
                if f > 0.0 then
                    CellBuilder() { integer f }
                else
                    CellBuilder() { rightPad }

            [ CellBuilder() { literal "物品" }
              CellBuilder() {
                  literal "数量"
                  rightAlign
              }
              CellBuilder() {
                  literal "已有"
                  rightAlign
              }
              CellBuilder() {
                  literal "缺少"
                  rightAlign
              } ]

            [ for m in list do
                  let had =
                      if inv.Contains(m.Item) then inv.Get(m.Item) else 0.0

                  let lack = m.Quantity - had

                  [ CellBuilder() { literal m.Item.Name }
                    CellBuilder() { integer m.Quantity }
                    CellBuilder() { literal (positiveOrPad had) }
                    CellBuilder() { literal (positiveOrPad lack) } ] ]
        }
