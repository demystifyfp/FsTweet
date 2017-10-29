$(function(){
  $("#tweetForm").submit(function(event){
    var $this = $(this);
    var $tweet = $("#tweet");
    event.preventDefault();
    $this.prop('disabled', true);
    $.ajax({
      url : "/tweets",
      type: "post",
      data: JSON.stringify({post : $tweet.val()}),
      contentType: "application/json"
    }).done(function(){
      $this.prop('disabled', false);
      alert("successfully posted");
      $tweet.val('');
    }).fail(function(jqXHR, textStatus, errorThrown) {
      console.log({jqXHR : jqXHR, textStatus : textStatus, errorThrown: errorThrown})
      alert("something went wrong!")
    });

  });

  $("textarea[maxlength]").on("propertychange input", function() {
    if (this.value.length > this.maxlength) {
        this.value = this.value.substring(0, this.maxlength);
    }  
  });

  let client = stream.connect(fsTweet.stream.apiKey, null, fsTweet.stream.appId);
  let userFeed = client.feed("user", fsTweet.user.id, fsTweet.user.feedToken);
  let timelineFeed = client.feed("timeline", fsTweet.user.id, fsTweet.user.timelineToken);

  userFeed.subscribe(function(data){
    renderTweet($("#wall"),data.new[0]);
  });
  timelineFeed.subscribe(function(data){
    renderTweet($("#wall"),data.new[0]);
  });

  timelineFeed.get({
    limit: 25
  }).then(function(body) {
    var timelineTweets = body.results
    userFeed.get({
      limit : 25
    }).then(function(body){
      var userTweets = body.results
      var allTweets = $.merge(timelineTweets, userTweets)
      allTweets.sort(function(t1, t2){
        return new Date(t2.time) - new Date(t1.time);
      })
      $(allTweets.reverse()).each(function(index, tweet){
        renderTweet($("#wall"), tweet);
      });
    })
  })
});