$(function(){
  var timeAgo = function () {
    return function(val, render) {
      return moment(render(val) + "Z").fromNow()
    };
  }

  var template = `
    <div class="tweet_read_view bg-info">
      <span class="text-muted">@{{tweet.username}} - {{#timeAgo}}{{tweet.time}}{{/timeAgo}}</span>
      <p>{{tweet.tweet}}</p>
    </div>
  `

  window.renderTweet = function($parent, tweet) {
    var htmlOutput = Mustache.render(template, {
        "tweet" : tweet,
        "timeAgo" : timeAgo
    });
    $parent.prepend(htmlOutput);
  };

  $body = $("body");
  
  $(document).on({
      ajaxStart: function() { $body.addClass("loading");    },
       ajaxStop: function() { $body.removeClass("loading"); }    
  });

});