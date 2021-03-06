﻿namespace KPX.FsCqHttp.Utils.HelpModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable
open KPX.FsCqHttp.Utils.UserOption


type HelpModule() =
    inherit CommandHandlerBase()

    member _.ShowCommandList(cfg : UserOptionParser, cmdArg : CommandEventArgs) =
        let tt = TextTable("命令", "说明")

        let nonCommandModules = ResizeArray<string>()

        let modules =
            cmdArg.ApiCaller.CallApi<GetCtxModules>().Moduldes

        for item in modules do
            match item with
            | :? CommandHandlerBase as cmdModule ->
                for cmd in cmdModule.Commands do
                    if not cmd.CommandAttribute.IsHidden
                       || cfg.IsDefined("hidden") then
                        tt.AddRow(cmd.CommandName, cmd.CommandAttribute.HelpDesc)
            | _ ->
                // TODO: 为非命令模块增加描述性词汇
                nonCommandModules.Add(item.GetType().Name)

        if nonCommandModules.Count <> 0 then
            tt.AddPostTable("已启用非指令模块：")

            for m in nonCommandModules do
                tt.AddPostTable(m)

        using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write(tt))

    member _.ShowCommandInfo(cfg : UserOptionParser, cmdArg : CommandEventArgs) =
        let cmd = cfg.CommandLine.[0]

        let api =
            cmdArg.ApiCaller.CallApi(TryGetCommand(cmd))

        match api.CommandInfo with
        | None -> cmdArg.QuickMessageReply(sprintf "该模块没有定义或不存在指令%s" cmd)
        | Some ci when String.IsNullOrEmpty(ci.CommandAttribute.LongHelp) ->
            cmdArg.QuickMessageReply(sprintf "%s没有定义说明文本" cmd)
        | Some ci ->
            use ret = cmdArg.OpenResponse(ForceText)
            ret.WriteLine("{0} ： {1}", ci.CommandName, ci.CommandAttribute.HelpDesc)
            ret.Write(ci.CommandAttribute.LongHelp)

    [<CommandHandlerMethodAttribute("help",
                                    "显示已知命令或显示命令文档详见#help #help",
                                    "没有参数显示已有指令。加指令名查看指令帮助如#help #help")>]
    member x.HandleHelp(cmdArg : CommandEventArgs) =
        let cfg = UserOptionParser()
        cfg.RegisterOption("hidden", "")
        cfg.Parse(cmdArg.Arguments)

        if cfg.CommandLine.Length = 0 then
            x.ShowCommandList(cfg, cmdArg)
        else
            x.ShowCommandInfo(cfg, cmdArg)