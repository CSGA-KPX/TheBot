namespace LibFFXIV.GameData.Provider

#nowarn "25"

open System.Reflection

open ProviderImplementation.ProvidedTypes

open FSharp.Core.CompilerServices

open LibFFXIV.GameData


[<Sealed>]
[<TypeProvider>]
type XivCollectionProvider(cfg: TypeProviderConfig) as x =
    inherit TypeProviderForNamespaces(cfg)

    let ns = "LibFFXIV.GameData.Provided"
    let asm = Assembly.GetExecutingAssembly()

    let colProvType =
        ProvidedTypeDefinition(asm, ns, "XivCollectionProvider", None, hideObjectMethods = true, nonNullable = true)

    let tpParameters =
        [ ProvidedStaticParameter("Archive", typeof<string>)
          ProvidedStaticParameter("Language", typeof<string>)
          ProvidedStaticParameter("Prefix", typeof<string>)
          ProvidedStaticParameter("HintJson", typeof<string>, System.String.Empty)
          ProvidedStaticParameter("Id", typeof<string>, System.String.Empty)]

    do
        colProvType.DefineStaticParameters(
            tpParameters,
            fun (typeName: string) (args: obj[]) ->
                let lang = XivLanguage.FromString(args.[1] :?> string)
                let archive = args.[0] :?> string
                let prefix = args.[2] :?> string

                let hint =
                    let str = args.[3] :?> string
                    if str = System.String.Empty then None else Some str

                let id = args.[4] :?> string

                let hdrCache = XivHeaderCache()

                // 忘了干什么的了
                if hdrCache.TryBuild(lang, archive, prefix, ?hintJsonDIr = hint) then
                    ()
                else
                    ()

                
                ProviderContext(hdrCache, id).ProvideFor(x, typeName)
        )

    do x.AddNamespace(ns, [ colProvType ])

[<assembly: TypeProviderAssembly>]
do ()
