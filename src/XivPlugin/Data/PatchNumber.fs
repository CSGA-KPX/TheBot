namespace KPX.XivPlugin.Data

open System.Text.RegularExpressions

open LiteDB


[<Struct>]
type PatchNumber(numCode: int) =
    static let logger = NLog.LogManager.GetLogger("PatchNumber")

    member x.PatchNumber = numCode

    member x.MajorPatch = numCode / 100

    member x.IsSameExpansion(patch: PatchNumber) = x.MajorPatch = patch.MajorPatch

    member x.MaxLevel =
        match x.MajorPatch with
        | 2 -> 50
        | 3 -> 60
        | 4 -> 70
        | 5 -> 80
        | 6 -> 90
        | _ ->
            logger.Warn($"未知版本号{numCode}")
            50

    static member val Patch_Invalid = PatchNumber(999)
    static member val Patch2_0 = PatchNumber(200)
    static member val Patch3_0 = PatchNumber(300)
    static member val Patch4_0 = PatchNumber(400)
    static member val Patch5_0 = PatchNumber(500)
    static member val Patch6_0 = PatchNumber(600)

    static member val RegexPattern = Regex("^[2-6].[0-5]\d?$", RegexOptions.Compiled)

    static member FromPlayerLevel(level: int) =
        if level <= 0 || level > 90 then
            invalidArg (nameof level) $"指定等级{level}不在许可范围内"
        elif level <= 50 then
            PatchNumber.Patch2_0
        elif level <= 60 then
            PatchNumber.Patch3_0
        elif level <= 70 then
            PatchNumber.Patch4_0
        elif level <= 80 then
            PatchNumber.Patch5_0
        elif level <= 90 then
            PatchNumber.Patch6_0
        else
            invalidArg (nameof level) $"指定等级{level}不在许可范围内"

type PatchNumberInstructions() =
    inherit KPX.TheBot.Host.PluginPrerunInstruction()

    override x.RunInstructions() =
        BsonMapper.Global.RegisterType<PatchNumber>(
            (fun x -> BsonValue(x.PatchNumber)),
            (fun v -> PatchNumber(v.AsInt32))
        )