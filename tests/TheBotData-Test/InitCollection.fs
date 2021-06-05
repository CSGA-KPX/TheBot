﻿module InitDatabase

open System

open KPX.TheBot.Data.Common.Database

open Expecto


let mutable private initCollection = true

let initDatabase =
    testCase "加载数据库"
    <| fun _ ->
        try
            if initCollection then
                Environment.CurrentDirectory <- IO.Path.Join(__SOURCE_DIRECTORY__, "/../../build/static/")
                BotDataInitializer.ClearCache()
                BotDataInitializer.ShrinkCache()
                BotDataInitializer.InitializeAllCollections()
                initCollection <- false
        with e -> failwithf $"{e}"
