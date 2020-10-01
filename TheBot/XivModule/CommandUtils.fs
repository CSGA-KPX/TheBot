module TheBot.Module.XivModule.Utils.CommandUtils

open BotData.XivData

open TheBot.Utils.Config
open KPX.FsCqHttp.Utils.UserOption

[<Literal>]
let private defaultServerKey = "defaultServerKey"

type XivConfig (args : KPX.FsCqHttp.Handler.CommandHandlerBase.CommandArgs) = 
    let opts = UserOptionParser()
    let cm = ConfigManager(ConfigOwner.User (args.MessageEvent.UserId))

    let defaultServerName = "拉诺西亚"
    let defaultServer = World.WorldFromName.[defaultServerName]

    do
        opts.RegisterOption("text", "")
        opts.RegisterOption("server", defaultServerName)

        args.Arguments
        |> Array.map (fun str -> 
            if World.WorldFromName.ContainsKey(str) then
                "server:"+str
            else
                str)
        |> opts.Parse

    member x.IsWorldDefined = opts.IsDefined("server")

    /// 获得查询目标服务器
    ///
    /// 用户指定 -> 用户配置 -> 默认（拉诺西亚）
    member x.GetWorld() = 
        if x.IsWorldDefined then
            World.WorldFromName.[opts.GetValue("server")]
        else
            cm.Get(defaultServerKey, defaultServer)

    member x.CommandLine = opts.CommandLine

    member x.IsImageOutput = not <| opts.IsDefined("text")

let XivSpecialChars = 
    [|
        '\ue03c' // HQ
        '\ue03d' //收藏品
    |]