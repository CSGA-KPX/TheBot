﻿module KPX.TheBot.Module.XivModule.Utils.CommandUtils

open KPX.TheBot.Data.XivData

open KPX.FsCqHttp.Handler

open KPX.TheBot.Utils.Config

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption


[<Literal>]
let defaultServerKey = "defaultServerKey"

type XivConfig(args : CommandEventArgs) =
    let opts = UserOptionParser()

    let cm =
        ConfigManager(ConfigOwner.User(args.MessageEvent.UserId))

    let defaultServerName = "拉诺西亚"
    let defaultServer = World.GetWorldByName(defaultServerName)

    do
        opts.RegisterOption("text", "")
        opts.RegisterOption("server", defaultServerName)

        [| for arg in args.Arguments do
            if World.DefinedWorld(arg) then
                yield "server:" + arg
            elif World.DefinedDC(arg) then
                let dc = World.GetDCByName(arg)

                let ss =
                    World.GetWorldsByDC(dc)
                    |> Seq.map (fun x -> sprintf "server:%s" x.WorldName)

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
            World.GetWorldByName(opts.GetValue("server"))
        else
            cm.Get(defaultServerKey, defaultServer)

    member x.GetWorlds() =
        if x.IsWorldDefined then
            opts.GetValues("server")
            |> Array.map (fun str -> World.GetWorldByName(str))
        else
            Array.singleton (cm.Get(defaultServerKey, defaultServer))

    member x.CommandLine = opts.CommandLine

    member x.CmdLineAsString = opts.CmdLineAsString

    member x.IsImageOutput =
        if opts.IsDefined("text") then ForceText else PreferImage

let XivSpecialChars =
    [| '\ue03c' // HQ
       '\ue03d' |] //收藏品
