module Database

open FSharp.Data.Sql
open System.Transactions
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

let private submitUpdatesAsTransaction (ctx: DbContext) = async {
  use transaction = 
    new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)
  do! ctx.SubmitUpdatesAsync()
  transaction.Complete()
}


let submitUpdates (ctx: DbContext) = 
  ctx.SubmitUpdatesAsync()
  |> Async.Catch
  |> Async.map ofChoice
  |> AR
