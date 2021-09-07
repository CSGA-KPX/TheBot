namespace KPX.TheBot.Data.Common.Database

open System.Collections.Generic

open LiteDB


[<AutoOpen>]
module LiteDBExtensions =
    type ILiteCollection<'T> with
        member x.SafeFindById(id : obj) =
            let ret = x.FindById(BsonValue(id))

            if isNull (box ret) then
                let msg = $"不能在%s{x.Name}中找到%A{id}"
                raise <| KeyNotFoundException(msg)

            ret

        member x.TryFindById(id : obj) =
            let ret = x.FindById(BsonValue(id))
            if isNull (box ret) then None else Some ret

        member x.TryFindOne(query : Query) =
            let ret = x.FindOne(query)
            if isNull (box ret) then None else Some ret

        member x.TryFindOne(expr : BsonExpression) =
            let ret = x.FindOne(expr)
            if isNull (box ret) then None else Some ret