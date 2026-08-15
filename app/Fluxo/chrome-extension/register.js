document.addEventListener('DOMContentLoaded', function () {
    window.setTimeout(()=>{
        document.getElementById("link").click();
    },1000);
    //window.open("fluxo-app:chrome-extension://" + chrome.runtime.id + "/");
    document.getElementById("link").href = "fluxo-app:chrome-extension://" + chrome.runtime.id + "/";
}, false);