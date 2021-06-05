module TheBotData_Test

open Expecto

[<Tests>]
let MainTestGroup =
    testSequencedGroup "MainTestGroup"
    <| testList
        "MainTestGroupList"
        [ yield InitDatabase.initDatabase
          yield XivTest.xivTests
          yield EveTest.eveTests ]

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssemblyWithCLIArgs [] argv
