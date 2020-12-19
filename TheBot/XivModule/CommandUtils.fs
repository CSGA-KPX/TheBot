module TheBot.Module.XivModule.Utils.CommandUtils

open BotData.XivData

open KPX.FsCqHttp.Handler

open TheBot.Utils.Config

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption

[<Literal>]
let defaultServerKey = "defaultServerKey"

type XivConfig(args : CommandEventArgs) =
    let opts = UserOptionParser()

    let cm =
        ConfigManager(ConfigOwner.User(args.MessageEvent.UserId))

    let defaultServerName = "拉诺西亚"
    let defaultServer = World.WorldFromName.[defaultServerName]

    do
        opts.RegisterOption("text", "")
        opts.RegisterOption("server", defaultServerName)

        [| for arg in args.Arguments do
            if World.WorldFromName.ContainsKey(arg) then
                yield "server:" + arg
            elif World.DataCenterAlias.ContainsKey(arg) then
                let dc = World.DataCenterAlias.[arg]

                let ss =
                    World.Worlds
                    |> Array.filter (fun x -> x.DataCenter = dc)
                    |> Array.map (fun x -> sprintf "server:%s" x.WorldName)

                yield! ss
            else
                yield arg |]
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

    member x.GetWorlds() =
        if x.IsWorldDefined then
            opts.GetValues("server")
            |> Array.map (fun str -> World.WorldFromName.[str])
        else
            Array.singleton (cm.Get(defaultServerKey, defaultServer))

    member x.CommandLine = opts.CommandLine

    member x.IsImageOutput =
        if opts.IsDefined("text") then ForceText else PreferImage

let XivSpecialChars =
    [| '\ue03c' // HQ
       '\ue03d' |] //收藏品
