module BotData.XivData.Item.Utils

open LibFFXIV.GameData.Raw

let TryGetToOption(x : bool, y : 'Value) =
    if x then Some(y)
    else None

/// 整合两个不同版本的表
//
/// b >= a
//
/// func a b -> bool = true then b else a
let MergeSheet(a : IXivSheet, b : IXivSheet, func : XivRow * XivRow -> bool ) = 
    if a.Name <> b.Name then
        invalidOp "Must merge on same sheet!"

    seq {
        for row in b do
            if a.ContainsKey(row.Key) then
                let rowA = a.[row.Key.Main, row.Key.Alt]
                let ret = func(rowA, row)
                if ret then
                    yield row
                else
                    yield rowA
            else
                yield row
    }