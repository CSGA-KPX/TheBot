﻿module KPX.FsCqHttp.Utils.HelpModule

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.TextTable

open KPX.FsCqHttp.Api.Context


[<Literal>]
let private helpHelp = "不加选项用来查看所有命令，加命令名查看命令帮助
如#help help"

type HelpModule() =
    inherit CommandHandlerBase()

    [<CommandHandlerMethodAttribute("help", "显示已知命令或显示命令文档", helpHelp)>]
    member x.HandleHelp(cmdArg : CommandEventArgs) =
        let cfg = UserOption.UserOptionParser()
        cfg.RegisterOption("hidden", "")
        cfg.Parse(cmdArg.Arguments)

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
                // TODO: 增加描述性词汇
                // nonCommandModules.Add(item.GetType().Name)
                ()

        if nonCommandModules.Count <> 0 then
            tt.AddPostTable("已启用非指令模块：")

            for m in nonCommandModules do
                tt.AddPostTable(m)

        using (cmdArg.OpenResponse(PreferImage)) (fun ret -> ret.Write(tt))

(*
    // 因为LongHelp基本没写。所以放弃了
    member x.HandleHelpUnused(cmdArg : CommandEventArgs) =
        if cmdArg.Arguments.Length = 0 then
            ()
        else
            for arg in cmdArg.Arguments do
                let arg = arg.ToLowerInvariant()

                let ret =
                    attribs
                    |> Array.tryFind (fun (cmd, _) -> cmd.Command = arg)

                if ret.IsSome then
                    let (cmd, _) = ret.Value
                    cmdArg.QuickMessageReply(cmd.LongHelp)
                else
                    cmdArg.QuickMessageReply(sprintf "找不到命令%s" arg)
*)