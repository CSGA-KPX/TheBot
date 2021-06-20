/// 此模块用于角色卡的CRUD操作
[<RequireQualifiedAccess>]
module KPX.TheBot.Module.TRpgModule.CardManager

open KPX.FsCqHttp
open KPX.FsCqHttp.Handler

open KPX.TheBot.Data.Common.Database
open KPX.TheBot.Module.TRpgModule.TRpgCharacterCard

open LiteDB


[<CLIMutable>]
[<RequireQualifiedAccess>]
type CurrentCard =
    { [<BsonId(false)>]
      UserId : uint64
      CardId : int64 }

    static member FromCard(c : CharacterCard) =
        { CurrentCard.UserId = c.UserId
          CurrentCard.CardId = c.Id }

[<Literal>]
let MAX_USER_CARDS = 16

let private db = getLiteDB "TRpgModule.db"
let private cardCol = db.GetCollection<CharacterCard>()
let private currCol = db.GetCollection<CurrentCard>()

let exists (c : CharacterCard) =
    cardCol.TryFindById(BsonValue(c.Id)).IsSome

let insert (c : CharacterCard) = cardCol.Insert(c) |> ignore

let upsert (c : CharacterCard) = cardCol.Upsert(c) |> ignore

let remove (c : CharacterCard) =
    cardCol.Delete(BsonValue(c.Id)) |> ignore

let setCurrent (c : CharacterCard) = currCol.Upsert(CurrentCard.FromCard(c)) |> ignore

let count (uid : UserId) =
    cardCol.Count(Query.EQ("UserId", BsonValue.op_Implicit uid.Value))

let getCards (uid : UserId) =
    cardCol.Find(Query.EQ("UserId", BsonValue.op_Implicit uid.Value))
    |> Seq.toArray
    
let getByName (uid : UserId, name : string) =
    let query =
        Query.And(
            Query.EQ("UserId", BsonValue.op_Implicit uid.Value),
            Query.EQ("ChrName", BsonValue(name))
        )
        
    cardCol.TryFindOne(query)

let tryGetCurrentCard (uid : UserId) =
    currCol.TryFindById(BsonValue.op_Implicit uid.Value)
    |> Option.map
        (fun ret ->
            let key = ret.CardId
            let card = cardCol.TryFindById(BsonValue(key))
            if card.IsNone then
                currCol.Delete(BsonValue.op_Implicit uid.Value) |> ignore
            card)
    |> Option.flatten

let getCurrentCard (uid : UserId) =
    let ret = tryGetCurrentCard uid
    
    if ret.IsSome then
        ret.Value
    else
        raise <| ModuleException(InputError, "没有设置当前角色")
    
let nameExists (uid : UserId, name : string) =
    let query =
        Query.And(
            Query.EQ("UserId.Value", BsonValue.op_Implicit uid.Value),
            Query.EQ("ChrName", BsonValue(name))
        )

    cardCol.Exists(query)

do
    cardCol.EnsureIndex(BsonExpression.Create("UserId.Value"), false)
    |> ignore

    cardCol.EnsureIndex(BsonExpression.Create("ChrName"), false)
    |> ignore