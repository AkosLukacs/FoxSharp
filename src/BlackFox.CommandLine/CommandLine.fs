namespace BlackFox.CommandLine

/// Escape arguments in a form that programs parsing it as Microsoft C Runtime will successfuly understand
/// Rules taken from http://www.daviddeley.com/autohotkey/parameters/parameters.htm#WINARGV
module MsvcrCommandLine =
    open System.Text

    let escapeArg (doubleQuoteEscape: bool) (arg : string) (builder : StringBuilder) =
        let needQuote = arg.Contains(" ") || arg.Contains("\t") || arg.Length = 0
        let rec escape (builder: StringBuilder) pos =
            if pos >= arg.Length then
                ()
            else
                let c = arg.[pos]
                let isLast = pos = arg.Length-1
                match c with
                | '"' -> // Quotes are escaped
                    if doubleQuoteEscape then
                        escape (builder.Append(@"""""")) (pos + 1)
                    else
                        escape (builder.Append(@"\""")) (pos + 1)
                | '\\' when isLast && needQuote -> // Backslash ending a quoted arg need escape
                    escape (builder.Append(@"\\")) (pos + 1)
                | '\\' when not isLast -> // Backslash followed by quote need to be escaped
                    let nextC = arg.[pos+1]
                    match nextC with
                    | '"' ->
                        escape (builder.Append(@"\\\""")) (pos + 2)
                    | _ ->
                        escape (builder.Append(c)) (pos + 1)
                | _ ->
                    escape (builder.Append(c)) (pos + 1)

        if needQuote then builder.Append('"') |> ignore
        escape builder 0
        if needQuote then builder.Append('"') |> ignore

    let escape cmdLine =
        let builder = StringBuilder()
        cmdLine |> Seq.iteri (fun i arg ->
            if (i <> 0) then builder.Append(' ') |> ignore
            escapeArg false arg builder)

        builder.ToString()

type CmdLineArgType = | Normal of string | Raw of string

type CmdLine = {
    Args: CmdLineArgType list
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CmdLine =
    open System
    open System.Text

    let empty = { Args = List.empty }

    let appendRaw value (cmdLine : CmdLine) =
        { cmdLine with Args = Raw(value) :: cmdLine.Args }

    let appendRawIfSome value (cmdLine : CmdLine) =
        match value with
        | Some(value) -> appendRaw value cmdLine
        | None -> cmdLine

    let concat (other : CmdLine) (cmdLine : CmdLine) =
        { cmdLine with Args = other.Args @ cmdLine.Args }

    let append (value : 'a) (cmdLine : CmdLine) =
        let s =
            match box value with
            | :? String as sv -> sv
            | _ -> sprintf "%A" value

        { cmdLine with Args = Normal(s) :: cmdLine.Args }

    let appendSeq (values: 'a seq) (cmdLine : CmdLine) =
        values |> Seq.fold (fun state o -> append o state) cmdLine

    let appendIfTrue cond value cmdLine =
        if cond then cmdLine |> append value else cmdLine

    let appendIfSome value cmdLine =
        match value with
        | Some(value) -> cmdLine |> append value
        | None -> cmdLine

    let appendSeqIfSome values (cmdLine : CmdLine) =
        match values with
        | Some(value) -> appendSeq value cmdLine
        | None -> cmdLine

    let appendIfNotNullOrEmpty value prefix cmdLine =
        appendIfTrue (not (String.IsNullOrEmpty(value))) (prefix+value) cmdLine

    let inline private argsInOrder cmdLine =
        cmdLine.Args |> List.rev

    let private escape escapeFun cmdLine =
        let builder = StringBuilder()
        cmdLine |> argsInOrder |> Seq.iteri (fun i arg ->
            if (i <> 0) then builder.Append(' ') |> ignore
            match arg with
            | Normal(arg) -> escapeFun arg builder
            | Raw(arg) -> builder.Append(arg) |> ignore)

        builder.ToString()

    let toStringForMsvcr doubleQuoteEscape cmdLine =
        escape (MsvcrCommandLine.escapeArg doubleQuoteEscape) cmdLine

    let toString cmdLine =
        toStringForMsvcr false cmdLine

    let fromSeq (values : string seq) =
        values |> Seq.fold (fun state o -> append o state) empty