namespace KPX.FsCqHttp.Api

open System
open System.IO
open System.Text

open KPX.FsCqHttp
open Newtonsoft.Json


[<AbstractClass>]
type ApiBase() =

    let mutable executed = false

    /// 该API是否被执行过
    member x.IsExecuted
        with get () = executed
        and internal set v = executed <- v

    member x.EnsureExecuted() =
        if not executed then invalidOp "该API尚未被执行"

    /// 将子类的属性转换为字符串格式
    override x.ToString() =
        let sb = StringBuilder()

        let props =
            x
                .GetType()
                .GetProperties(
                    Reflection.BindingFlags.Instance
                    ||| Reflection.BindingFlags.Public
                    ||| Reflection.BindingFlags.DeclaredOnly
                )

        let header = $"%s{x.GetType().Name}---"
        sb.AppendLine(header) |> ignore

        for prop in props do
            sb.AppendFormat("{0} => {1}\r\n", prop.Name, prop.GetValue(x))
            |> ignore

        sb.AppendLine("".PadRight(header.Length, '-'))
        |> ignore

        sb.ToString()

[<AbstractClass>]
/// OneBot API基类
type CqHttpApiBase(action : string) =
    inherit ApiBase()

    member x.ActionName = action

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

/// 指示衍生类型可以提供API访问
type IApiCallProvider =
    [<Obsolete>]
    abstract CallerUserId : UserId
    [<Obsolete>]
    abstract CallerId : string
    [<Obsolete>]
    abstract CallerName : string

    abstract CallApi<'T when 'T :> ApiBase> : 'T -> 'T
    /// 调用一个不需要额外设定的api
    abstract CallApi<'T when 'T :> ApiBase and 'T : (new : unit -> 'T)> : unit -> 'T
