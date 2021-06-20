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
        // 检查当前角色数量
        let cc =
            CardManager.count cmdArg.MessageEvent.UserId

        if cc >= CardManager.MAX_USER_CARDS then
            cmdArg.Abort(InputError, "角色数量上限，你已经有{0}张，上限为{1}张。", cc, CardManager.MAX_USER_CARDS)

        // 处理卡信息
        let regex =
            Text.RegularExpressions.Regex(@"([^\s\|0-9]+)([0-9]+)")

        let card =
            CharacterCard.DefaultOf(cmdArg.MessageEvent.UserId)

        // 写入默认技能
        for kv in Coc7.DefaultSkillValues do
            card.[kv.Key] <- kv.Value

        // 写入给定属性
        for m in regex.Matches(cmdArg.HeaderLine) do
            let name = m.Groups.[1].Value
            let prop = name |> Coc7.MapCoc7SkillName
            card.[prop] <- m.Groups.[2].Value |> int

        CardManager.insert card
        
        // insert不会改变卡ID，还得自己获取一次
        let card =
            CardManager
                .getByName(
                    cmdArg.MessageEvent.UserId,
                    card.ChrName
                )
                .Value

        CardManager.setCurrent card
        
        cmdArg.Reply($"已保存并绑定角色，请检查属性，或使用.pc remove %s{card.ChrName}删除。")
        using
            (cmdArg.OpenResponse(ForceImage))
            (fun ret ->
                let tt = card.ToTextTable()
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


        | Some List ->
            use ret = cmdArg.OpenResponse(ForceText)

            let cards =
                CardManager.getCards cmdArg.MessageEvent.UserId

            ret.WriteLine($"当前有角色%i{cards.Length}张：")

            for c in cards do
                ret.WriteLine(c.ChrName)
        | Some (Remove name) ->
            let card =
                CardManager.getByName (cmdArg.MessageEvent.UserId, name)

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            CardManager.remove card.Value
            cmdArg.Reply($"已删除角色%s{card.Value.ChrName}")
        | Some Clear -> raise <| NotImplementedException("该指令尚未实现")
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
                CardManager.getByName (cmdArg.MessageEvent.UserId, name)

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            match cmdArg.MessageEvent.Message.TryGetAt() with
            | None -> cmdArg.Abort(InputError, "需要at一个人作为接收方")
            | Some AtUserType.All -> cmdArg.Abort(InputError, "DD不可取")
            | Some (AtUserType.User uid) ->
                let newName = Guid.NewGuid().ToString("N")

                let copy =
                    { card.Value with
                          UserId = uid.Value
                          ChrName = newName }

                CardManager.insert copy

                cmdArg.Reply($"已复制到%i{uid.Value}，名称：%s{newName}，自行改名")
        | Some (Set name) ->
            let card =
                CardManager.getByName (cmdArg.MessageEvent.UserId, name)

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定角色%s{name}")

            CardManager.setCurrent card.Value
            cmdArg.Reply($"已设定当前角色为：%s{card.Value.ChrName}")
