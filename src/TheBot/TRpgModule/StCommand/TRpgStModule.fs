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

    [<CommandHandlerMethod(".st", "设置人物卡（不兼容子命令！）", "")>]
    member x.HandleST(cmdArg : CommandEventArgs) =
        // 检查当前人物卡数量
        let cc =
            CardManager.count cmdArg.MessageEvent.UserId

        if cc >= CardManager.MAX_USER_CARDS then
            cmdArg.Abort(InputError, "人物卡数量上限，你已经有{0}张，上限为{1}张。", cc, CardManager.MAX_USER_CARDS)

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

        using
            (cmdArg.OpenResponse(ForceImage))
            (fun ret ->
                let tt = card.ToTextTable()
                tt.AddPreTable("已保存人物卡：")
                ret.Write(tt))

    [<CommandHandlerMethod(".pc", "人物卡操作", "")>]
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

            ret.WriteLine($"当前有人物卡%i{cards.Length}张：")

            for c in cards do
                ret.WriteLine(c.ChrName)
        | Some (Remove name) ->
            let card =
                CardManager.getByName (cmdArg.MessageEvent.UserId, name)

            if card.IsNone then
                cmdArg.Abort(InputError, $"并不存在指定人物卡%s{name}")

            CardManager.remove card.Value
            cmdArg.Reply($"已删除人物卡%s{card.Value.ChrName}")
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
                    cmdArg.Reply($"{propName}：${card.[propName]}")
                else
                    cmdArg.Abort(InputError, $"人物卡中并不存在属性%s{propName}")
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
                cmdArg.Abort(InputError, $"并不存在指定人物卡%s{name}")

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
                cmdArg.Abort(InputError, $"并不存在指定人物卡%s{name}")

            CardManager.setCurrent card.Value
            cmdArg.Reply($"已设定当前人物卡为：%s{card.Value.ChrName}")
