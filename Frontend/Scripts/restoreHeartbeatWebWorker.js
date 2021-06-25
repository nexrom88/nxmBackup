//send restore heartbeat frequently (3 secs)
setInterval(function () {

  //do heartbeat ajax call with vanilla js
  var xhr = new XMLHttpRequest();
  xhr.onreadystatechange = function () { };
  xhr.open('PUT', '../api/Restore');
  xhr.send()

}, 3000);