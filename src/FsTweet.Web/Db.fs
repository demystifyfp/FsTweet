module Database

open FSharp.Data.Sql
open Chessie.ErrorHandling
open System
open Npgsql
open Chessie

[<Literal>]
let private connString = 
  "Server=127.0.0.1;Port=5432;Database=FsTweet;User Id=postgres;Password=test;"

[<Literal>]
let private npgsqlLibPath = @"./../../packages/database/Npgsql/lib/net451"

[<Literal>]
let private dbVendor = Common.DatabaseProviderTypes.POSTGRESQL

type Db = SqlDataProvider<
            ConnectionString=connString,
            DatabaseVendor=dbVendor,
            ResolutionPath=npgsqlLibPath,
            UseOptionTypes=true>

type DataContext = Db.dataContext

type GetDataContext = unit -> DataContext
let dataContext (connString : string) : GetDataContext =
  let isMono = 
    System.Type.GetType ("Mono.Runtime") <> null
  match isMono with
  | true -> 
    // SQLProvider doesn't support async transaction in mono
    let opts : Transactions.TransactionOptions = {
      IsolationLevel = Transactions.IsolationLevel.DontCreateTransaction
      Timeout = System.TimeSpan.MaxValue
    } 
    fun _ -> Db.GetDataContext(connString, opts)
  | _ -> 
    fun _ -> Db.GetDataContext connString


let submitUpdates (ctx: DataContext) = 
  ctx.SubmitUpdatesAsync()
  |> AR.catch

let (|UniqueViolation|_|) constraintName (ex : Exception) =
  match ex with
  | :? AggregateException as agEx  ->
    match agEx.Flatten().InnerException with 
    | :? PostgresException as pgEx ->
      if pgEx.ConstraintName = constraintName && 
          pgEx.SqlState = "23505" then
        Some ()
      else None
    | _ -> None
  | _ -> None
