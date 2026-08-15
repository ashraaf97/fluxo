"use strict";

fluxo.requestWatcher = {
    requests: {},
    responses: {},
    callback: undefined,

    onErrorOccurred: function (error) {
        fluxo.log("Error: " + error.requestId);
        fluxo.log(error);
        this.clearRequestResponse(error.requestId);
    },

    onCompleted: function (details) {
        this.clearRequestResponse(details.requestId);
    },

    clearRequestResponse: function (id) {
        delete fluxo.requestWatcher.requests[id];
        delete fluxo.requestWatcher.responses[id];
    },

    onSendHeaders: function (info) {
        this.requests[info.requestId] = info;
        this.responses[info.requestId] = { url: info.url };
    },

    onHeadersReceived: function (response) {
        fluxo.log("Response received: " + response.url);
        this.responses[response.requestId] = response;
        var request = this.requests[response.requestId];
        if (this.callback) {
            this.callback.onResponse(request, response);
        }
    },

    onCreated: function (item) {
        fluxo.log("Download created!");
        try {
            if (!item) {
                return;
            }
            if (!fluxo.monitoring.isMonitoring()) {
                fluxo.log("Not monitoring!");
                return;
            }
            if (item.method && item.method === "POST") {
                return;
            }
            fluxo.log(item);
            var requestId = -1;
            var url = item.finalUrl || item.url;
            for (var k in this.requests) {
                if (this.requests[k].url === url) {
                    requestId = this.requests[k].requestId;
                    break;
                }
            }
            if (requestId === -1) {
                fluxo.log("no request id!");
                return;
            }
            var response = this.responses[requestId];
            if (!this.callback.isMatchingDownload(item, response)) {
                fluxo.log("not matching download!");
                return;
            }
            fluxo.log("Download state: " + item.state);
            if (chrome.runtime.getManifest().applications) {
                try { browser.downloads.cancel(item.id); } catch { }
                try { browser.downloads.removeFile(item.id); } catch { }
                try { browser.downloads.erase({ id: item.id }); } catch { }
            } else {
                try { chrome.downloads.cancel(item.id); } catch { }
                try { chrome.downloads.erase({ id: item.id }); } catch { }
                try { chrome.downloads.removeFile(item.id); } catch { }
            }

            fluxo.log(item.finalUrl + " " + item.referrer + " " +
                item.filename + " " + item.headers + " " + item.method);

            this.callback.onDownload(item, this.requests[requestId], this.responses[requestId]);
        } catch (ex) {
            fluxo.log(ex);
        }
    },

    attach: function (callback) {
        this.callback = callback;
        //This will monitor and intercept files download if 
        //criteria matches and Fluxo is running
        //Use request array to get request headers
        chrome.webRequest.onHeadersReceived.addListener(
            this.onHeadersReceived.bind(this),
            { urls: ["http://*/*", "https://*/*"] },
            ["responseHeaders"]
        );
        try {
            chrome.webRequest.onSendHeaders.addListener(
                this.onSendHeaders.bind(this),
                { urls: ["http://*/*", "https://*/*"] },
                ["requestHeaders", "extraHeaders"]
            );
        } catch {
            chrome.webRequest.onSendHeaders.addListener(
                this.onSendHeaders.bind(this),
                { urls: ["http://*/*", "https://*/*"] },
                ["requestHeaders"]
            );
        }

        chrome.webRequest.onErrorOccurred.addListener(
            this.onErrorOccurred.bind(this),
            { urls: ["http://*/*", "https://*/*"] }
        );

        chrome.webRequest.onCompleted.addListener(
            this.onCompleted.bind(this),
            { urls: ["http://*/*", "https://*/*"] }
        );

        if (chrome.runtime.getManifest().applications) {
            browser.downloads.onCreated.addListener(this.onCreated.bind(this));
        } else {
            chrome.downloads.onCreated.addListener(this.onCreated.bind(this));
        }
    }
};