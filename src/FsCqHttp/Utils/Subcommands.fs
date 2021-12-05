namespace rec KPX.FsCqHttp.Utils.Subcommands

open System

open FSharp.Reflection

open KPX.FsCqHttp.Handler
open KPX.FsCqHttp.Utils.UserOption


[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
type AltCommandNameAttribute([<ParamArray>] names: string []) =
    inherit Attribute()

    member x.Names = names

(*
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = false)>]
/// 指示当前DU Case为默认指令分支
type DefaultCommandAttribute() =
    inherit Attribute()
*)

type ISubcommandTemplate =
    /// 返回每种子命令的DU的帮助文本
    abstract Usage: string

[<RequireQualifiedAccess>]
type private SubCommandParamsInfo =
    | None
    | Option of OptionBase
    | Arguments of Type []

type private SubCommandCaseInfo =
    { CommandNames: string []
      UnionCase: UnionCaseInfo
      ParamsInfo: SubCommandParamsInfo }

    static member Parse(ui: UnionCaseInfo) =
        let names = ResizeArray<string>()
        names.Add(ui.Name.ToUpperInvariant())

        for attr in ui.GetCustomAttributes(typeof<AltCommandNameAttribute>) do
            for alias in (attr :?> AltCommandNameAttribute).Names do
                names.Add(alias.ToUpperInvariant())

        let obp, args =
            ui.GetFields()
            |> Array.map (fun p -> p.PropertyType)
            |> Array.partition (fun t -> t.IsSubclassOf(typeof<OptionBase>) || t = typeof<OptionBase>)

        let paramsInfo =
            match obp.Length, args.Length with
            | 0, 0 -> SubCommandParamsInfo.None
            | 0, _ -> SubCommandParamsInfo.Arguments args
            | 1, 0 ->
                Activator.CreateInstance(obp |> Array.head) :?> OptionBase
                |> SubCommandParamsInfo.Option
            | 1, _ -> invalidOp "已经使用Option时不能再制定其他参数"
            | _, _ -> invalidOp "不允许同时使用多个OptionBase类型"

        { CommandNames = names.ToArray()
          ParamsInfo = paramsInfo
          UnionCase = ui }

type SubcommandParser private () =
    static member Parse<'T when 'T :> ISubcommandTemplate>(args: string []) =
        // 生成指令模板
        let subs =
            FSharpType.GetUnionCases(typeof<'T>, false)
            |> Array.map SubCommandCaseInfo.Parse

        args
        |> Array.tryHead
        |> Option.map
            (fun cmd ->
                let u = cmd.ToUpperInvariant()

                subs |> Array.tryFind (fun sub -> sub.CommandNames |> Array.exists ((=) u)))
        |> Option.flatten
        |> Option.map
            (fun info ->
                // 第一个用来表示子命令了，所以从第二个开始算参数
                let argTail =
                    if args.Length = 1 then
                        Array.empty
                    else
                        args |> Array.tail

                match info.ParamsInfo with
                | SubCommandParamsInfo.None -> FSharpValue.MakeUnion(info.UnionCase, Array.empty) :?> 'T
                | SubCommandParamsInfo.Option ob ->
                    ob.Parse(argTail)
                    FSharpValue.MakeUnion(info.UnionCase, Array.singleton<obj> ob) :?> 'T
                | SubCommandParamsInfo.Arguments args ->
                    if argTail.Length <> args.Length then
                        invalidOp $"输入参数不正确：需要%i{args.Length}个参数，而提供了%i{argTail.Length}个"

                    let uiParams = Array.map2 (fun (t: Type) (s: string) -> Convert.ChangeType(s, t)) args argTail

                    FSharpValue.MakeUnion(info.UnionCase, uiParams) :?> 'T)

    static member Parse<'T when 'T :> ISubcommandTemplate>(cmdArg: CommandEventArgs) =
        SubcommandParser.Parse<'T>(cmdArg.HeaderArgs)

    static member GenerateHelp<'T when 'T :> ISubcommandTemplate>() =
        [| for ui in FSharpType.GetUnionCases(typeof<'T>, false) do
               let mutable name = ui.Name

               for attr in ui.GetCustomAttributes(typeof<AltCommandNameAttribute>) do
                   for alias in (attr :?> AltCommandNameAttribute).Names do
                       name <- alias

               let args =
                   ui.GetFields()
                   |> Array.map
                       (fun pi ->
                           if pi.PropertyType.IsValueType then
                               Activator.CreateInstance(pi.PropertyType)
                           else
                               null)

               let dummyCase = FSharpValue.MakeUnion(ui, args) :?> 'T

               yield $"子指令：%s{name} ：%s{dummyCase.Usage}" |]
