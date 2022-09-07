module KPX.XivPlugin.Modules.Utils.XivExpression

open System

open KPX.XivPlugin.Data

open KPX.TheBot.Host.DataModel.Recipe
open KPX.TheBot.Host.Utils.GenericRPN
open KPX.TheBot.Host.Utils.RecipeRPN


type ItemAccumulator = ItemAccumulator<XivItem>

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
            if token.Contains("@") then
                let tmp = token.Split('@')

                if tmp.Length <> 2 then
                    failwithf "装等表达式错误"

                let jobs = Collections.Generic.HashSet<ClassJob>()

                match ClassJob.TryParse(tmp.[0]) with
                | Some (job) -> jobs.Add(job) |> ignore
                | _ ->
                    let func = (fun map -> map.Code) >> jobs.Add >> ignore

                    match tmp.[0] with
                    | "生产" -> ClassJob.CraftJobs |> Seq.iter func
                    | "采集" -> ClassJob.GatherJobs |> Seq.iter func
                    | "生产采集"
                    | "生活"
                    | "生采" -> ClassJob.CraftGatherJobs |> Seq.iter func
                    | _ -> failwith "未知职业，如确定无误请联系开发者添加简写"


                let acu = ItemAccumulator()
                let cgc = CraftableGearCollection.Instance
                let iLevel = int tmp.[1]

                for job in jobs do
                    let gears = cgc.Search(iLevel, job)

                    if gears.Length = 0 then
                        failwith $"不存在指定的装备 {job}@{iLevel}"

                    for (g, q) in gears do
                        let item = ItemCollection.Instance.GetByItemId(g.ItemId)
                        acu.[item] <- q

                Operand(Accumulator(acu))
            else
                let item = ItemCollection.Instance.TryGetByName(token.TrimEnd(CommandUtils.XivSpecialChars))

                if item.IsNone then
                    failwithf $"找不到物品 %s{token}"

                Operand(Accumulator(ItemAccumulator(item.Value)))
