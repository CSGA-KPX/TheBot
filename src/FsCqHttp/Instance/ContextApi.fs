// 此命名空间提供访问CqWsContext内信息的API
//
// 目前架构上无法通过IApiCallProvider访问CqWsContext的数据
// 所以使用API的方式实现访问和并发控制
namespace KPX.FsCqHttp.Api.Context

open KPX.FsCqHttp
open KPX.FsCqHttp.Instance
open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Message

open Newtonsoft.Json.Linq


type GetCtxModuleInfo() =
    inherit WsContextApiBase()

    let mutable m = ContextModuleInfo()

    member x.ModuleInfo =
        x.EnsureExecuted()
        m

    override x.Invoke(ctx) = m <- ctx.Modules

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
type TryGetCommand(cmdName: string) =
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

/// 将指定CommandEventArgs事件重写为其他指令
/// 强制使用调度器
type RewriteCommand(e: CommandEventArgs, messages: seq<ReadOnlyMessage>) =
    inherit WsContextApiBase()

    new(e, cmdLines: seq<string>) =
        RewriteCommand(
            e,
            cmdLines
            |> Seq.map
                (fun cmd ->
                    let msg = Message()
                    msg.Add(cmd)
                    msg :> ReadOnlyMessage)
        )

    override x.Invoke(ctx) =
        for msg in messages do
            let obj = e.RawEvent.RawEventPost.DeepClone() :?> JObject
            obj.["message"] <- JToken.FromObject(msg) :?> JArray
            obj.["raw_message"] <- JValue(msg.ToCqString())

            let msgEvent = CqEventArgs.Parse(ctx, PostContent(obj)) :?> CqMessageEventArgs

            let isCmd = ctx.Modules.TryCommand(msgEvent)

            if isCmd.IsSome then
                let ci = isCmd.Value
                let cmdArgs = CommandEventArgs(msgEvent, ci.CommandAttribute)
                TaskScheduler.enqueue (ctx.Modules, TaskContext.Command(cmdArgs, ci))
            else
                TaskScheduler.enqueue (ctx.Modules, TaskContext.Message msgEvent)
