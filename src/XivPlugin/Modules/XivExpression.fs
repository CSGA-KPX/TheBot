module KPX.XivPlugin.Modules.Utils.XivExpression

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open KPX.XivPlugin.Data

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.GenericRPN
open KPX.TheBot.Host.Utils.RecipeRPN


type ItemAccumulator = ItemAccumulator<XivItem>

type private SubmarineInfo =
    { Item: XivItem
      PartRank: int
      ClassName: string
      FilterGroup: int
      Slot: int }

type private AtExpression private () as x =

    let parsers = ResizeArray<string * string -> ItemAccumulator option>()

    let submarineDict = Dictionary<string, XivItem>()

    let submarineRegex =
        let pattern = "(?<part>[一-九0-9]改?){4}"
        Regex(pattern, RegexOptions.Compiled)

    do
        parsers.Add(x.ParseGearSet)
        parsers.Add(x.ParseSubmarine)

    static member val Instance = AtExpression()

    member private x.ParseGearSet(job, iLv) =
        let jobs = HashSet<ClassJob>()

        match ClassJob.TryParse(job) with
        | Some (job) -> jobs.Add(job) |> ignore
        | _ ->
            let func = (fun map -> map.Code) >> jobs.Add >> ignore

            match job with
            | "生产" -> ClassJob.CraftJobs |> Seq.iter func
            | "采集" -> ClassJob.GatherJobs |> Seq.iter func
            | "生产采集"
            | "生活"
            | "生采" -> ClassJob.CraftGatherJobs |> Seq.iter func
            | _ -> ()

        if jobs.Count <> 0 then
            let acu = ItemAccumulator()
            let cgc = CraftableGearCollection.Instance
            let iLevel = int iLv

            for job in jobs do
                let gears = cgc.Search(iLevel, job)

                if gears.Length = 0 then
                    failwith $"不存在指定的装备 {job}@{iLevel}"

                for (g, q) in gears do
                    let item = ItemCollection.Instance.GetByItemId(g.ItemId)
                    acu.[item] <- q

            Some acu
        else
            None

    member private x.BuildSubmarineData() =
        let col = ChinaDistroData.GetCollection()
        let subPart = col.SubmarinePart

        let submarineItems =
            col.Item
            |> Seq.filter (fun row -> row.FilterGroup.AsInt() = 36)
            |> Seq.map (fun item ->
                let p = subPart.[item.AdditionalData.AsInt()]

                { Item = ItemCollection.Instance.GetByItemId(item.Key.Main)
                  ClassName =
                    let itemName = item.Name.AsString()
                    itemName.Remove(itemName.IndexOf('级'))
                  PartRank = p.Rank.AsInt()
                  FilterGroup = 36
                  Slot = p.Slot.AsInt() })
            |> Seq.toArray
            |> Array.groupBy (fun info -> info.Slot)

        let classDict =
            let classAcc = Dictionary<string, string>()
            let items = submarineItems |> Array.head |> snd

            items
            |> Array.filter (fun info -> not <| info.ClassName.Contains("改"))
            |> Array.sortBy (fun info -> info.PartRank)
            |> Array.iteri (fun idx info ->
                classAcc.Add(info.ClassName, $"{idx + 1}")
                classAcc.Add(info.ClassName + "改", $"{idx + 1}改"))

            classAcc

        for (slot, slotItems) in submarineItems do
            for info in slotItems do
                let key = $"潜水艇:{info.ClassName}:{slot}"
                let key2 = $"潜水艇:{classDict.[info.ClassName]}:{slot}"
                submarineDict.Add(key, info.Item)
                submarineDict.Add(key2, info.Item)

    member private x.ParseSubmarine(t, expr) =
        if t = "潜水艇" || t = "潜艇" then
            if submarineDict.Count = 0 then
                x.BuildSubmarineData()

            let m = submarineRegex.Match(expr)
            let succ, group = m.Groups.TryGetValue("part")

            if succ then
                let acu = ItemAccumulator()

                group.Captures
                |> Seq.iteri (fun slotId capture ->
                    if not <| capture.Value.Contains('0') then
                        let item = submarineDict.[$"潜水艇:{capture.Value}:{slotId}"]
                        acu.Update(item, 1.0))

                Some acu
            else
                None
        else
            None

    member x.TryParse(token: string) =
        if token.Contains('@') then
            let tmp = token.Split('@', 2)

            parsers |> Seq.tryPick (fun parser -> parser (tmp.[0], tmp.[1]))
        else
            None

type XivExpression() as x =
    inherit RecipeExpression<XivItem>()

    do
        let unaryFunc (l: RecipeOperand<XivItem>) =
            match l with
            | Number f ->
                let item = ItemCollection.Instance.GetByItemId(int f)

                let acu = ItemAccumulator(item)
                Accumulator acu
            | Accumulator _ -> failwithf "#符号仅对数字使用"

        let itemOperator = GenericOperator<_>('#', Int32.MaxValue, UnaryFunc = Some unaryFunc)

        x.Operators.Add(itemOperator)

    override x.TryGetItemByName(str) =
        failwith ""
        // 不再使用
        ItemCollection.Instance.TryGetByName(str.TrimEnd(CommandUtils.XivSpecialChars))

    override x.Tokenize(token) =
        match token with
        | _ when String.forall Char.IsDigit token -> Operand(Number(token |> float))
        | _ ->
            match AtExpression.Instance.TryParse(token) with
            | Some acu -> Operand(Accumulator(acu))
            | None ->
                let item = ItemCollection.Instance.TryGetByName(token.TrimEnd(CommandUtils.XivSpecialChars))

                if item.IsNone then
                    failwithf $"找不到物品 %s{token}"

                Operand(Accumulator(ItemAccumulator(item.Value)))
