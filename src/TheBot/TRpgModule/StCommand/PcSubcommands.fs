namespace KPX.TheBot.Module.TRpgModule.StCommand

open KPX.FsCqHttp.Utils.UserOption
open KPX.FsCqHttp.Utils.Subcommands
open KPX.TheBot.Module.TRpgModule.Coc7


type StShowCommandOpts() as x =
    inherit OptionBase()

    let skillName =
        OptionCellSimple(x, "skill", "", ArgIndex = Some 0)

    member x.SkillName =
        if skillName.IsDefined then
            let pn = skillName.Value
            let succ, value = SkillNameAlias.TryGetValue(pn)
            if succ then Some value else Some pn
        else
            None

type PcSubcommands =
    | New of chrName : string
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
            | New _ -> "创建空白卡"
            | List -> "看角色列表"
            | Remove _ -> "删除角色 remove 角色名称"
            | Clear -> "（未实装）清空数据"
            | Get -> "（未实装）获取当前角色属性"
            | Show _ -> "展示角色属性 show/show 属性名称"
            | Lock -> "（未实装）锁定当前角色"
            | Unlock -> "（未实装）解锁当前角色"
            | Rename _ -> "角色改名 rename 新名字"
            | Copy _ -> "角色复制 copy 角色名 @接收方"
            | Set _ -> "设置当前角色 set 角色名"
