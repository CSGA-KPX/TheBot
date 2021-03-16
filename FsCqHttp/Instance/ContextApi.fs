// 此命名空间提供访问CqWsContext内信息的API
//
// 目前架构上无法通过IApiCallProvider访问CqWsContext的数据
// 所以使用API的方式实现访问和并发控制
namespace KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp.Event

open KPX.FsCqHttp.Instance


type GetCtxModules() =
    inherit WsContextApiBase()

    let mutable m = Array.empty

    member x.Moduldes =
        x.EnsureExecuted()
        m

    override x.Invoke(ctx) = m <- ctx.Moduldes |> Seq.toArray

/// 返回无序的已定义指令。
///
/// 需要有序请使用GetCtxModules
type GetCtxCommands() =
    inherit WsContextApiBase()

    let mutable c = Array.empty

    member x.Commands =
        x.EnsureExecuted()
        c

    override x.Invoke(ctx) = c <- ctx.Commands |> Seq.toArray

/// 根据名称（含CommandStart）查找模块内指令
type TryGetCommand(cmdName : string) =
    inherit WsContextApiBase()

    let mutable cmd = None

    member x.CommandInfo =
        x.EnsureExecuted()
        cmd

    override x.Invoke(ctx) =
        cmd <-
            let cmp = System.StringComparer.OrdinalIgnoreCase

            ctx.Commands
            |> Seq.tryFind (fun cmd -> cmp.Equals(cmd.CommandAttribute.Command, cmdName))
