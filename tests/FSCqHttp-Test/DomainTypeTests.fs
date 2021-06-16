module DomainTypeTests

open KPX.FsCqHttp

open Expecto

open Newtonsoft.Json
open Newtonsoft.Json.Linq


[<JsonConverter(typeof<StringEnumConverter<TestUnion>>)>]
type TestUnion =
    | Test1
    | T_E_S_T
    | [<AltStringEnumValue("Test2")>] T_E_S_T_2

[<Tests>]
let domainTypeTests =
    testList "DomainTypeTests" [
        testCase "SingleCaseInlineConvert" <| fun _ ->
            let t = UserId 123456UL
            Expect.equal (JValue.FromObject(t).ToObject<UserId>()) t "UserId"
            
            let t = GroupId 123456UL
            Expect.equal (JValue.FromObject(t).ToObject<GroupId>()) t "GroupId"
            
            let t = MessageId 123456
            Expect.equal (JValue.FromObject(t).ToObject<MessageId>()) t "MessageId"
            
        testCase "StringEnumConvert" <| fun _ ->
            Expect.equal (JValue.Parse("\"Test1\"").ToObject<TestUnion>()) Test1 "Normal"
            Expect.equal (JValue.Parse("\"tEST1\"").ToObject<TestUnion>()) Test1 "NormalCasing"
            Expect.equal (JValue.FromObject(Test1).ToObject<TestUnion>()) Test1 "NormalConvert"
            
            Expect.equal (JValue.Parse("\"T_E_S_T\"").ToObject<TestUnion>()) T_E_S_T "Complex"
            Expect.equal (JValue.Parse("\"t_e_s_t\"").ToObject<TestUnion>()) T_E_S_T "ComplexCasing"
            Expect.equal (JValue.FromObject(T_E_S_T).ToObject<TestUnion>()) T_E_S_T "ComplexConvert"
            
            Expect.equal (JValue.Parse("\"Test2\"").ToObject<TestUnion>()) T_E_S_T_2 "Alt"
            Expect.equal (JValue.Parse("\"test2\"").ToObject<TestUnion>()) T_E_S_T_2 "AltCasing"
            Expect.equal (JValue.FromObject(T_E_S_T_2).ToObject<TestUnion>()) T_E_S_T_2 "AltConvert"
    ]