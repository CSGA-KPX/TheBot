namespace KPX.FsCqHttp.Api
open System
open System.IO
open System.Collections.Generic
open System.Text
open Newtonsoft.Json

[<AbstractClass>]
type ApiRequestBase(action : string) as x =
    let logger = NLog.LogManager.GetLogger(x.GetType().Name)

    member internal x.Logger = logger

    /// 生成请求Json
    member x.GetRequestJson(?echo : string) =
        let echo = defaultArg echo ""
        let sb = new StringBuilder()
        let sw = new StringWriter(sb)
        let js = new JsonSerializer()
        use w = new JsonTextWriter(sw, Formatting = Formatting.Indented)
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

    abstract HandleResponseData : IReadOnlyDictionary<string, string> -> unit

    /// 写入Params对象内
    abstract WriteParams : JsonTextWriter * JsonSerializer-> unit

    default x.HandleResponseData _ = ()
    default x.WriteParams(_,_) = ()

    override x.ToString() = 
        let sb = new StringBuilder()
        let props = x.GetType().GetProperties(Reflection.BindingFlags.Instance ||| Reflection.BindingFlags.Public ||| Reflection.BindingFlags.DeclaredOnly)
        let header = sprintf "%s---" (x.GetType().Name)
        sb.AppendLine(header)  |> ignore
        for prop in props do 
            sb.AppendFormat("{0} => {1}\r\n", prop.Name, prop.GetValue(x)) |> ignore
        sb.AppendLine("".PadRight(header.Length, '-'))  |> ignore
        sb.ToString()