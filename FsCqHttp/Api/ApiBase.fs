namespace KPX.FsCqHttp.Api

open System
open System.IO
open System.Text

open Newtonsoft.Json


[<AbstractClass>]
type ApiRequestBase(action : string) as x =
    let logger =
        NLog.LogManager.GetLogger(x.GetType().Name)

    let mutable executed = false

    member x.ActionName = action

    member x.IsExecuted
        with get () = executed
        and internal set (v) = executed <- v

    member internal x.Logger = logger

    /// 生成请求Json
    member x.GetRequestJson(echo : string) =
        let sb = StringBuilder()
        use sw = new StringWriter(sb)
        let js = JsonSerializer()

        use w =
            new JsonTextWriter(sw, Formatting = Formatting.None)

        w.WriteStartObject()

        if not <| String.IsNullOrEmpty(echo) then
            w.WritePropertyName("echo")
            w.WriteValue(echo)

        w.WritePropertyName("action")
        w.WriteValue(action)
        w.WritePropertyName("params")
        w.WriteStartObject()
        x.WriteParams(w, js)
        w.WriteEndObject()
        w.WriteEndObject()
        sb.ToString()

    abstract HandleResponse : ApiResponse -> unit

    /// 写入Params对象内
    abstract WriteParams : JsonTextWriter * JsonSerializer -> unit

    default x.HandleResponse _ = ()
    default x.WriteParams(_, _) = ()

    override x.ToString() =
        let sb = StringBuilder()

        let props =
            x
                .GetType()
                .GetProperties(Reflection.BindingFlags.Instance
                               ||| Reflection.BindingFlags.Public
                               ||| Reflection.BindingFlags.DeclaredOnly)

        let header = sprintf "%s---" (x.GetType().Name)
        sb.AppendLine(header) |> ignore

        for prop in props do
            sb.AppendFormat("{0} => {1}\r\n", prop.Name, prop.GetValue(x))
            |> ignore

        sb.AppendLine("".PadRight(header.Length, '-'))
        |> ignore

        sb.ToString()

type IApiCallProvider =

    abstract CallApi<'T when 'T :> ApiRequestBase> : 'T -> 'T
    /// 调用一个不需要额外设定的api
    abstract CallApi<'T when 'T :> ApiRequestBase and 'T : (new : unit -> 'T)> : unit -> 'T
