module BotData.Common.LiteDBHelpers

open LiteDB

type ILiteCollection<'T> with
    member x.TryFindById(id : BsonValue) = 
        let ret = x.FindById(id)
        if isNull (box ret) then
            None
        else
            Some ret