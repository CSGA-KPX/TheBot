module TheBot.Utils.UserOption

open System
open System.Text
open System.Collections.Generic

[<DefaultAugmentation(false)>]
type UserOptionValue =
    | Defined of string
    | Default of string

    member x.IsDefined = match x with | Defined _ -> true | _ -> false

    member x.GetValue<'T when 'T :> IConvertible>() = 
        let ret = 
            match x with
            | Defined x -> x 
            | Default x -> x
        Convert.ChangeType(ret, typeof<'T>) :?> 'T

type UserOptionParser() = 
    static let seperator = [|";"; "；"; "："; ":"|]
    let mutable parsed = false
    let mutable cmdLine = Array.empty<string>
    let options = Dictionary<string, UserOptionValue>()

    let values = List<string * UserOptionValue>()
    //let alias = Dictionary<string, string>()

    member x.RegisterOption (name : string, value : string, ?alias : string []) = 
        let name  = name.ToLowerInvariant()
        let alias = defaultArg alias Array.empty

        if alias.Length <> 0 then raise <| NotImplementedException("alias")

        values.Add(name, Default value)

    member x.GetValue<'T when 'T :> IConvertible>(key : string) = 
        if not parsed then invalidOp "还没解析过"
        options.[key].GetValue<'T>()

    member x.GetValue(key : string) = x.GetValue<string>(key)

    member x.IsDefined(key : string) =
        if not parsed then invalidOp "还没解析过"
        options.[key].IsDefined

    member x.Parse(input : string []) = 
        if parsed then invalidOp "已经解析过了"
        for (k,v) in values do options.Add(k, v)
        cmdLine <- 
            [|
                for str in input do 
                    let contains = seperator |> Array.exists (fun s -> str.Contains(s))
                    if contains then
                        let s = str.Split(seperator, 2, StringSplitOptions.RemoveEmptyEntries)
                        let key   = s.[0]
                        let value = if s.Length >= 2 then s.[1] else ""
                        if options.ContainsKey(key) then
                            options.[key] <- Defined value
                        else
                            yield str
                    else
                        yield str
            |]

        parsed <- true

    member x.CommandLine = cmdLine

    member x.CmdLineAsString = String.Join(" ", cmdLine)