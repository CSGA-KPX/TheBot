module TheBot.Module.DiceModule

open System
open KPX.FsCqHttp.DataType
open KPX.FsCqHttp.Handler.CommandHandlerBase
open TheBot.Utils

module ChoiceHelper =
    open System.Text.RegularExpressions

    let YesOrNoRegex = Regex("(.*)(.+)([没不]\2)(.*)", RegexOptions.Compiled)

module DiceExpression =
    open TheBot.GenericRPN
    open System.Text.RegularExpressions

    type DicerOperand(i : int) =
        member x.Value = i

        interface IOperand<DicerOperand> with
            override l.Add(r) = DicerOperand(l.Value + r.Value)
            override l.Sub(r) = DicerOperand(l.Value - r.Value)
            override l.Div(r) = DicerOperand(l.Value / r.Value)
            override l.Mul(r) = DicerOperand(l.Value * r.Value)

        override x.ToString() = i.ToString()

    type DiceExpression() as x =
        inherit GenericRPNParser<DicerOperand>()

        let tokenRegex = Regex("([^0-9])", RegexOptions.Compiled)

        do
            x.AddOperator(GenericOperator('D', 5))
            x.AddOperator(GenericOperator('d', 5))

        override x.Tokenize(str) =
            [| let strs = tokenRegex.Split(str) |> Array.filter (fun x -> x <> "")
               for str in strs do
                   match str with
                   | _ when Char.IsDigit(str.[0]) -> yield Operand(DicerOperand(str |> int))
                   | _ when x.Operatos.ContainsKey(str) -> yield Operator(x.Operatos.[str])
                   | _ -> failwithf "Unknown token %s" str |]

        member x.Eval(str : string, dicer : Dicer) =
            let func =
                EvalDelegate<DicerOperand>(fun (c, l, r) ->
                let d = l.Value
                let l = l :> IOperand<DicerOperand>
                match c with
                | '+' -> l.Add(r)
                | '-' -> l.Sub(r)
                | '*' -> l.Mul(r)
                | '/' -> l.Div(r)
                | 'D'
                | 'd' ->
                    let ret = Array.init<int> d (fun _ -> dicer.GetRandom(r.Value |> uint32)) |> Array.sum
                    DicerOperand(ret)
                | _ -> failwithf "")
            x.EvalWith(str, func)

        member x.TryEval(str : string, dicer : Dicer) =
            try
                let ret = x.Eval(str, dicer)
                Ok(ret)
            with e -> Error e

module EatHelper =
    let emptyChars = [| ' '; '\t'; '\r'; '\n' |]

    let arrayLastN (arr : 'a [], count : int) = 
            let n = arr.Length
            if n < count then
                invalidOp "Length < count!"
            arr.[n-count .. n-1]

    type EatConfig(owner) = 
        inherit UserConfig.ConfigItem<string[] * string []>(owner)

        override x.SerializeData(arr) =
            let (b,d) = arr
            String.Join("|", String.Join(" ", b), String.Join(" ", d))

        override x.DeserializeData(str) =
            let split = str.Split(Array.singleton '|', StringSplitOptions.None)
            if split.Length <> 2 then
                (Array.empty, Array.empty)
            else
                split.[0].Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)
                , split.[1].Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)

        override x.Default = ""

    
    let breakfast = """
    西北风 豆腐脑 小米粥 煎饼果子 黑米糕 水果沙拉 鸡蛋羹 南瓜馅饼 三明治 驴肉火烧
    麦当劳贫民早餐 饺子 馅饼 茶叶蛋 昨晚剩饭 鸡蛋灌饼 奶香小馒头 汉堡包 粢饭糕
    素椒面 豌杂面 燃面 绿豆稀饭 泡菜 麦当当薯饼 锅贴 老麻抄手 羊马渣渣面 蒸饺
    油条 麻团 萝卜丝油端子 韭菜盒子 面包 列巴 蛋糕 黑芝麻糊 土家酱香饼
    掉渣大饼 鸡蛋火烧 包子 馄钝 小笼汤包 生煎包 饭团 面条 粉丝汤 糖三角 糯米糕
    红薯 玉米 黄瓜 麦谷乐 炒饭 八宝饭 八宝粥 元宵 蒸饭 杂粮煎饼 水饺 荷包蛋
    烧饼 饽饽 肉夹馍 馄饨 火腿 蛋挞 南瓜粥 玉米糊 燕麦片
    辣糊汤 糁汤 牛奶麦片 梅干菜包 香菇青菜包 煎蛋 生煎 皮蛋瘦肉粥 煎饼 水煮蛋
    鸡蛋饼 手抓饼 烧麦 热干面 酸奶 玉米粥 肉包 素包 苹果 梨 香蕉 馒头 豆浆 牛奶 花卷
    流心奶黄包 小笼包 上汤猫耳朵 葱油拌面 豆浆烧饼油条 臭豆腐""".Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)

    let dinner = """
    麦当劳贫民餐 蒸功夫 萨莉亚 猪排饭 寿司 砂锅粥 咖喱饭 卤肉饭 冒菜 麻辣拌 鸡架 寿喜锅 肥羊
    食堂快餐 食堂铁板 食堂面 食堂鲜捞 食堂关东煮 泡面  素椒面 豌杂面 燃面 羊马渣渣面
    老麻抄手 毛血旺 肥肠 血旺 豆花牛肉 水煮牛肉 牛肉咔饼 烧鸡公 芋儿鸡 柴火鸡 青花椒鸡 胡椒猪肚鸡
    花胶鸡 椒麻鸡 海南鸡饭 白切鸡 火锅鱼 烤鱼 纸上烤鱼 石锅三角峰 黄辣丁 甜皮鸭 金陵烤鸭 北京烤鸭
    樟茶鸭 泰式火锅 肝腰合炒 乐山豆腐脑 西坝豆腐 简阳羊肉汤 白切羊肉 爆炒羊肚 驴肉火烧 朝天锅
    鸡鸭和乐 川北凉粉 拌拉皮 蒜泥护心肉 麦当劳 华莱士 豚骨拉面 辣白菜炒五花肉 烤青花鱼
    海鲜波奇饭 肥牛饭 咖喱 冬阴功汤 小鸡炖蘑菇 苹果 香蕉 梨 橙子 橘子 西瓜 黄瓜 胡萝卜
    面条 中式快餐 烧烤蛋包饭 刺身 盖浇饭 盖浇面 锅贴 牛肉汤 牛肉粉丝 便利店 香锅
    水饺 凉面 凉皮 河粉 肠粉 汉堡 炸鸡 下馆子点菜 炒饭 炒面 水果 健身餐
    关东煮 肯德基 馒头 排骨汤 羊蝎子 不吃 圣安娜  食堂 食其家 汉堡王
    麻辣烫 便当 全家 烤鸭 小杨生煎 三黄鸡 臭豆腐 烤冷面 热干面 螺蛳粉 速食汤 速食包
    火鸡面 烫饭 卤鸭爪 炸鸡爪 牛肉汤 麻辣香锅 浓香芝士年糕 韩式炸鸡 鱼蛋车仔面 奶油猪仔包 肥宅快乐鸡
    家常小炒 串串 火锅 烤肉 披萨 方便面 黄焖鸡 兰州拉面""".Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)

    let ng = """
    随便 都行 看你 不知道 不行就别吃了 建议玩手机 还是打游戏吧""".Split(emptyChars, StringSplitOptions.RemoveEmptyEntries)

    let GetEatString(msgArg : CommandArgs) =
        let (seed, data) = 
            let str = msgArg.RawMessage
            let (b, d) = 
                EatConfig(UserConfig.ConfigOwner.User (msgArg.MessageEvent.UserId |> uint64))
                    .Load()
            match str with
            | _ when str.Contains("早") -> "早餐", Array.append breakfast b
            | _ when str.Contains("午") -> "午餐", Array.append dinner d
            | _ when str.Contains("晚") -> "晚餐", Array.append dinner d
            | _ when str.Contains("加") -> "加餐", Array.append breakfast b
            | _ -> failwithf "可选关键词 早 中 午 晚 加"

        let data = data |> Array.distinct

        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent), AutoRefreshSeed = false)

        let dice = dicer.GetRandomFromString(seed, 100u)

        if dice >= 96 then
            dicer.GetRandomItem(ng)
        else
            let mapped = 
                data
                |> Array.map (fun x -> x, dicer.GetRandomFromString(seed + "吃" + x, 100u))

            let eat = 
                mapped
                |> Array.filter (fun (_, c) -> c <= 5 )
                |> Array.sortBy (snd)
                |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

            let notEat = 
                mapped
                |> Array.filter (fun (_, c) -> c >= 96 )
                |> Array.sortByDescending (snd)
                |> Array.map (fun (i,c) -> sprintf "%s(%i)" i c )

            sprintf "宜：%s\r\n忌：%s" (String.Join(" ", eat)) (String.Join(" ", notEat))

type DiceModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("c", "对多个选项1d100", "A B C D")>]
    member x.HandleChoices(msgArg : CommandArgs) =
        let atUser = msgArg.MessageEvent.Message.GetAts() |> Array.tryHead
        let sw = new IO.StringWriter()
        if atUser.IsSome then
            match atUser.Value with
            | Message.AtUserType.All ->
                failwithf "公共事件请用at bot账号"
            | Message.AtUserType.User x when x = msgArg.CqEventArgs.SelfId ->
                sw.WriteLine("公投：")
            | Message.AtUserType.User x ->
                let atUserName = KPX.FsCqHttp.Api.GroupApi.GetGroupMemberInfo(msgArg.MessageEvent.GroupId, x)
                let ret = msgArg.CqEventArgs.CallApi(atUserName)
                sw.WriteLine("{0} 为 {1} 投掷：", msgArg.MessageEvent.GetNicknameOrCard, ret.DisplayName)

        let tt = TextTable.FromHeader([| "1D100"; "选项" |])

        [|
            for arg in msgArg.Arguments do 
                let m = ChoiceHelper.YesOrNoRegex.Match(arg)
                if m.Success then
                    yield m.Groups.[1].Value + m.Groups.[2].Value + m.Groups.[4].Value
                    yield m.Groups.[1].Value + m.Groups.[3].Value + m.Groups.[4].Value
                else
                    yield arg
        |]
        |> Array.map (fun c -> 
            let seed = 
                if atUser.IsSome then SeedOption.SeedByAtUserDay(msgArg.MessageEvent)
                else SeedOption.SeedByUserDay(msgArg.MessageEvent)
            let dicer = Dicer(seed, AutoRefreshSeed = false)
            (c, dicer.GetRandomFromString(c, 100u)))
        |> Array.sortBy snd
        |> Array.iter (fun (c, n) -> tt.AddRow((sprintf "%03i" n), c))

        sw.Write(tt.ToString())
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("jrrp", "今日人品值", "")>]
    member x.HandleJrrp(msgArg : CommandArgs) =
        let dicer = Dicer(SeedOption.SeedByUserDay(msgArg.MessageEvent))
        let jrrp = dicer.GetRandom(100u)
        msgArg.CqEventArgs.QuickMessageReply(sprintf "%s今日人品值是%i" msgArg.MessageEvent.GetNicknameOrCard jrrp)

    [<CommandHandlerMethodAttribute("cal", "计算器", "")>]
    member x.HandleCalculator(msgArg : CommandArgs) =
        let sw = new System.IO.StringWriter()
        let dicer = Dicer()
        let parser = DiceExpression.DiceExpression()
        for arg in msgArg.Arguments do
            let ret = parser.TryEval(arg, dicer)
            match ret with
            | Error e -> sw.WriteLine("对{0}失败{1}", arg, e.ToString())
            | Ok i -> sw.WriteLine("对{0}求值得{1}", arg, i.Value)
        msgArg.CqEventArgs.QuickMessageReply(sw.ToString())

    [<CommandHandlerMethodAttribute("eat", "吃什么？", "#eat 晚餐")>]
    member x.HandleEat(msgArg : CommandArgs) =
        msgArg.CqEventArgs.QuickMessageReply(EatHelper.GetEatString(msgArg))

    [<CommandHandlerMethodAttribute("eab", "添加早餐/加餐", "#eab 西北风 东南风")>]
    member x.HandleAddBreakfast(msgArg : CommandArgs) =
        let cfg = EatHelper.EatConfig(UserConfig.ConfigOwner.User (msgArg.MessageEvent.UserId |> uint64))
        let (b, d) = cfg.Load()
        
        let b = Array.append (b) (msgArg.Arguments) |> Array.distinct
        cfg.Save(b,d)

        sprintf "已将%A保存到配置" (msgArg.Arguments)
        |> msgArg.CqEventArgs.QuickMessageReply

    [<CommandHandlerMethodAttribute("ead", "添加午餐/晚餐", "#ead 西北风 东南风")>]
    member x.HandleAddDinner(msgArg : CommandArgs) =
        let cfg = EatHelper.EatConfig(UserConfig.ConfigOwner.User (msgArg.MessageEvent.UserId |> uint64))
        let (b, d) = cfg.Load()
        
        let d = Array.append (d) (msgArg.Arguments) |> Array.distinct
        cfg.Save(b,d)

        sprintf "已将%A保存到配置" (msgArg.Arguments)
        |> msgArg.CqEventArgs.QuickMessageReply

    [<CommandHandlerMethodAttribute("gacha", "抽10连 概率3%", "")>]
    member x.HandleGacha(msgArg : CommandArgs) =
        let cutoff =
            msgArg.Arguments
            |> Seq.tryPick (fun arg ->
                let (succ, i) = UInt32.TryParse(arg)
                if succ then Some(int i) else None)
            |> Option.defaultValue 3
        
        let dicer = Dicer(SeedOption.SeedRandom |> Array.singleton )

        let count = 10
        let ret =
            Array.init count (fun _ -> dicer.GetRandom(100u))
            |> Array.map (fun x -> if x <= cutoff then "红" else "黑")

        msgArg.CqEventArgs.QuickMessageReply(String.Join(" ", ret))