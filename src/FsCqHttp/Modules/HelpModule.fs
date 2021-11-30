namespace KPX.FsCqHttp.Utils.HelpModule

open System

open KPX.FsCqHttp.Handler

open KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Utils.TextResponse
open KPX.FsCqHttp.Utils.UserOption


type HelpOption() as x =
    inherit CommandOption()

    let hidden = OptionCell(x, "hidden")

    member x.ShowHiddenCommands = hidden.IsDefined

/// 提供基础的帮助指令实现
/// 需要在应用中继承并调用HelpCommandImpl
[<AbstractClass>]
type HelpModuleBase() =
    inherit CommandHandlerBase()

    member _.ShowCommandList(cfg: HelpOption, cmdArg: CommandEventArgs) =
        use resp = cmdArg.OpenResponse(PreferImage)

        let modules =
            cmdArg
                .ApiCaller
                .CallApi<GetCtxModuleInfo>()
                .ModuleInfo
                .AllModules

        resp.Table {
            [ CellBuilder() { literal "命令" }; CellBuilder() { literal "说明" } ]

            let nonCommandModules = ResizeArray<string>()

            [ for item in modules do
                  match item with
                  | :? CommandHandlerBase as cmdModule ->
                      for cmd in cmdModule.Commands do
                          if not cmd.CommandAttribute.IsHidden || cfg.IsDefined("hidden") then
                              [ CellBuilder() { literal cmd.CommandAttribute.Command }
                                CellBuilder() { literal cmd.CommandAttribute.HelpDesc } ]
                  | _ -> nonCommandModules.Add(item.GetType().Name) ]

            yield!
                [ if nonCommandModules.Count <> 0 then
                      CellBuilder() { literal "已启用非指令模块：" }

                      for item in nonCommandModules do
                          CellBuilder() { literal item } ]

        }
        |> ignore

    member _.ShowCommandInfo(cfg: HelpOption, cmdArg: CommandEventArgs) =
        let cmd = cfg.NonOptionStrings.[0]

        let api = cmdArg.ApiCaller.CallApi(TryGetCommand(cmd))

        match api.CommandInfo with
        | None -> cmdArg.Reply $"该模块没有定义或不存在指令%s{cmd}"
        | Some ci ->
            use ret = cmdArg.OpenResponse(ForceText)
            ret.WriteLine("{0} ： {1}", ci.CommandAttribute.Command, ci.CommandAttribute.HelpDesc)

            if not <| String.IsNullOrEmpty(ci.CommandAttribute.LongHelp) then
                ret.Write(ci.CommandAttribute.LongHelp)

    member x.HelpCommandImpl(cmdArg: CommandEventArgs) =
        let cfg = HelpOption()
        cfg.Parse(cmdArg.HeaderArgs)

        if cfg.NonOptionStrings.Count = 0 then
            x.ShowCommandList(cfg, cmdArg)
        else
            x.ShowCommandInfo(cfg, cmdArg)
