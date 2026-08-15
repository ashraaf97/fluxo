"use strict";

fluxo.requestWatcher = {
    requests: {},
    responses: {},
    urlMap: {},
    callback: undefined,

    onErrorOccurred: function (error) {
        this.clearRequestResponse(error.requestId);
    },

    onCompleted: function (details) {
        this.clearRequestResponse(details.requestId);
    },

    clearRequestResponse: function (id) {
        var response = fluxo.requestWatcher.responses[id];
        if (response && response.url) {
            delete fluxo.requestWatcher.urlMap[response.url];
        }
        delete fluxo.requestWatcher.requests[id];
        delete fluxo.requestWatcher.responses[id];
    },

    onSendHeaders: function (info) {
        this.requests[info.requestId] = info;
    },

    onHeadersReceived: function (response) {
        this.responses[response.requestId] = response;
        var request = this.requests[response.requestId];
        if (this.callback) {
            this.callback.onResponse(request, response);
        }
        this.urlMap[response.url] = response.requestId;
    },

    onCreated: function (item) {
        try {
            if (!item) {
                return;
            }
            if (!fluxo.monitoring.isMonitoring()) {
                return;
            }
            if (item.method && item.method === "POST") {
                return;
            }

            var requestId = this.urlMap[item.finalUrl || item.url];
            fluxo.log("urlmap: " + requestId);
            if (!requestId) {
                return;
            }
            var response = this.responses[requestId];
            if (!response) {
                return;
            }
            if (!this.callback.isMatchingDownload(item, response)) {
                return;
            }
            var dl = chrome.downloads || downloads;
            dl.cancel(item.id);
            dl.erase({ id: item.id });
            dl.removeFile(item.id);

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

        var dl = chrome.downloads || downloads;
        dl.onCreated.addListener(this.onCreated.bind(this));
    }
};