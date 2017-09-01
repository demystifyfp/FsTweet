namespace UserSignup

module Domain =
  open Chessie.ErrorHandling
  open BCrypt.Net
  open System.Security.Cryptography

  type Username = private Username of string with
    static member TryCreate (username : string) =
      match username with
      | null | ""  -> fail "Username should not be empty"
      | x when x.Length > 12 -> fail "Username should not be more than 12 characters"
      | x -> x.Trim().ToLowerInvariant() |> Username |> ok
    member this.Value = 
      let (Username username) = this
      username

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

  type PasswordHash = private PasswordHash of string with
    member this.Value =
      let (PasswordHash passwordHash) = this
      passwordHash

    static member Create (password : Password) =
      BCrypt.HashPassword(password.Value)
      |> PasswordHash
      
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
      System.Convert.ToBase64String b
      |> VerificationCode 

  type CreateUserRequest = {
    Username : Username
    PasswordHash : PasswordHash
    Email : EmailAddress
    VerificationCode : VerificationCode
  }

  type UserId = UserId of int

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

  let mapFailure f aResult = 
    let mapFirstItem xs = 
      List.head xs |> f |> List.singleton 
    mapFailure mapFirstItem aResult

  let mapAsyncFailure f aResult =
    aResult
    |> Async.ofAsyncResult 
    |> Async.map (mapFailure f) |> AR

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
      |> mapAsyncFailure CreateUserError

    let sendEmailReq = {
      Username = req.Username
      VerificationCode = createUserReq.VerificationCode
      EmailAddress = createUserReq.Email
    }
    do! sendEmail sendEmailReq 
        |> mapAsyncFailure SendEmailError

    return userId
  }

module Persistence =
  open Domain
  open Chessie.ErrorHandling

  let createUser createUserReq = asyncTrial {
    printfn "%A created" createUserReq 
    return UserId 1
  }
    
module Email =
  open Domain
  open Chessie.ErrorHandling

  let sendSignupEmail signupEmailReq = asyncTrial {
    printfn "Email %A sent" signupEmailReq
    return ()
  }

module Suave =
  open Suave
  open Suave.Filters
  open Suave.Operators
  open Suave.DotLiquid
  open Suave.Form
  open Domain
  open Chessie.ErrorHandling

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

  let handleUserSignupError viewModel errs = 
    match List.head errs with
    | CreateUserError cuErr ->
      handleCreateUserError viewModel cuErr
    | SendEmailError err ->
      handleSendEmailError viewModel err

  let handleUserSignupSuccess viewModel _ =
    sprintf "/signup/success/%s" viewModel.Username
    |> Redirection.FOUND 

  let handleUserSignupResult viewModel result =
    either 
      (handleUserSignupSuccess viewModel)
      (handleUserSignupError viewModel) result

  let handleUserSignupAsyncResult viewModel aResult = 
    aResult
    |> Async.ofAsyncResult
    |> Async.map (handleUserSignupResult viewModel)

  let handleUserSignup signupUser ctx = async {
    match bindEmptyForm ctx.request with
    | Choice1Of2 (vm : UserSignupViewModel) ->
      let result =
        UserSignupRequest.TryCreate (vm.Username, vm.Password, vm.Email)
      match result with
      | Ok (userSignupReq, _) ->
        let userSignupAsyncResult = signupUser userSignupReq
        let! webpart =
          handleUserSignupAsyncResult vm userSignupAsyncResult
        return! webpart ctx
      | Bad msgs ->
        let viewModel = {vm with Error = Some (List.head msgs)}
        return! page signupTemplatePath viewModel ctx
    | Choice2Of2 err ->
      let viewModel = {emptyUserSignupViewModel with Error = Some err}
      return! page signupTemplatePath viewModel ctx
  }

  let webPart () =
    let createUser = Persistence.createUser
    let sendSignupEmail = Email.sendSignupEmail
    let signupUser = Domain.signupUser createUser sendSignupEmail
    choose [
      path "/signup" 
        >=> choose [
          GET >=> page signupTemplatePath emptyUserSignupViewModel
          POST >=> handleUserSignup signupUser
        ]
      pathScan "/signup/success/%s" (page "user/signup_success.liquid")
    ]
      

