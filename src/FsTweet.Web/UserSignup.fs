namespace UserSignup

module Domain =
  open Chessie.ErrorHandling
  open System.Security.Cryptography
  open User
  open Chessie

  type UserSignupRequest = {
    Username : Username
    Password : Password
    EmailAddress : EmailAddress
  }
  with static member TryCreate (username, password, email) =
        trial {
          let! username = Username.TryCreate username
          let! password = Password.TryCreate password
          let! emailAddress = EmailAddress.TryCreate email
          return {
            Username = username
            Password = password
            EmailAddress = emailAddress
          }
        }

  let base64URLEncoding bytes =
    let base64String = 
       System.Convert.ToBase64String bytes
    base64String.TrimEnd([|'='|])
      .Replace('+', '-').Replace('/', '_')
      
  type VerificationCode = private VerificationCode of string with
    member this.Value =
      let (VerificationCode verificationCode) = this
      verificationCode
    static member Create () =
      use rngCsp = new RNGCryptoServiceProvider()
      let verificationCodeLength = 15
      let b : byte [] = 
        Array.zeroCreate verificationCodeLength
      rngCsp.GetBytes(b)
      base64URLEncoding b
      |> VerificationCode 

  type CreateUserRequest = {
    Username : Username
    PasswordHash : PasswordHash
    Email : EmailAddress
    VerificationCode : VerificationCode
  }

  type CreateUserError =
  | EmailAlreadyExists
  | UsernameAlreadyExists
  | Error of System.Exception

  type CreateUser = 
    CreateUserRequest -> AsyncResult<UserId, CreateUserError>
  type SignupEmailRequest = {
    Username : Username
    EmailAddress : EmailAddress
    VerificationCode : VerificationCode
  }
  type SendEmailError = SendEmailError of System.Exception
  
  type SendSignupEmail = SignupEmailRequest -> AsyncResult<unit, SendEmailError>

  type UserSignupError =
  | CreateUserError of CreateUserError
  | SendEmailError of SendEmailError

  type SignupUser = 
    CreateUser -> SendSignupEmail -> UserSignupRequest 
      -> AsyncResult<UserId, UserSignupError>
 
  type VerifyUser = string -> AsyncResult<Username option, System.Exception>

  let signupUser (createUser : CreateUser) 
                 (sendEmail : SendSignupEmail) 
                 (req : UserSignupRequest) = asyncTrial {

    let createUserReq = {
      PasswordHash = PasswordHash.Create req.Password
      Username = req.Username
      Email = req.EmailAddress
      VerificationCode = VerificationCode.Create()
    }

    let! userId = 
      createUser createUserReq
      |> AR.mapFailure CreateUserError

    let sendEmailReq = {
      Username = req.Username
      VerificationCode = createUserReq.VerificationCode
      EmailAddress = createUserReq.Email
    }
    do! sendEmail sendEmailReq 
        |> AR.mapFailure SendEmailError

    return userId
  }

module Persistence =
  open Domain
  open Chessie.ErrorHandling
  open Database
  open System
  open FSharp.Data.Sql
  open Chessie
  open User
  
  let private mapException (ex : System.Exception) =
    match ex with
    | UniqueViolation "IX_Users_Email" _ ->
      EmailAlreadyExists
    | UniqueViolation "IX_Users_Username" _ -> 
      UsernameAlreadyExists
    | _ -> Error ex

  let createUser (getDataCtx : GetDataContext) 
    (createUserReq : CreateUserRequest) = asyncTrial {
    let ctx = getDataCtx ()
    let users = ctx.Public.Users
    
    let newUser = users.Create()
    newUser.Email <- createUserReq.Email.Value
    newUser.EmailVerificationCode <- 
      createUserReq.VerificationCode.Value
    newUser.Username <- createUserReq.Username.Value
    newUser.IsEmailVerified <- false
    newUser.PasswordHash <- createUserReq.PasswordHash.Value
    

    do! submitUpdates ctx 
        |> AR.mapFailure mapException

    printfn "User Created %A" newUser.Id
    return UserId newUser.Id
  }

  let verifyUser (getDataCtx : GetDataContext) (verificationCode : string) = asyncTrial {
    let ctx = getDataCtx ()
    let! userToVerify = 
      query {
        for u in ctx.Public.Users do
        where (u.EmailVerificationCode = verificationCode)
      } |> Seq.tryHeadAsync |> AR.catch
    match userToVerify with
    | None -> return None
    | Some user ->
      user.EmailVerificationCode <- ""
      user.IsEmailVerified <- true
      do! submitUpdates ctx
      let! username = Username.TryCreateAsync user.Username
      return Some username
  }
    
module Email =
  open Domain
  open Chessie.ErrorHandling
  open Email
  open Chessie

  let sendSignupEmail sendEmail signupEmailReq = asyncTrial {
    let verificationCode =
      signupEmailReq.VerificationCode.Value
    let placeHolders = 
      Map.empty
        .Add("verification_code", verificationCode)
        .Add("username", signupEmailReq.Username.Value)
    let email = {
      To = signupEmailReq.EmailAddress.Value
      TemplateId = int64(3160924)
      PlaceHolders = placeHolders
    }
    do! sendEmail email 
      |> AR.mapFailure Domain.SendEmailError
  }

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Suave.DotLiquid
  open Suave.Form
  open Domain
  open Chessie.ErrorHandling
  open Database
  open Chessie
  open User

  type UserSignupViewModel = {
    Username : string
    Email : string
    Password: string
    Error : string option
  }  
  let emptyUserSignupViewModel = {
    Username = ""
    Email = ""
    Password = ""
    Error = None
  }

  let signupTemplatePath = "user/signup.liquid" 

  let handleCreateUserError viewModel = function 
  | EmailAlreadyExists ->
    let viewModel = 
      {viewModel with Error = Some ("email already exists")}
    page signupTemplatePath viewModel
  | UsernameAlreadyExists ->
    let viewModel = 
      {viewModel with Error = Some ("username already exists")}
    page signupTemplatePath viewModel
  | Error ex ->
    printfn "Server Error : %A" ex
    let viewModel = 
      {viewModel with Error = Some ("something went wrong")}
    page signupTemplatePath viewModel

  let handleSendEmailError viewModel err =
    printfn "error while sending email : %A" err
    let viewModel = 
      {viewModel with Error = Some ("something went wrong")}
    page signupTemplatePath viewModel

  let onUserSignupFailure viewModel err = 
    match err with
    | CreateUserError cuErr ->
      handleCreateUserError viewModel cuErr
    | SendEmailError err ->
      handleSendEmailError viewModel err

  let onUserSignupSuccess viewModel _ =
    sprintf "/signup/success/%s" viewModel.Username
    |> Redirection.FOUND 

  let handleUserSignup signupUser ctx = async {
    match bindEmptyForm ctx.request with
    | Choice1Of2 (vm : UserSignupViewModel) ->
      let result =
        UserSignupRequest.TryCreate (vm.Username, vm.Password, vm.Email)
      match result with
      | Success userSignupReq ->
        let! webpart =
          signupUser userSignupReq
          |> AR.either (onUserSignupSuccess vm) (onUserSignupFailure vm)
        return! webpart ctx
      | Failure msg ->
        let viewModel = {vm with Error = Some msg}
        return! page signupTemplatePath viewModel ctx
    | Choice2Of2 err ->
      let viewModel = {emptyUserSignupViewModel with Error = Some err}
      return! page signupTemplatePath viewModel ctx
  }

  let onVerificationSuccess username =
    match username with
    | Some (username : Username) ->
      page "user/verification_success.liquid" username.Value
    | _ ->
      page "not_found.liquid" "invalid verification code"

  let onVerificationFailure (ex : System.Exception) =
    printfn "%A" ex
    page "server_error.liquid" "error while verifying email"

  let handleSignupVerify (verifyUser : VerifyUser) verificationCode ctx = async {
    let! webpart = 
      verifyUser verificationCode 
      |> AR.either onVerificationSuccess onVerificationFailure
    return! webpart ctx
  }
  

  let webPart getDataCtx sendEmail =
    let createUser = Persistence.createUser getDataCtx
    let sendSignupEmail = Email.sendSignupEmail sendEmail
    let signupUser = Domain.signupUser createUser sendSignupEmail
    let verifyUser = Persistence.verifyUser getDataCtx
    choose [
      path "/signup" 
        >=> choose [
          GET >=> page signupTemplatePath emptyUserSignupViewModel
          POST >=> handleUserSignup signupUser
        ]
      pathScan "/signup/success/%s" (page "user/signup_success.liquid")
      pathScan "/signup/verify/%s" (handleSignupVerify verifyUser)
    ]