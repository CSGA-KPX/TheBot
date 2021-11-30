﻿module KPX.XivPlugin.Modules.Utils.CommandUtils

open KPX.XivPlugin.Data

open KPX.FsCqHttp.Utils.UserOption


let XivSpecialChars =
    [| '\ue03c' // HQ
       '\ue03d' |] //收藏品

type XivWorldOpt(cb : CommandOption, key, defVal) =
    inherit OptionCell<World>(cb, key, defVal)

    override x.ConvertValue(name) = World.GetWorldByName(name)

type XivOption() as x =
    inherit CommandOption(UndefinedOptionHandling = UndefinedOptionHandling.AsNonOption)

    static let defaultServer = World.GetWorldByName("拉诺西亚")

    // 因为下游可能需要第一个也可能需要全部，所以保留显式的Cell
    member val World = XivWorldOpt(x, "world", defaultServer)

    override x.PreParse(args) =
        // 将服务器名称转换为指令，同时展开大区查询
        seq {
            for arg in args do
                if World.DefinedWorld(arg) then
                    yield "world:" + arg
                elif World.DefinedDC(arg) then
                    let dc = World.GetDCByName(arg)

                    let ss =
                        World.GetWorldsByDC(dc)
                        |> Seq.map (fun x -> $"world:%s{x.WorldName}")

                    yield! ss
                else
                    yield arg
        }