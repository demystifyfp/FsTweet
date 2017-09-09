module Email

open PostmarkDotNet
open Chessie.ErrorHandling
open System

type Email = {
  To : string
  TemplateId : int64
  PlaceHolders : Map<string,string>
}

type SendEmail = Email -> AsyncResult<unit, Exception>

let mapPostmarkResponse (response : Choice<PostmarkResponse, Exception>) =
  match response with
  | Choice1Of2 postmarkRes ->
    match postmarkRes.Status with
    | PostmarkStatus.Success -> 
      ok ()
    | _ ->
      let ex = new Exception(postmarkRes.Message)
      fail ex
  | Choice2Of2 ex -> fail ex

let sendEmailViaPostmark senderEmailAddress (client : PostmarkClient) email =
  let msg = 
    new TemplatedPostmarkMessage(
      From = senderEmailAddress,
      To = email.To,
      TemplateId = email.TemplateId,
      TemplateModel = email.PlaceHolders
    )
  client.SendMessageAsync(msg)
  |> Async.AwaitTask
  |> Async.Catch
  |> Async.map mapPostmarkResponse
  |> AR

let initSendEmail senderEmailAddress emailClientToken : SendEmail =
  let client = new PostmarkClient(emailClientToken)
  sendEmailViaPostmark senderEmailAddress client


let consoleSendEmail email = asyncTrial {
  printfn "%A" email
}