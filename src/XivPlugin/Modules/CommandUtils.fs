module KPX.XivPlugin.Modules.Utils.CommandUtils

open KPX.XivPlugin.Data

open KPX.FsCqHttp.Utils.UserOption


let XivSpecialChars =
    [| '\ue03c' // HQ
       '\ue03d' |] //收藏品

type XivWorldOpt(cb: CommandOption, key, defVal) =
    inherit OptionCell<World>(cb, key, defVal)

    override x.ConvertValue(name) = World.GetWorldByName(name)

type XivOption() as x =
    inherit CommandOption(UndefinedOptionHandling = UndefinedOptionHandling.AsNonOption)

    static let defaultServer = World.GetWorldByName("拉诺西亚")

    let patchOpt = OptionCellSimple<float>(x, "patch", PatchNumber.Patch6_0.PatchNumber)

    // 因为下游可能需要第一个也可能需要全部，所以保留显式的Cell
    member val World = XivWorldOpt(x, "world", defaultServer)

    /// 版本号，Some表示用户指定了特定版本，None则未指定
    member x.PatchNumber =
        if patchOpt.IsDefined then
            Some(PatchNumber(patchOpt.Value * 100.0 |> int))
        else
            None

    override x.PreParse(args) =
        // 将服务器名称转换为指令，同时展开大区查询
        seq {
            for arg in args do
                if World.DefinedWorld(arg) then
                    yield $"world:%s{arg}"
                elif World.DefinedDC(arg) then
                    let dc = World.GetDCByName(arg)

                    let ss = World.GetWorldsByDC(dc) |> Seq.map (fun x -> $"world:%s{x.WorldName}")

                    yield! ss
                elif PatchNumber.RegexPattern.IsMatch(arg) then
                    yield $"patch:%s{arg}"
                else
                    yield arg
        }
