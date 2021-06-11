namespace rec KPX.FsCqHttp.Utils.Subcommands

open System
open System.Collections.Generic

open FSharp.Reflection

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.UserOption


[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type AltCommandName([<ParamArray>] names : string []) =
    inherit Attribute()

    member x.Names = names

    member x.Test = FSharpType.GetUnionCases

type ISubcommandTemplate =
    /// 返回每种子命令的DU的帮助文本
    abstract Usage : string

type private SubCommandCaseInfo =
    { CommandNames : string []
      //Arguments : Type []
      UnionCase : UnionCaseInfo
      OptionParser : OptionBase option }

    static member Parse(ui : UnionCaseInfo) =
        let names = ResizeArray<string>()
        names.Add(ui.Name.ToUpperInvariant())

        for attr in ui.GetCustomAttributes(typeof<AltCommandName>) do
            for alias in (attr :?> AltCommandName).Names do
                names.Add(alias.ToUpperInvariant())

        let obp, args =
            ui.GetFields()
            |> Array.map (fun p -> p.PropertyType)
            |> Array.partition
                (fun t ->
                    t.IsSubclassOf(typeof<OptionBase>)
                    || t = typeof<OptionBase>)

        if args.Length <> 0 then
            raise <| NotImplementedException("暂不支持直接定义属性")

        if obp.Length > 1 then invalidOp "不支持多个OptionBase类型"

        let ob =
            obp
            |> Array.tryHead
            |> Option.map (fun t -> Activator.CreateInstance(t) :?> OptionBase)

        { CommandNames = names.ToArray()
          UnionCase = ui
          OptionParser = ob }

type SubcommandParser private () =
    static member Parse<'T when 'T :> ISubcommandTemplate>(args : string []) =
        // 生成指令模板
        let subs =
            FSharpType.GetUnionCases(typeof<'T>, false)
            |> Array.map (SubCommandCaseInfo.Parse)



        args
        |> Array.tryHead
        |> Option.map
            (fun cmd ->
                let u = cmd.ToUpperInvariant()

                subs
                |> Array.tryFind (fun sub -> sub.CommandNames |> Array.exists ((=) u)))
        |> Option.flatten
        |> Option.map
            (fun info ->
                info.OptionParser
                |> Option.map
                    (fun ob ->
                        if args.Length = 1 then
                            ob.Parse(Seq.empty)
                        else
                            ob.Parse(args |> Array.tail)

                        FSharpValue.MakeUnion(info.UnionCase, Array.singleton<obj> ob) :?> 'T)
                |> Option.defaultWith
                    (fun () -> FSharpValue.MakeUnion(info.UnionCase, Array.empty) :?> 'T))

    static member Parse<'T when 'T :> ISubcommandTemplate>(cmdArg : CommandEventArgs) =
        SubcommandParser.Parse<'T>(cmdArg.HeaderArgs)
