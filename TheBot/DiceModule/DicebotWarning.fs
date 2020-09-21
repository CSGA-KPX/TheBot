module TheBot.Module.DicebotWarning

open System
open System.Collections
open KPX.FsCqHttp.Handler


let diceCommands = 
    [|
        ".r"; ".rs"; ".w"; ".ww"; ".set"; ".sc"; ".en"; ".coc"; ".dnd"; ".coc";
        ".ti"; ".li"; ".st"; ".rc"; ".ra"; ".name"; ".rules"; ".help"; ".me";
        ".ri"; ".init"; ".nn"; ".nnn"; ".rh"; ".bot"; ".ob"; ".me"; ".welcome";
        ".jrrp";
    |] |> set

type DiceWarningModule() =
    inherit HandlerModuleBase()

    override x.HandleMessage(arg, e) =
        let msg = e.Message.ToString().Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
        if msg.Length >= 1 then
            let head = msg.[0].ToLowerInvariant()
            if head.StartsWith(".") && diceCommands.Contains(head) then
                arg.QuickMessageReply("现在使用的平台无法加载Dice!插件，请去前往kokona.tech使用官方机器人", atUser = true)