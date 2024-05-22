namespace TypeShape.Tests.FSharp

type FSharpRecord =
    {
        IntProperty: int
        StringProperty: string
        BoolProperty: bool
    }

[<Struct>]
type FSharpStructRecord =
    {
        IntProperty: int
        StringProperty: string
        BoolProperty: bool
    }

type GenericFSharpRecord<'T> =
    {
        GenericProperty: 'T
    }

[<Struct>]
type GenericFSharpStructRecord<'T> =
    {
        GenericProperty: 'T
    }

type FSharpClass(stringProperty: string, intProperty : int) =
    member _.StringProperty = stringProperty
    member _.IntProperty = intProperty

[<Struct>]
type FSharpStruct(stringProperty: string, intProperty : int) =
    member _.StringProperty = stringProperty
    member _.IntProperty = intProperty

type GenericFSharpClass<'T>(value: 'T) =
    member _.Value = value

[<Struct>]
type GenericFSharpStruct<'T>(value: 'T) =
    member _.Value = value