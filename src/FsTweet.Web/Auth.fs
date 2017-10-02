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
      match user.EmailAddress with
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
  open Suave.Authentication
  open Suave.Cookie
  open Suave.State.CookieStateStore

  type LoginViewModel = {
    Username : string
    Password : string
    ReturnPath: string
    Error : string option
  }

  let emptyLoginViewModel = {
    Username = ""
    Password = ""
    ReturnPath = ""
    Error = None
  }

  let loginTemplatePath = "user/login.liquid"

  let renderLoginPage (viewModel : LoginViewModel) = 
    page loginTemplatePath viewModel

  let userSessionKey = "fsTweetUser"

  let createUserSession (user : User) =
    statefulForSession 
    >=> context (fun ctx ->
                  match HttpContext.state ctx with
                  | Some state ->
                      state.set userSessionKey user
                  | _ -> never)
  let getLoggedInUser ctx : User option =
    match HttpContext.state ctx with
    | Some state -> 
      state.get userSessionKey
    | _ -> None

  let userSession fFailure fSuccess = 
    statefulForSession 
    >=> context (fun ctx ->
                  match getLoggedInUser ctx with
                  | Some user -> fSuccess user
                  | _ -> fFailure)
    

  let redirectToLoginPage (req : HttpRequest) = 
    let redirectUrl = 
      sprintf "/login?returnPath=%s" req.path
    Redirection.FOUND redirectUrl
  let onAnonymousAccess =
    request redirectToLoginPage
    
  let user fSuccess =
    userSession onAnonymousAccess fSuccess
  
  let onLoginSuccess viewModel (user : User) = 
    let redirectUrl = 
      match viewModel.ReturnPath with
      | "" -> "/wall"
      | x -> x
    authenticated CookieLife.Session false 
      >=> createUserSession user
      >=> Redirection.FOUND redirectUrl

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
    either 
      (onLoginSuccess viewModel)
      (onLoginFailure viewModel) loginResult

  let handleLoginAsyncResult viewModel aLoginResult = 
    aLoginResult
    |> Async.ofAsyncResult
    |> Async.map (handleLoginResult viewModel)


  let handleUserLogin findUser (ctx : HttpContext) = async {
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

  let renderLoginPageWithRedirect (request : HttpRequest) =
    let viewModel =
      match request.["returnPath"] with
      | None -> emptyLoginViewModel
      | Some returnPath -> 
        {emptyLoginViewModel with ReturnPath = returnPath}
    renderLoginPage viewModel

  let webpart getDataCtx =
    let findUser = Persistence.findUser getDataCtx
    path "/login" >=> choose [
      GET >=> request renderLoginPageWithRedirect
      POST >=> handleUserLogin findUser
    ]