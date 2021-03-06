﻿// Copyright (c) Stephen Tetley 2019
// License: BSD 3 Clause

#r "netstandard"
#r "System.Xml.Linq"

open System.IO

#I @"C:\Users\stephen\.nuget\packages\ExcelProvider\1.0.1\lib\netstandard2.0"
#r "ExcelProvider.Runtime.dll"

#I @"C:\Users\stephen\.nuget\packages\ExcelProvider\1.0.1\typeproviders\fsharp41\netstandard2.0"
#r "ExcelDataReader.DataSet.dll"
#r "ExcelDataReader.dll"
#r "ExcelProvider.DesignTime.dll"
open FSharp.Interop.Excel

// Use FSharp.Data for CSV output
#I @"C:\Users\stephen\.nuget\packages\FSharp.Data\3.0.1\lib\netstandard2.0"
#r @"FSharp.Data.dll"
open FSharp.Data


#load "..\src\RewriteYourStrings\Internal\Levenshtein.fs"
#load "..\src\RewriteYourStrings\RewriteMonad.fs"
#load "..\src\RewriteYourStrings\Query.fs"
#load "..\src\RewriteYourStrings\Transform.fs"
open RewriteYourStrings.RewriteMonad
open RewriteYourStrings

type DocsTable = 
    ExcelFile< FileName = @"G:\work\Projects\rtu\edms_uploads_5.xlsx",
               SheetName = "Sheet1!",
               ForceString = true >

type DocsRow = DocsTable.Row


let readInstTable () : seq<DocsRow> = 
    let isBlank (row : DocsRow) = match row.GetValue(0) with null -> true | _ -> false 
    let allrows = (new DocsTable ()).Data
    allrows |> Seq.filter (not << isBlank)

let mm3xFirmware : Rewrite = 
    whenM (isLiteralMatch "MM3x RTU Firmware") 
          (constR "<<<Wrong title page, remake file>>>" )

let erskineTooLong1 : Rewrite = 
    replace1Re "Erskine Battery Asset Replacement Install Sheet" "S3953 Erskine Battery Replacement"

let erskineTooLong2 : Rewrite = 
    replace1Re "Erskine Battery Asset Replacement" "S3953 Erskine Battery Replacement"


let notMMIMUpgrade : Rewrite = 
    replace1Re "S3953 RTU MMIM Upgrade Manual" "S3953 RTU Asset Replacement"

let mk5Upgrade : Rewrite = 
    replace1Re "MK5 MMIM Upgrade Site Works" "S3953 Mk5 MMIM Asset Upgrade"

let replaceUnderscore : Rewrite = 
    charReplace '_' ' '

let rewriteTitle : Rewrite = 
    trim 
        >>. choice [ mm3xFirmware
                   ; erskineTooLong1
                   ; erskineTooLong2
                   ; notMMIMUpgrade
                   ; mk5Upgrade 
                   ]
        >>. replaceUnderscore

[<Literal>]
let ResultSchema = 
    "Title(string), File Date(string), To Update(string), New Title(string)"

type ResultTable = 
    CsvProvider< Sample = ResultSchema,
                 Schema = ResultSchema,
                 HasHeaders = true >

type ResultRow = ResultTable.Row

let makeTriple (docuRow : DocsRow) : ResultRow =
    let input = docuRow.Title.Trim()
    let output = rewriteOrId rewriteTitle input
    let action = if input <> output then "To Change" else "[match]"
    new ResultRow(input, docuRow.``File Date``, action, output)


let main () = 
    let outfile = @"G:\work\Projects\rtu\edms_rename.csv"
    let rows = 
        readInstTable () 
            |> Seq.sortBy (fun row -> (row.``File Date``, row.Title))
            |> Seq.map makeTriple
            
    let table = new ResultTable(rows)
    use sw = new StreamWriter(path=outfile, append=false)
    table.Save(writer = sw, separator = ',', quote = '\"')


