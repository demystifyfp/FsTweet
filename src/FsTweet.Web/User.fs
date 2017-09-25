module User 
  
open Chessie.ErrorHandling
open Chessie
open BCrypt.Net

type Username = private Username of string with
  static member TryCreate (username : string) =
    match username with
    | null | ""  -> fail "Username should not be empty"
    | x when x.Length > 12 -> fail "Username should not be more than 12 characters"
    | x -> x.Trim().ToLowerInvariant() |> Username |> ok
  static member TryCreateAsync username =
    Username.TryCreate username
    |> mapFailure (System.Exception)
    |> Async.singleton
    |> AR
  member this.Value = 
    let (Username username) = this
    username
    
type UserId = UserId of int

type EmailAddress = private EmailAddress of string with
  member this.Value =
    let (EmailAddress emailAddress) = this
    emailAddress  
  static member TryCreate (emailAddress : string) =
   try 
     new System.Net.Mail.MailAddress(emailAddress) |> ignore
     emailAddress.Trim().ToLowerInvariant() |>  EmailAddress  |> ok
   with
     | _ -> fail "Invalid Email Address"

type Password = private Password of string with 
  member this.Value =
    let (Password password) = this
    password
  static member TryCreate (password : string) =
    match password with
    | null | ""  -> fail "Password should not be empty"
    | x when x.Length < 4 || x.Length > 8 -> fail "Password should contain only 4-8 characters"
    | x -> Password x |> ok

type PasswordHash = private PasswordHash of string with
  member this.Value =
    let (PasswordHash passwordHash) = this
    passwordHash

  static member Create (password : Password) =
    BCrypt.HashPassword(password.Value)
    |> PasswordHash 

type UserEmail = 
| Verified of EmailAddress
| NotVerified of EmailAddress

type User = {
  UserId : UserId
  Username : Username
  Email : UserEmail
  PasswordHash : PasswordHash
}

type FindUser = Username -> AsyncResult<User option, System.Exception>


module Persistence =
  open Database
  let findUser (getDataCtx : GetDataContext) (username : Username) = ()