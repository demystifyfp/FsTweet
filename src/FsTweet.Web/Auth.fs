namespace Auth

module Domain = 
  open User
  open Chessie.ErrorHandling
  open Chessie

  type LoginRequest = {
    Username : Username
    Password : Password
  }
  with static member TryCreate (username, password) = 
        trial {
          let! username = Username.TryCreate username
          let! password = Password.TryCreate password
          return {
            Username = username
            Password = password
          }
        }

  type LoginError =
  | UsernameNotFound
  | EmailNotVerified
  | PasswordMisMatch
  | Error of System.Exception

  type Login = FindUser -> LoginRequest -> AsyncResult<User, LoginError>

  let login (findUser : FindUser) (req : LoginRequest) = asyncTrial {
    let! userToFind = 
      findUser req.Username |> AR.mapFailure Error
    match userToFind with
    | None -> 
      return! AR.fail UsernameNotFound
    | Some user -> 
      match user.Email with
      | NotVerified _ -> 
        return! AR.fail EmailNotVerified
      | Verified _ ->
        let isMatchingPassword =
          PasswordHash.VerifyPassword req.Password user.PasswordHash
        match isMatchingPassword with
        | false -> return! AR.fail PasswordMisMatch 
        | _ -> return user
  }

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Suave.DotLiquid
  open Suave.Form
  open Domain
  open Chessie.ErrorHandling
  open Chessie
  open User

  type LoginViewModel = {
    Username : string
    Password : string
    Error : string option
  }

  let emptyLoginViewModel = {
    Username = ""
    Password = ""
    Error = None
  }

  let loginTemplatePath = "user/login.liquid"

  let renderLoginPage (viewModel : LoginViewModel) = 
    page loginTemplatePath viewModel

  let onLoginSuccess (user : User) = 
    Successful.OK user.Username.Value

  let onLoginFailure viewModel loginError =
    match loginError with
    | PasswordMisMatch ->
       let vm = 
        {viewModel with Error = Some "password didn't match"}
       renderLoginPage vm
    | EmailNotVerified -> 
       let vm = 
        {viewModel with Error = Some "email not verified"}
       renderLoginPage vm
    | UsernameNotFound -> 
       let vm = 
        {viewModel with Error = Some "invalid username"}
       renderLoginPage vm
    | Error ex -> 
      printfn "%A" ex
      let vm = 
        {viewModel with Error = Some "something went wrong"}
      renderLoginPage vm
    
  let handleLoginResult viewModel loginResult = 
    either onLoginSuccess (onLoginFailure viewModel) loginResult

  let handleLoginAsyncResult viewModel aLoginResult = 
    aLoginResult
    |> Async.ofAsyncResult
    |> Async.map (handleLoginResult viewModel)


  let handleUserLogin findUser ctx = async {
    match bindEmptyForm ctx.request with
    | Choice1Of2 (vm : LoginViewModel) ->
      let result = 
        LoginRequest.TryCreate (vm.Username, vm.Password)
      match result with
      | Success req -> 
        let aLoginResult = login findUser req 
        let! webpart = 
          handleLoginAsyncResult vm aLoginResult
        return! webpart ctx
      | Failure err -> 
        let viewModel = {vm with Error = Some err}
        return! renderLoginPage viewModel ctx
    | Choice2Of2 err ->
      let viewModel = 
        {emptyLoginViewModel with Error = Some err}
      return! renderLoginPage viewModel ctx
  }

  let webpart getDataCtx =
    let findUser = Persistence.findUser getDataCtx
    path "/login" >=> choose [
      GET >=> renderLoginPage emptyLoginViewModel
      POST >=> handleUserLogin findUser
    ]