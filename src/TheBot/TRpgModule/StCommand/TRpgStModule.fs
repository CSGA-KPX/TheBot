namespace KPX.TheBot.Module.TRpgModule.StCommand

open System

open KPX.FsCqHttp.Message
open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.Subcommands
open KPX.FsCqHttp.Utils.TextTable

open KPX.TheBot.Module.TRpgModule
open KPX.TheBot.Module.TRpgModule.TRpgCharacterCard


type StModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethod(".st", "设置角色（不兼容子命令！）", "")>]
    member x.HandleST(cmdArg : CommandEventArgs) =
        // 处理卡信息
        let regex =
            Text.RegularExpressions.Regex(@"([^\s\|0-9]+)([0-9]+)")

        let card = CardManager.getCurrentCard cmdArg.MessageEvent.UserId

        // 写入给定属性
        for m in regex.Matches(cmdArg.HeaderLine) do
            let name = m.Groups.[1].Value
            let prop = name |> Coc7.MapCoc7SkillName
            card.[prop] <- m.Groups.[2].Value |> int

        CardManager.upsert card
        
        using
            (cmdArg.OpenResponse(ForceImage))
            (fun ret ->
                let tt = card.ToTextTable()
                tt.AddPreTable("已经保存角色，请检查属性值")
                ret.Write(tt))

    [<CommandHandlerMethod(".pc", "角色操作，不带参数查看帮助", "")>]
    member x.HandlePC(cmdArg : CommandEventArgs) =
        match SubcommandParser.Parse<PcSubcommands>(cmdArg.HeaderArgs) with
        | None ->
            using
                (cmdArg.OpenResponse(ForceText))
                (fun ret ->
                    let help =
                        SubcommandParser.GenerateHelp<PcSubcommands>()

                    for line in help do
                        ret.WriteLine(line))

        | Some (New chrName) ->
            // 检查当前角色数量
            let cc =
                CardManager.count cmdArg.MessageEvent.UserId

            if cc >= CardManager.MAX_USER_CARDS then
                cmdArg.Abort(InputError, "角色数量上限，你已经有{0}张，上限为{1}张。", cc, CardManager.MAX_USER_CARDS)
                
            // 检查名字是否被占用
            let c =
                CardManager.getByName cmdArg.MessageEvent.UserId chrName

            if c.IsSome then cmdArg.Abort(InputError, "该角色名已被占用")

            let card =
                { CharacterCard.DefaultOf(cmdArg.MessageEvent.UserId) with
                      ChrName = chrName }

            CardManager.insert card |> CardManager.setCurrent

            cmdArg.Reply($"已保存并绑定角色")

        | Some List ->
            use ret = cmdArg.OpenResponse(ForceText)

            let cards =
                CardManager.getCards cmdArg.MessageEvent.UserId

            ret.WriteLine($"当前有角色%i{cards.Length}张：")

            for c in cards do
                ret.WriteLine(c.ChrName)
        | Some (Remove name) ->
            let card =
                CardManager.getByName cmdArg.MessageEvent.UserId name

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            CardManager.remove card.Value
            cmdArg.Reply($"已删除角色%s{card.Value.ChrName}")
        | Some Clear ->
            let card = CardManager.getCurrentCard cmdArg.MessageEvent.UserId
            card.Props.Clear()
            
            CardManager.upsert(card)
            cmdArg.Reply("已清空所有数据")
            
        | Some Get -> raise <| NotImplementedException("该指令尚未实现")
        | Some (Show opt) ->
            let card =
                CardManager.getCurrentCard cmdArg.MessageEvent.UserId

            match opt.SkillName with
            | None ->
                using (cmdArg.OpenResponse(ForceImage)) (fun ret -> ret.Write(card.ToTextTable()))
            | Some propName ->
                if card.Props.ContainsKey(propName) then
                    cmdArg.Reply($"{propName}：%i{card.[propName]}")
                else
                    cmdArg.Abort(InputError, $"角色中并不存在属性%s{propName}")
        | Some Lock -> raise <| NotImplementedException("该指令尚未实现")
        | Some Unlock -> raise <| NotImplementedException("该指令尚未实现")
        | Some (Rename name) ->
            let card =
                CardManager.getCurrentCard cmdArg.MessageEvent.UserId

            CardManager.upsert { card with ChrName = name }
            cmdArg.Reply($"已改名为：%s{name}")
        | Some (Copy name) ->
            let card =
                CardManager.getByName cmdArg.MessageEvent.UserId name

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            match cmdArg.MessageEvent.Message.TryGetAt() with
            | None -> cmdArg.Abort(InputError, "需要at一个人作为接收方")
            | Some AtUserType.All -> cmdArg.Abort(InputError, "DD不可取")
            | Some (AtUserType.User uid) ->
                // 检查接收方是否有同名卡
                let tCard =
                    CardManager.getByName uid card.Value.ChrName

                if tCard.IsSome then cmdArg.Abort(InputError, "接收方已有同名卡")

                let copy =
                    { card.Value with
                          UserId = uid.Value
                          ChrName = card.Value.ChrName }

                CardManager.insert copy |> ignore

                cmdArg.Reply($"已复制到%i{uid.Value}，名称：%s{copy.ChrName}")
        | Some (Set name) ->
            let card =
                CardManager.getByName cmdArg.MessageEvent.UserId name

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            CardManager.setCurrent card.Value
            cmdArg.Reply($"已设定当前角色为：%s{card.Value.ChrName}")
