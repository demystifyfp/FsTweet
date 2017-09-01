module Database

open FSharp.Data.Sql
open Chessie.ErrorHandling

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

type DbContext = Db.dataContext

let getDbContext (connString : string) : DbContext =
  let isMono = 
    System.Type.GetType ("Mono.Runtime") <> null
  match isMono with
  | true -> 
    let opts : Transactions.TransactionOptions = {
      IsolationLevel = Transactions.IsolationLevel.DontCreateTransaction
      Timeout = System.TimeSpan.MaxValue
    } 
    Db.GetDataContext(connString, opts)
  | _ -> 
    Db.GetDataContext connString

// https://github.com/fsprojects/SQLProvider/issues/112#issuecomment-150917285
let clearUpdatesOnError (ctx: DbContext) aChoice = async {
  let! choice = aChoice
  match choice with
  | Choice2Of2 ex -> 
    ctx.ClearUpdates() |> ignore
  | _ -> ()
  return choice
}
let submitUpdates (ctx: DbContext) = 
  ctx.SubmitUpdatesAsync()
  |> Async.Catch
  |> clearUpdatesOnError ctx
  |> Async.map ofChoice
  |> AR
