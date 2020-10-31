/// 此模块用于角色卡的CRUD操作
module TheBot.Module.TRpgModule.CardManager

open System

open LiteDB

open KPX.FsCqHttp.Handler

open TheBot.Module.TRpgModule.TRpgUtils
open TheBot.Module.TRpgModule.TRpgCharacterCard


let private cardCol = 
    let col = TRpgDb.GetCollection<CharacterCard>()
    col.EnsureIndex(BsonExpression.Create("UserId"), false) |> ignore
    col.EnsureIndex(BsonExpression.Create("ChrName"), false) |> ignore
    col

type private CurrentCard = 
    {
        Id : int
        UserId : uint64
        CardId : string
    }

type CommandArgs with
    
    /// 获取发送用户的所有角色卡
    member x.GetChrCards() = 
        ()

    /// 获取当前角色卡
    member x.GetChrCard() = 
        ()
