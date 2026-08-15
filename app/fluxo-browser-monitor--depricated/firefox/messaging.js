"use strict";
fluxo.messaging = {
    xhrHost: "http://127.0.0.1:9614",
    nativePort: undefined,
    nativeHostVerified: false,
    onDisconnect: function () { },
    onSync: function (data) { },
    connectWithApp: function (onSync, onDisconnect) {
        fluxo.messaging.onDisconnect = onDisconnect;
        fluxo.messaging.onSync = onSync;
        fluxo.messaging.connectNative().then(function (port) {
            fluxo.log("Connected successfully with native host");
            fluxo.messaging.nativePort = port;
        }).catch(function () {
            fluxo.log("Error with native messaging, trying with XHR");
            fluxo.messaging.connectXHR();
        });
        chrome.runtime.onMessage.addListener(fluxo.messaging.onPageMessage);
    },
    sendToFluxo: function (request, response, file, video, referer) {
        fluxo.log("sending to fluxo: " + response.url + " " + fluxo.messaging.nativePort);
        try {
            if (fluxo.messaging.nativePort) {
                fluxo.messaging.sendWithNativeMessaging(request, response, file, video, referer);
            } else {
                fluxo.messaging.sendWithXHR(request, response, file, video, referer);
            }
        } catch (ex) { fluxo.log(ex); }
    },
    sendUrlsToFluxo: function (urls) {
        if (urls && urls.length > 0) {
            fluxo.messaging.sendRecUrl(urls, 0, []);
        }
    },
    connectXHR: function () {
        setInterval(function () { fluxo.messaging.pingXHR(); }, 5000);
    },
    connectNative: function () {
        return new Promise(function (resolve, reject) {
            fluxo.messaging.nativePort = undefined;
            try {
                var nativeHostKey = chrome.runtime.getManifest().applications ? "fluxoff.native_host" : "fluxo_chrome.native_host";
                fluxo.log("Connecting to native messaging host: " + nativeHostKey);
                var port = chrome.runtime.connectNative(nativeHostKey);
                fluxo.log(port);
                if (!port) {
                    fluxo.log("Unable to connect to native messaging host");
                    reject("Unable to connect to native messaging host");
                }
                fluxo.log("Connected to native messaging host");
                port.onDisconnect.addListener(function () {
                    if (!fluxo.messaging.nativePort) {
                        reject("Failed to connect to native messaging host!");
                    } else {
                        fluxo.messaging.onDisconnect();
                        reject("Disconnected from native messaging host!");
                    }
                });
                port.onMessage.addListener(function (data) {
                    if (data.appExited) {
                        fluxo.messaging.postNativeMessage({});
                        fluxo.messaging.onDisconnect();
                    } else {
                        if (!fluxo.messaging.nativePort) {
                            resolve(port);
                        }
                        fluxo.messaging.onSync(data);
                    }
                });
            } catch (err) {
                log("Error while creating native messaging host");
                fluxo.log(err);
                reject("Unable to connect to native messaging host");
            }
        });
    },
    pingXHR: function () {
        var xhr = new XMLHttpRequest();
        xhr.onreadystatechange = function () {
            if (xhr.readyState == XMLHttpRequest.DONE) {
                if (xhr.status == 200) {
                    var data = JSON.parse(xhr.responseText);
                    fluxo.messaging.onSync(data);
                }
                else {
                    fluxo.messaging.onDisconnect();
                }
            }
        };
        xhr.open('GET', fluxo.messaging.xhrHost + "/sync", true);
        xhr.send(null);
    },
    sendRecUrl: function (urls, index, data) {
        if (index > 0 && index == urls.length - 1) {
            fluxo.log(data);
            if (fluxo.messaging.nativePort) {
                fluxo.log("Sending links to native host");
                fluxo.messaging.postNativeMessage({ messageType: "links", messages: data });
            } else {
                var text = "";
                data.forEach(item => {
                    text += "url=" + item.url + "\r\n";
                    text += "res=realUA:" + navigator.userAgent + "\r\n";
                    Object.keys(item.cookies).forEach(function (key) {
                        text += "cookie=" + key + ":" + item.cookies[key] + "\r\n";
                    });
                    text += "\r\n\r\n";
                });
                var xhr = new XMLHttpRequest();
                xhr.open('POST', fluxo.messaging.xhrHost + "/links", true);
                xhr.send(text);
            }
            return;
        }
        var url = urls[index];
        chrome.cookies.getAll({ "url": url }, function (cookies) {
            var cookieDict = {};
            cookies.forEach(cookie => {
                cookieDict[cookie.name] = cookie.value;
            });
            var linkItem = {
                url: url,
                cookies: cookieDict,
                responseHeaders: { realUA: [navigator.userAgent] }
            };
            data.push(linkItem);
            fluxo.messaging.sendRecUrl(urls, index + 1, data);
        });
    },
    sendUrlToFluxo: function (url) {
        fluxo.log("sending to fluxo: " + url);
        if (fluxo.messaging.nativePort) {
            chrome.cookies.getAll({ "url": url }, function (cookies) {
                var cookieDict = {};
                cookies.forEach(cookie => {
                    cookieDict[cookie.name] = cookie.value;
                });
                var data = {
                    url: url,
                    cookies: cookieDict,
                    responseHeaders: { realUA: [navigator.userAgent] }
                }
                fluxo.log(data);
                fluxo.messaging.postNativeMessage({ messageType: "download", message: data });
            });
        } else {
            var data = "url=" + url + "\r\n";
            data += "res=realUA:" + navigator.userAgent + "\r\n";
            chrome.cookies.getAll({ "url": url }, function (cookies) {
                for (var i = 0; i < cookies.length; i++) {
                    var cookie = cookies[i];
                    data += "cookie=" + cookie.name + ":" + cookie.value + "\r\n";
                }
                fluxo.log(data);
                var xhr = new XMLHttpRequest();
                xhr.open('POST', fluxo.messaging.xhrHost + "/download", true);
                xhr.send(data);
            });
        }
    },
    sendWithNativeMessaging: function (request, response, file, video, referer) {
        var data = {
            url: response.url,
            file: file,
            requestHeaders: {},
            responseHeaders: {},
            cookies: {},
            method: request.method
        };
        var hasReferer = false;
        if (request.extraHeaders) {
            request.extraHeaders.forEach(header => {
                fluxo.util.addToValueList(data.requestHeaders, header.name, header.value);
            });
        }
        if (request.requestHeaders) {
            request.requestHeaders.forEach(header => {
                if (header.name.toLowerCase() === 'referer') {
                    hasReferer = true;
                }
                fluxo.util.addToValueList(data.requestHeaders, header.name, header.value);
            });
        }
        if (response.responseHeaders) {
            response.responseHeaders.forEach(header => {
                fluxo.util.addToValueList(data.responseHeaders, header.name, header.value);
            });
        }
        fluxo.util.addToValueList(data.responseHeaders, "tabId", request.tabId);
        fluxo.util.addToValueList(data.responseHeaders, "realUA", navigator.userAgent);

        if (hasReferer === false && referer) {
            data += "req=Referer:" + referer + "\r\n";
        }
        fluxo.messaging.postNativeMessage({ messageType: video ? "video" : "download", message: data });
    },
    sendWithXHR: function (request, response, file, video, referer) {
        fluxo.log("Sending to fluxo using xhr");
        var data = "url=" + response.url + "\r\n";
        if (file) {
            data += "file=" + file + "\r\n";
        }
        var hasReferer = false;
        if (request.extraHeaders) {
            for (var i = 0; i < request.extraHeaders.length; i++) {
                data += "req=" + request.extraHeaders[i].name + ":" + request.extraHeaders[i].value + "\r\n";
                fluxo.log("extraHeaders: " + request.extraHeaders[i].name + ":" + request.extraHeaders[i].value);
            }
        }
        if (request.requestHeaders) {
            for (var i = 0; i < request.requestHeaders.length; i++) {
                if (request.requestHeaders[i].name == 'Referer') {
                    hasReferer = true;
                }
                data += "req=" + request.requestHeaders[i].name + ":" + request.requestHeaders[i].value + "\r\n";
                fluxo.log("requestHeaders: " + request.requestHeaders[i].name + ":" + request.requestHeaders[i].value);
            }
        }
        if (response.responseHeaders) {
            for (var i = 0; i < response.responseHeaders.length; i++) {
                data += "res=" + response.responseHeaders[i].name + ":" + response.responseHeaders[i].value + "\r\n";
                fluxo.log("responseHeaders: " + response.responseHeaders[i].name + ":" + response.responseHeaders[i].value);
            }
        }
        if (hasReferer === false && referer) {
            data += "req=Referer:" + referer + "\r\n";
        }
        data += "res=tabId:" + request.tabId + "\r\n";
        data += "res=realUA:" + navigator.userAgent + "\r\n";
        chrome.cookies.getAll({ "url": response.url }, function (cookies) {
            if (cookies) {
                for (var i = 0; i < cookies.length; i++) {
                    var cookie = cookies[i];
                    data += "cookie=" + cookie.name + ":" + cookie.value + "\r\n";
                }
            }
            fluxo.log(data);
            var xhr = new XMLHttpRequest();
            xhr.open('POST', fluxo.messaging.xhrHost + (video ? "/video" : "/download"), true);
            xhr.send(data);
        });
    },
    postNativeMessage: function (message) {
        if (fluxo.messaging.nativePort) {
            try {
                fluxo.messaging.nativePort.postMessage(message);
            } catch (err) {
                fluxo.log(err);
                try { fluxo.messaging.nativePort.disconnect(); } catch { }
                fluxo.messaging.nativePort = undefined;
                fluxo.messaging.onDisconnect();
            }
        }
    },
    onPageMessage: function (request, sender, sendResponse) {
        if (request.type === "links") {
            fluxo.messaging.sendUrlsToFluxo(request.links);
            sendResponse({ done: "done" });
        }
        else if (request.type === "stat") {
            var resp = {
                isDisabled: fluxo.monitoring.state.disabled,
                list: fluxo.monitoring.videoList,
                noEncoding: fluxo.messaging.nativePort ? true : false
            };
            sendResponse(resp);
        }
        else if (request.type === "cmd") {
            fluxo.monitoring.state.disabled = request.disable;
            fluxo.log("disabled " + disabled);
        }
        else if (request.type === "vid") {
            if (fluxo.monitoring.state.isFluxoUp && fluxo.messaging.nativePort) {
                fluxo.messaging.postNativeMessage({ messageType: "videoIds", videoIds: [request.itemId + ""] });
            } else {
                var xhr = new XMLHttpRequest();
                xhr.open('POST', fluxo.messaging.xhrHost + "/item", true);
                xhr.send(request.itemId);
            }
        }
        else if (request.type === "clear") {
            if (fluxo.messaging.nativePort) {
                fluxo.messaging.postNativeMessage({ messageType: "clear" });
            }
            else {
                var xhr = new XMLHttpRequest();
                xhr.open('GET', fluxo.messaging.xhrHost + "/clear", true);
                xhr.send();
            }
        }
    }
};