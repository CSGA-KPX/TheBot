/// 此模块用于角色卡的CRUD操作
module TheBot.Module.TRpgModule.CardManager

open System

open LiteDB

open BotData.Common.LiteDBHelpers

open KPX.FsCqHttp.Handler

open TheBot.Module.TRpgModule.TRpgUtils
open TheBot.Module.TRpgModule.TRpgCharacterCard

[<Literal>]
let MAX_USER_CARDS = 16

let private cardCol = 
    let col = TRpgDb.GetCollection<CharacterCard>()
    col.EnsureIndex(BsonExpression.Create("UserId"), false) |> ignore
    col.EnsureIndex(BsonExpression.Create("ChrName"), false) |> ignore
    col

let CardExists (c : CharacterCard) = cardCol.TryFindById(BsonValue(c.Id)).IsSome

let InsertCard (c : CharacterCard) = cardCol.Insert(c) |> ignore

let UpsertCard (c : CharacterCard) = cardCol.Upsert(c) |> ignore

let RemoveCard (c : CharacterCard) = cardCol.Delete(BsonValue(c.Id)) |> ignore

[<CLIMutable>]
type CurrentCard = 
    {
        [<BsonIdAttribute(false)>]
        UserId : uint64
        CardId : int64
    }

let private currentCardCol = TRpgDb.GetCollection<CurrentCard>()

let SetCurrentCard(uid, card : CharacterCard) = 
    currentCardCol.Upsert({
        CurrentCard.UserId = uid
        CurrentCard.CardId = card.Id
    }) |> ignore


let CountUserCard (uid : uint64) =
    cardCol.Count(Query.EQ("UserId", BsonValue.op_Implicit(uid)))

type CommandEventArgs with
    
    /// 获取发送用户的所有角色卡
    member x.GetChrCards() = 
        let uid = x.MessageEvent.UserId
        cardCol.Find(Query.EQ("UserId", BsonValue.op_Implicit(uid)))
        |> Seq.toArray

    /// 获取当前角色卡
    member x.TryGetChrCard() = 
        let uid = x.MessageEvent.UserId
        // TODO : TryFindById
        let ret = currentCardCol.FindById(BsonValue.op_Implicit(uid))
        if isNull (box ret) then 
            None
        else
            let key = ret.CardId
            let card = cardCol.FindById(BsonValue(key))
            if isNull (box card) then
                currentCardCol.Delete(BsonValue.op_Implicit(uid)) |> ignore
                None
            else
                printfn "%A" card
                Some card

    /// 获取当前角色卡
    member x.GetChrCard() = 
        let card = x.TryGetChrCard()
        if card.IsNone then
            raise <| ModuleException(InputError, "没有设置当前角色卡")
        card.Value