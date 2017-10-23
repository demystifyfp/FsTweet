$(function(){
  let client = stream.connect(fsTweet.stream.apiKey, null, fsTweet.stream.appId);
  let userFeed = client.feed("user", fsTweet.user.id, fsTweet.user.feedToken);

  userFeed.get({
    limit: 25
  }).then(function(body) {
    $(body.results.reverse()).each(function(index, tweet){
      renderTweet($("#tweets"), tweet);
    });
  })
});