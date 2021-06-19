namespace KPX.TheBot.Module.TRpgModule.StCommand

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Utils.Subcommands


type StShowCommandOpts() as x =
    inherit OptionBase()

    let skillName =
        OptionCellSimple(x, "skill", "", ArgIndex = Some 0)

    member x.SkillName =
        if skillName.IsDefined then Some skillName.Value else None

type PcSubcommands =
    | List
    | [<AltCommandName("rm")>] Remove of name : string
    | [<AltCommandName("clr")>] Clear
    /// 只返回属性值和非默认值的技能（啥意思？）
    | Get
    | Show of StShowCommandOpts
    | Lock
    | Unlock
    | Rename of newName : string
    | Set of name : string
    /// at单独获取
    | [<AltCommandName("cp")>] Copy of fromName : string

    interface ISubcommandTemplate with
        member x.Usage =
            match x with
            
            | List -> "看人物卡列表"
            | Remove _ -> "删除人物卡 remove 人物卡名称"
            | Clear -> "清空数据"
            | Get -> "获取当前人物卡属性"
            | Show _ -> "展示人物卡属性 show/show 属性名称"
            | Lock -> "锁定当前人物卡"
            | Unlock -> "解锁当前人物卡"
            | Rename _ -> "人物卡改名 rename 新名字"
            | Copy _ -> "人物卡复制 copy 人物卡名 @接收方"
            | Set _ -> "设置当前人物卡 set 人物卡名"
